using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Core.Tools;
using JetBrains.Annotations;

namespace Core.DI
{
    public enum ServiceLifetime
    {
        Transient,
        Scoped,
        Singleton
    }

    public interface IScope : IDisposable
    {
        IServiceProvider ServiceProvider { get; }
    }
    [AttributeUsage(AttributeTargets.Constructor)]
    public class InjectAttribute: Attribute{}

    public class ServiceDescriptor
    {
        public Type ServiceType;
        public ServiceLifetime Lifetime;
        [CanBeNull] public Type ImplementationType;
        [CanBeNull] public object ImplementationInstance;
        [CanBeNull] public Func<IServiceProvider, object> ImplementationFactory;
        public ServiceDescriptor(Type serviceType, Type implementationType,ServiceLifetime lifetime)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
            Lifetime = lifetime;
        }
        public ServiceDescriptor(Type serviceType, object implementationInstance)
        {
            ServiceType = serviceType;
            ImplementationInstance = implementationInstance;
            Lifetime = ServiceLifetime.Singleton;
        }
        public ServiceDescriptor(Type serviceType,Func<IServiceProvider, object> implementationFactory,ServiceLifetime lifetime)
        {
            ServiceType = serviceType;
            ImplementationFactory = implementationFactory;
            Lifetime = lifetime;
        }
    }
    public class DIContainer:MonoSingleton<DIContainer>,IServiceProvider,IDisposable
    {
        private readonly ConcurrentDictionary<Type, ServiceDescriptor> _serviceDescriptors = new();//服务描述符
        private readonly ConcurrentDictionary<Type, object> _singletonInstances = new();//实现实例
        private readonly ConcurrentBag<IDisposable> _disposables = new();//待释放服务
        private readonly ThreadLocal<Stack<Type>> _resolveStack = new(() => new Stack<Type>());//递归依赖检测栈
        private bool _disposed;
        
        #region 注册方法

        private void Register(ServiceDescriptor descriptor)=>_serviceDescriptors[descriptor.ServiceType] = descriptor;
        
        public void RegisterTransient<TService, TImplementation>() where TImplementation : TService
            =>Register(new ServiceDescriptor(typeof(TService), typeof(TImplementation), ServiceLifetime.Transient));
        public void RegisterSingleton<TService, TImplementation>() where TImplementation : TService 
            =>Register(new ServiceDescriptor(typeof(TService), typeof(TImplementation), ServiceLifetime.Singleton));
        public void RegisterScoped<TService, TImplementation>() where TImplementation : TService
            =>Register(new ServiceDescriptor(typeof(TService), typeof(TImplementation), ServiceLifetime.Scoped));
        

        public void RegisterSingleton<TService>(TService implementationInstance) where TService:class
            =>Register(new ServiceDescriptor(typeof(TService), implementationInstance));
        
        public void RegisterTransient<TService>(Func<IServiceProvider, object> implementationFactory) where TService:class
            =>Register(new ServiceDescriptor(typeof(TService), sp=>implementationFactory(sp)!, ServiceLifetime.Transient));
        public void RegisterSingleton<TService>(Func<IServiceProvider, object> implementationFactory) where TService:class
            =>Register(new ServiceDescriptor(typeof(TService), sp=>implementationFactory(sp)!, ServiceLifetime.Singleton));
        public void RegisterScoped<TService>(Func<IServiceProvider, object> implementationFactory) where TService:class
            =>Register(new ServiceDescriptor(typeof(TService), sp=>implementationFactory(sp)!, ServiceLifetime.Scoped));
        #endregion

        #region 服务解析

        [CanBeNull]
        public object GetService(Type serviceType)=>ResolveService(serviceType, null);

        #endregion

        #region 解析
        public T? GetService<T>() => (T?)GetService(typeof(T));
        [CanBeNull]
        private object ResolveService(Type serviceType,[CanBeNull] Scope scope)
        {
            if (_resolveStack.Value!.Contains(serviceType))
                throw new InvalidOperationException($"Circular dependency: {string.Join(" -> ", _resolveStack.Value.Reverse())} -> {serviceType}");
            
            _resolveStack.Value.Push(serviceType);
            try
            {
                return ResolveCore(serviceType, scope);
            }
            finally
            {
                _resolveStack.Value.Pop();
            }
        }

        [CanBeNull]
        private object ResolveCore(Type serviceType, [CanBeNull] Scope scope)
        {
            if (_serviceDescriptors.TryGetValue(serviceType, out var descriptor))
            {
                return ResolveDescriptor(descriptor, scope);
            }
            if (!serviceType.IsInterface && !serviceType.IsAbstract && !serviceType.IsValueType)
            {
                var autoDesc = new ServiceDescriptor(serviceType, serviceType, ServiceLifetime.Transient);
                return ResolveDescriptor(autoDesc, scope);
            }
            return null;
        }

        [CanBeNull]
        private object ResolveDescriptor(ServiceDescriptor descriptor, [CanBeNull] Scope scope)
        {
            return descriptor.Lifetime switch
            {
                ServiceLifetime.Singleton => ResolveSingleton(descriptor),
                ServiceLifetime.Scoped => ResolveScope(descriptor, scope),
                ServiceLifetime.Transient => ResolveTransient(descriptor, scope)
            };
        }

        private object ResolveSingleton(ServiceDescriptor descriptor)
        {
            if (_singletonInstances.TryGetValue(descriptor.ServiceType, out var instance))
            {
                return instance;
            }

            lock (_singletonInstances)
            {
                if (_singletonInstances.TryGetValue(descriptor.ServiceType, out instance))
                {
                    return instance;
                }
                instance = CreateInstance(descriptor, null);
                _singletonInstances[descriptor.ServiceType] = instance;
                if(instance is  IDisposable disposable)_disposables.Add(disposable);
                return instance;
            }
            
        }
        private object ResolveScope(ServiceDescriptor descriptor,Scope scope)
        {
            if (scope.ScopedInstances.TryGetValue(descriptor.ServiceType, out var instance))
            {
                return instance;
            }
            instance = CreateInstance(descriptor, scope);
            scope.ScopedInstances[descriptor.ServiceType] = instance;
            if(instance is IDisposable disposable) scope.Disposables.Add(disposable);
            
            return instance;
        }

        private object ResolveTransient(ServiceDescriptor descriptor,[CanBeNull] Scope scope)
        {
            var instance = CreateInstance(descriptor, null);
            if (instance is IDisposable disposable)
            {
                if(scope!=null)scope.Disposables.Add(disposable);
                else _disposables.Add(disposable);
            }

            return instance;
        }
        private readonly ConcurrentDictionary<Type,ConstructorInfo> _constructorsCache = new ConcurrentDictionary<Type, ConstructorInfo>();
        private object CreateInstance(ServiceDescriptor descriptor, [CanBeNull] Scope scope)
        {
            if (descriptor.ImplementationFactory != null)
            {
                return descriptor.ImplementationFactory(scope!=null?scope.ServiceProvider:this);
            }

            if (descriptor.ImplementationInstance != null)
            {
                return descriptor.ImplementationInstance;
            }
            
            var implementationType = descriptor.ImplementationType;
            var ctor=_constructorsCache.GetOrAdd(implementationType,type=>
            {
                var constructors=type.GetConstructors();
                return constructors.FirstOrDefault(c
                           => c.GetCustomAttributes(typeof(InjectAttribute), false).Length != 0) ??
                       constructors.OrderByDescending(c => c.GetParameters().Length).First();
            });
            var parameters = ctor.GetParameters();
            var paramValues = new object?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                paramValues[i] = ResolveService(paramType, scope)??
                                 (parameters[i].HasDefaultValue?parameters[i].DefaultValue:null);
            }
            
            return ctor.Invoke(paramValues);
        }
        #endregion

        #region 资源释放
        public void Dispose()
        {
            if(_disposed)return;
            _disposed = true;
            foreach (var obj in _disposables)
            {
                obj.Dispose();
            }
            _disposables.Clear();
            _singletonInstances.Clear();
        }
        #endregion
        
        #region 作用域实现

        public IScope CreateScope()
        {
            var scope=new Scope(this);
            _disposables.Add(scope);
            return scope;
        }
        private class Scope:IScope
        {
            private DIContainer Container { get; }
            public ConcurrentDictionary<Type, object> ScopedInstances { get; } = new();
            public ConcurrentBag<IDisposable> Disposables { get; } = new();
            public IServiceProvider ServiceProvider { get; }
            private bool _disposed;
            public Scope(DIContainer container)
            {
                Container = container;
                ServiceProvider=new ScopedServiceProvider(this);
            }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                foreach (var disposable in Disposables)
                {
                    disposable.Dispose();
                }
                Disposables.Clear();
                ScopedInstances.Clear();
            }
            private class ScopedServiceProvider : IServiceProvider
            {
                private readonly Scope _scope;
                public ScopedServiceProvider(Scope scope) => _scope = scope;
                public object? GetService(Type serviceType) => _scope.Container.ResolveService(serviceType, _scope);
            }
        }

        #endregion
    }
    public static class ServiceProviderExtensions
    {
        public static T? GetService<T>(this IServiceProvider sp)
            => (T?)sp.GetService(typeof(T));
    }
}