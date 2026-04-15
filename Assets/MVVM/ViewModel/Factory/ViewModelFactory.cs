using System;
using Core.DI;
using MVVM.ViewModel.Base;
using MVVM.ViewModel.Interfaces;

namespace MVVM.ViewModel.Factory
{
    public class ViewModelFactory:IViewModelFactory
    {
        private readonly DIContainer _container;
        private IScope _rootScope;
        private readonly object _lock = new object();
        public ViewModelFactory(DIContainer container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
        }
        public T CreateScoped<T>() where T : ViewModelBase
        {
            var scope = GetCurrentScope() ?? _container.CreateScope(GetOrCreateRootScope());
            return CreateForScope<T>(scope);
        }

        public T CreateTransient<T>() where T : ViewModelBase
        {
            var viewModel = _container.GetService<T>();
            if (viewModel == null)
            {
                // 尝试注册并重试（开发时便利）
                _container.RegisterTransient<T, T>();
                viewModel = _container.GetService<T>();
            }

            return viewModel;
        }

        public T GetSingleton<T>() where T : ViewModelBase
        {
            return _container.GetService<T>();
        }

        public T CreateForScope<T>(IScope scope) where T : ViewModelBase
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            var viewModel = _container.GetService<T>(scope);
            if (viewModel == null)
            {
                throw new InvalidOperationException(
                    $"无法创建ViewModel: {typeof(T).Name}。请确保已注册到DI容器。");
            }

            return viewModel;
        }
        private IScope GetCurrentScope()
        {
            // 实现获取当前执行上下文Scope的逻辑
            // 可以通过AsyncLocal或类似机制实现
            // 暂时返回null，由调用方显式传递
            return null;
        }
        private IScope GetOrCreateRootScope()
        {
            lock (_lock)
            {
                return _rootScope ??= _container.CreateScope();
            }
        }
    }
}