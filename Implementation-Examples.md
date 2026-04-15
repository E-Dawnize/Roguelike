# 具体代码改进示例

基于Current-Code-Analysis-Report.md的分析结果，提供可直接实现的代码改进示例。

## 1. DI系统改进示例

### 1.1 Scope嵌套支持扩展

**问题**：现有Scope不支持嵌套，子Scope无法访问父Scope服务

**解决方案**：修改Scope类支持父Scope引用

```csharp
// 在DIContainer.cs中修改Scope类
private class Scope : IScope
{
    private DIContainer Container { get; }
    public Scope ParentScope { get; }
    public ConcurrentDictionary<int, object> ScopedInstances { get; } = new();
    public ConcurrentBag<IDisposable> Disposables { get; } = new();
    public IServiceProvider ServiceProvider { get; }
    private bool _disposed;
    
    // 修改构造函数支持父Scope
    public Scope(DIContainer container, Scope parentScope = null)
    {
        Container = container;
        ParentScope = parentScope;
        ServiceProvider = new ScopedServiceProvider(this);
    }
    
    // 添加父Scope查找支持的方法
    public object GetScopedInstance(int descriptorId)
    {
        // 1. 在当前Scope查找
        if (ScopedInstances.TryGetValue(descriptorId, out var instance))
            return instance;
        
        // 2. 在父Scope查找
        if (ParentScope != null)
        {
            instance = ParentScope.GetScopedInstance(descriptorId);
            if (instance != null)
                return instance;
        }
        
        return null;
    }
    
    public bool HasScopedInstance(int descriptorId)
    {
        if (ScopedInstances.ContainsKey(descriptorId))
            return true;
        
        if (ParentScope != null)
            return ParentScope.HasScopedInstance(descriptorId);
        
        return false;
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
        public object GetService(Type serviceType) => _scope.Container.ResolveService(serviceType, _scope);
    }
}

// 修改DIContainer的ResolveScope方法支持父Scope查找
private object ResolveScope(ServiceDescriptor descriptor, Scope scope)
{
    if (scope == null) throw new InvalidOperationException("Scoped service requires a scope.");
    
    // 首先在Scope链中查找现有实例
    var existingInstance = scope.GetScopedInstance(descriptor.Id);
    if (existingInstance != null)
        return existingInstance;
    
    // 创建新实例
    var instance = CreateInstance(descriptor, scope);
    scope.ScopedInstances[descriptor.Id] = instance;
    if (instance is IDisposable disposable) 
        scope.Disposables.Add(disposable);
    
    return instance;
}

// 修改CreateScope方法支持父Scope
public IScope CreateScope(IScope parentScope = null)
{
    var parent = parentScope as Scope;
    var scope = new Scope(this, parent);
    return scope;
}
```

### 1.2 构造函数调用性能优化

**问题**：使用反射调用构造函数性能较低

**解决方案**：使用表达式树编译构造函数调用

```csharp
// 在DIContainer类中添加缓存和编译方法
private readonly ConcurrentDictionary<ConstructorInfo, Func<object[], object>> _constructorInvokers = new();

private object CreateInstance(ServiceDescriptor descriptor, Scope scope)
{
    if (descriptor.ImplementationFactory != null)
    {
        return descriptor.ImplementationFactory(scope != null ? scope.ServiceProvider : this);
    }

    if (descriptor.ImplementationInstance != null)
    {
        return descriptor.ImplementationInstance;
    }
    
    var implementationType = descriptor.ImplementationType;
    var constructor = GetConstructor(implementationType);
    
    // 使用缓存的构造函数调用器
    var invoker = _constructorInvokers.GetOrAdd(constructor, CompileConstructorInvoker);
    
    var parameters = constructor.GetParameters();
    var paramValues = new object[parameters.Length];
    for (int i = 0; i < parameters.Length; i++)
    {
        var paramType = parameters[i].ParameterType;
        var val = TryResolveEnumerable(paramType, scope, out var enumerableValue) 
            ? enumerableValue 
            : ResolveService(paramType, scope);
        
        val = val ?? (parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null);
        if (val == null) 
            throw new InvalidOperationException($"Missing dependency: {paramType}");
        
        paramValues[i] = val;
    }
    
    return invoker(paramValues);
}

private Func<object[], object> CompileConstructorInvoker(ConstructorInfo constructor)
{
    var parameters = constructor.GetParameters();
    var argsParam = Expression.Parameter(typeof(object[]), "args");
    
    var argsExpressions = new Expression[parameters.Length];
    for (int i = 0; i < parameters.Length; i++)
    {
        var index = Expression.Constant(i);
        var parameterType = parameters[i].ParameterType;
        var argAccess = Expression.ArrayIndex(argsParam, index);
        argsExpressions[i] = Expression.Convert(argAccess, parameterType);
    }
    
    var newExpression = Expression.New(constructor, argsExpressions);
    var lambda = Expression.Lambda<Func<object[], object>>(
        Expression.Convert(newExpression, typeof(object)), 
        argsParam);
    
    return lambda.Compile();
}

private ConstructorInfo GetConstructor(Type type)
{
    var constructors = type.GetConstructors();
    
    // 优先使用标记了[Inject]的构造函数
    var injectedConstructor = constructors.FirstOrDefault(c => 
        c.GetCustomAttributes(typeof(InjectAttribute), false).Length > 0);
    
    if (injectedConstructor != null)
        return injectedConstructor;
    
    // 使用参数最多的构造函数
    return constructors.OrderByDescending(c => c.GetParameters().Length).First();
}
```

## 2. MVVM框架改进示例

### 2.1 ViewModelBase命令系统扩展

**问题**：ViewModelBase缺少命令系统支持

**解决方案**：添加完整的命令系统，支持同步和异步命令

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Core.DI;
using Core.Events.EventInterfaces;
using MVVM.Interfaces;

namespace MVVM.ViewModel.Base
{
    public abstract class ViewModelBase : INotifyPropertyChanged, IViewModel, IInitializable, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        [Inject] protected IEventCenter EventCenter;
        
        // 命令系统
        private readonly Dictionary<string, ICommand> _commands = new();
        private bool _isInitialized;
        private bool _isDisposed;
        
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public virtual bool SetProperty<T>(ref T property, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(property, value)) 
                return false;
            
            property = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        
        // 命令注册和获取
        protected void RegisterCommand(string name, ICommand command)
        {
            _commands[name] = command;
        }
        
        protected ICommand GetCommand(string name)
        {
            return _commands.TryGetValue(name, out var command) ? command : null;
        }
        
        protected ICommand CreateCommand(Action execute, Func<bool> canExecute = null)
        {
            return new RelayCommand(execute, canExecute);
        }
        
        protected ICommand CreateAsyncCommand(Func<Task> execute, Func<bool> canExecute = null)
        {
            return new AsyncCommand(execute, canExecute);
        }
        
        protected ICommand CreateCommand<T>(Action<T> execute, Func<T, bool> canExecute = null)
        {
            return new RelayCommand<T>(execute, canExecute);
        }
        
        // 初始化生命周期
        public void Initialize()
        {
            if (_isInitialized) return;
            
            OnInitializing();
            PerformInitialization();
            OnInitialized();
            
            _isInitialized = true;
        }
        
        protected virtual void OnInitializing() { }
        protected virtual void OnInitialized() { }
        
        private void PerformInitialization()
        {
            // 注入依赖（对于非MonoBehaviour的ViewModel）
            if (!(this is UnityEngine.Component))
            {
                // 通过ServiceLocator或DI容器注入
                // 这里需要根据项目实际情况实现
            }
            
            // 初始化命令
            InitializeCommands();
            
            // 订阅事件
            SubscribeToEvents();
        }
        
        protected virtual void InitializeCommands() { }
        protected virtual void SubscribeToEvents() { }
        protected virtual void UnsubscribeFromEvents() { }
        
        // 清理生命周期
        public void Dispose()
        {
            if (_isDisposed) return;
            
            OnDisposing();
            UnsubscribeFromEvents();
            CleanupResources();
            OnDisposed();
            
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
        
        protected virtual void OnDisposing() { }
        protected virtual void OnDisposed() { }
        protected virtual void CleanupResources() { }
        
        // 基础命令实现
        public class RelayCommand : ICommand
        {
            private readonly Action _execute;
            private readonly Func<bool> _canExecute;
            
            public event EventHandler CanExecuteChanged;
            
            public RelayCommand(Action execute, Func<bool> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }
            
            public bool CanExecute(object parameter)
            {
                return _canExecute?.Invoke() ?? true;
            }
            
            public void Execute(object parameter)
            {
                _execute();
            }
            
            public void RaiseCanExecuteChanged()
            {
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        
        public class RelayCommand<T> : ICommand
        {
            private readonly Action<T> _execute;
            private readonly Func<T, bool> _canExecute;
            
            public event EventHandler CanExecuteChanged;
            
            public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }
            
            public bool CanExecute(object parameter)
            {
                if (_canExecute == null) return true;
                
                if (parameter is T typedParameter)
                    return _canExecute(typedParameter);
                    
                return false;
            }
            
            public void Execute(object parameter)
            {
                if (parameter is T typedParameter)
                    _execute(typedParameter);
            }
            
            public void RaiseCanExecuteChanged()
            {
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        
        // 异步命令实现
        public class AsyncCommand : ICommand
        {
            private readonly Func<Task> _execute;
            private readonly Func<bool> _canExecute;
            private bool _isExecuting;
            
            public event EventHandler CanExecuteChanged;
            
            public AsyncCommand(Func<Task> execute, Func<bool> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }
            
            public bool CanExecute(object parameter)
            {
                return !_isExecuting && (_canExecute?.Invoke() ?? true);
            }
            
            public async void Execute(object parameter)
            {
                if (CanExecute(parameter))
                {
                    try
                    {
                        _isExecuting = true;
                        RaiseCanExecuteChanged();
                        
                        await _execute();
                    }
                    finally
                    {
                        _isExecuting = false;
                        RaiseCanExecuteChanged();
                    }
                }
            }
            
            public void RaiseCanExecuteChanged()
            {
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
```

### 2.2 ValidatableViewModelBase验证系统

**问题**：缺少数据验证支持

**解决方案**：创建支持IDataErrorInfo的验证基类

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace MVVM.ViewModel.Base
{
    public abstract class ValidatableViewModelBase : ViewModelBase, IDataErrorInfo
    {
        private readonly Dictionary<string, List<string>> _errors = new();
        
        public string this[string columnName]
        {
            get
            {
                if (_errors.TryGetValue(columnName, out var columnErrors) && columnErrors.Count > 0)
                {
                    return string.Join("; ", columnErrors);
                }
                return string.Empty;
            }
        }
        
        public string Error
        {
            get
            {
                var allErrors = _errors.Values.SelectMany(e => e).ToList();
                return allErrors.Count > 0 ? string.Join("; ", allErrors) : string.Empty;
            }
        }
        
        public bool HasErrors => _errors.Count > 0;
        
        protected void AddError(string propertyName, string error)
        {
            if (!_errors.ContainsKey(propertyName))
                _errors[propertyName] = new List<string>();
            
            if (!_errors[propertyName].Contains(error))
                _errors[propertyName].Add(error);
                
            OnPropertyChanged(propertyName);
        }
        
        protected void ClearErrors(string propertyName)
        {
            if (_errors.ContainsKey(propertyName))
            {
                _errors.Remove(propertyName);
                OnPropertyChanged(propertyName);
            }
        }
        
        protected bool ValidateProperty<T>(string propertyName, T value, Func<T, ValidationResult> validator)
        {
            ClearErrors(propertyName);
            
            var result = validator(value);
            if (!result.IsValid)
            {
                AddError(propertyName, result.ErrorMessage);
                return false;
            }
            
            return true;
        }
        
        protected void ValidateAllProperties()
        {
            // 子类可以重写此方法来验证所有属性
        }
        
        public class ValidationResult
        {
            public bool IsValid { get; }
            public string ErrorMessage { get; }
            
            public ValidationResult(bool isValid, string errorMessage = null)
            {
                IsValid = isValid;
                ErrorMessage = errorMessage;
            }
            
            public static ValidationResult Valid() => new ValidationResult(true);
            public static ValidationResult Invalid(string errorMessage) => new ValidationResult(false, errorMessage);
        }
        
        // 内置验证规则
        public static Func<string, ValidationResult> RequiredValidator(string fieldName)
        {
            return value => string.IsNullOrWhiteSpace(value) 
                ? ValidationResult.Invalid($"{fieldName}不能为空")
                : ValidationResult.Valid();
        }
        
        public static Func<int, ValidationResult> RangeValidator(string fieldName, int min, int max)
        {
            return value => value < min || value > max
                ? ValidationResult.Invalid($"{fieldName}必须在{min}和{max}之间")
                : ValidationResult.Valid();
        }
        
        public static Func<float, ValidationResult> RangeValidator(string fieldName, float min, float max)
        {
            return value => value < min || value > max
                ? ValidationResult.Invalid($"{fieldName}必须在{min}和{max}之间")
                : ValidationResult.Valid();
        }
    }
}
```

## 3. 绑定系统改进示例

### 3.1 BindingManager完整实现

**问题**：BindingManager只有基本框架，缺乏实际管理功能

**解决方案**：实现完整的绑定注册、查找和清理功能

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Core.Events.EventInterfaces;
using MVVM.Binding.Interfaces;

namespace MVVM.Binding
{
    public class BindingManager : IBindingManager, IDisposable
    {
        private readonly Dictionary<object, List<IBinding>> _viewModelBindings = new();
        private readonly Dictionary<GameObject, List<IBinding>> _viewBindings = new();
        private readonly Dictionary<string, List<IBinding>> _bindingGroups = new();
        private readonly IEventCenter _eventCenter;
        private bool _disposed;
        
        public BindingManager(IEventCenter eventCenter)
        {
            _eventCenter = eventCenter ?? throw new ArgumentNullException(nameof(eventCenter));
            
            // 订阅场景卸载事件
            _eventCenter.Subscribe<SceneUnloadingEvent>(OnSceneUnloading);
        }
        
        public void RegisterBinding(IBinding binding, object viewModel = null, GameObject view = null, string group = null)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            
            // 注册到ViewModel
            if (viewModel != null)
            {
                if (!_viewModelBindings.TryGetValue(viewModel, out var vmBindings))
                {
                    vmBindings = new List<IBinding>();
                    _viewModelBindings[viewModel] = vmBindings;
                }
                vmBindings.Add(binding);
            }
            
            // 注册到View
            if (view != null)
            {
                if (!_viewBindings.TryGetValue(view, out var vBindings))
                {
                    vBindings = new List<IBinding>();
                    _viewBindings[view] = vBindings;
                }
                vBindings.Add(binding);
            }
            
            // 注册到分组
            if (!string.IsNullOrEmpty(group))
            {
                if (!_bindingGroups.TryGetValue(group, out var groupBindings))
                {
                    groupBindings = new List<IBinding>();
                    _bindingGroups[group] = groupBindings;
                }
                groupBindings.Add(binding);
            }
            
            Debug.Log($"绑定注册: {binding.GetType().Name}, ViewModel: {viewModel?.GetType().Name}, View: {view?.name}");
        }
        
        public void UnregisterBinding(IBinding binding)
        {
            if (binding == null) return;
            
            // 从ViewModel绑定中移除
            var vmKeysToRemove = new List<object>();
            foreach (var kvp in _viewModelBindings)
            {
                if (kvp.Value.Remove(binding) && kvp.Value.Count == 0)
                {
                    vmKeysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in vmKeysToRemove)
            {
                _viewModelBindings.Remove(key);
            }
            
            // 从View绑定中移除
            var viewKeysToRemove = new List<GameObject>();
            foreach (var kvp in _viewBindings)
            {
                if (kvp.Value.Remove(binding) && kvp.Value.Count == 0)
                {
                    viewKeysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in viewKeysToRemove)
            {
                _viewBindings.Remove(key);
            }
            
            // 从分组中移除
            var groupKeysToRemove = new List<string>();
            foreach (var kvp in _bindingGroups)
            {
                if (kvp.Value.Remove(binding) && kvp.Value.Count == 0)
                {
                    groupKeysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in groupKeysToRemove)
            {
                _bindingGroups.Remove(key);
            }
        }
        
        public void UnregisterAllForViewModel(object viewModel)
        {
            if (viewModel == null) return;
            
            if (_viewModelBindings.TryGetValue(viewModel, out var bindings))
            {
                foreach (var binding in bindings.ToList()) // 使用ToList防止集合修改
                {
                    binding.Unbind();
                    UnregisterBinding(binding);
                }
            }
        }
        
        public void UnregisterAllForView(GameObject view)
        {
            if (view == null) return;
            
            if (_viewBindings.TryGetValue(view, out var bindings))
            {
                foreach (var binding in bindings.ToList())
                {
                    binding.Unbind();
                    UnregisterBinding(binding);
                }
            }
        }
        
        public void UnregisterAllInGroup(string group)
        {
            if (string.IsNullOrEmpty(group)) return;
            
            if (_bindingGroups.TryGetValue(group, out var bindings))
            {
                foreach (var binding in bindings.ToList())
                {
                    binding.Unbind();
                    UnregisterBinding(binding);
                }
            }
        }
        
        public IEnumerable<IBinding> GetBindingsForViewModel(object viewModel)
        {
            if (viewModel == null) return Enumerable.Empty<IBinding>();
            
            return _viewModelBindings.TryGetValue(viewModel, out var bindings) 
                ? bindings.ToList() 
                : Enumerable.Empty<IBinding>();
        }
        
        public IEnumerable<IBinding> GetBindingsForView(GameObject view)
        {
            if (view == null) return Enumerable.Empty<IBinding>();
            
            return _viewBindings.TryGetValue(view, out var bindings) 
                ? bindings.ToList() 
                : Enumerable.Empty<IBinding>();
        }
        
        public IEnumerable<IBinding> GetBindingsInGroup(string group)
        {
            if (string.IsNullOrEmpty(group)) return Enumerable.Empty<IBinding>();
            
            return _bindingGroups.TryGetValue(group, out var bindings) 
                ? bindings.ToList() 
                : Enumerable.Empty<IBinding>();
        }
        
        public int GetBindingCount()
        {
            // 去重统计绑定数量
            var allBindings = new HashSet<IBinding>();
            
            foreach (var bindings in _viewModelBindings.Values)
                foreach (var binding in bindings)
                    allBindings.Add(binding);
            
            return allBindings.Count;
        }
        
        private void OnSceneUnloading(SceneUnloadingEvent evt)
        {
            CleanupSceneBindings(evt.Scene);
        }
        
        private void CleanupSceneBindings(Scene scene)
        {
            var viewsToRemove = new List<GameObject>();
            
            foreach (var kvp in _viewBindings)
            {
                if (kvp.Key.scene == scene)
                {
                    UnregisterAllForView(kvp.Key);
                    viewsToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var view in viewsToRemove)
            {
                _viewBindings.Remove(view);
            }
            
            Debug.Log($"清理场景绑定: {scene.name}, 移除 {viewsToRemove.Count} 个View的绑定");
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;
            
            // 清理所有绑定
            var allBindings = new HashSet<IBinding>();
            foreach (var bindings in _viewModelBindings.Values)
                foreach (var binding in bindings)
                    allBindings.Add(binding);
            
            foreach (var binding in allBindings)
            {
                binding.Unbind();
            }
            
            _viewModelBindings.Clear();
            _viewBindings.Clear();
            _bindingGroups.Clear();
            
            // 取消事件订阅
            if (_eventCenter != null)
            {
                _eventCenter.Unsubscribe<SceneUnloadingEvent>(OnSceneUnloading);
            }
            
            Debug.Log("BindingManager已清理");
        }
        
        // 调试工具方法
        public string GetDebugInfo()
        {
            var info = $"BindingManager状态:\n";
            info += $"- ViewModel绑定: {_viewModelBindings.Count} 个ViewModel\n";
            info += $"- View绑定: {_viewBindings.Count} 个GameObject\n";
            info += $"- 分组绑定: {_bindingGroups.Count} 个分组\n";
            info += $"- 总绑定数: {GetBindingCount()} 个绑定\n";
            
            return info;
        }
    }
    
    // 场景卸载事件定义（需要在事件系统中定义）
    public class SceneUnloadingEvent : IEvent
    {
        public Scene Scene { get; set; }
    }
}
```

### 3.2 自动绑定注册扩展

**问题**：绑定组件需要手动注册到BindingManager

**解决方案**：扩展PropertyBinding和CommandBinding支持自动注册

```csharp
// 在PropertyBinding.cs中添加自动注册支持
public class PropertyBinding : MonoBehaviour, IPropertyBinding, IBinding
{
    [SerializeField] private GameObject _viewModelObject;
    [SerializeField] private string _sourceProperty;
    [SerializeField] private Component _targetComponent;
    [SerializeField] private string _targetProperty;
    [SerializeField] private BindingMode _bindingMode = BindingMode.OneWay;
    
    private object _viewModel;
    private PropertyInfo _sourcePropertyInfo;
    private PropertyInfo _targetPropertyInfo;
    private bool _isBound;
    
    private void Awake()
    {
        // 自动注册到BindingManager
        AutoRegisterBinding();
    }
    
    private void AutoRegisterBinding()
    {
        // 获取ViewModel（如果设置了_viewModelObject）
        object viewModel = null;
        if (_viewModelObject != null)
        {
            var vmComponent = _viewModelObject.GetComponent<IViewModel>();
            if (vmComponent != null)
            {
                viewModel = vmComponent;
            }
        }
        
        // 注册到BindingManager
        var bindingManager = ServiceLocator.GetService<IBindingManager>();
        if (bindingManager != null)
        {
            bindingManager.RegisterBinding(this, viewModel, gameObject);
        }
        else
        {
            Debug.LogWarning("BindingManager未找到，自动注册失败");
        }
    }
    
    private void OnDestroy()
    {
        // 从BindingManager注销
        var bindingManager = ServiceLocator.GetService<IBindingManager>();
        if (bindingManager != null)
        {
            bindingManager.UnregisterBinding(this);
        }
        
        Unbind();
    }
    
    // 实现IBinding接口
    public void Bind()
    {
        if (_isBound) return;
        
        // 绑定逻辑...
        _isBound = true;
    }
    
    public void Unbind()
    {
        if (!_isBound) return;
        
        // 解绑逻辑...
        _isBound = false;
    }
    
    public bool IsBound => _isBound;
}

// 在CommandBinding.cs中添加类似的支持
public class CommandBinding : MonoBehaviour, ICommandBinding, IBinding
{
    [SerializeField] private GameObject _viewModelObject;
    [SerializeField] private string _commandName;
    [SerializeField] private UIButton _targetButton;
    
    private ICommand _command;
    private bool _isBound;
    
    private void Awake()
    {
        AutoRegisterBinding();
    }
    
    private void AutoRegisterBinding()
    {
        object viewModel = null;
        if (_viewModelObject != null)
        {
            var vmComponent = _viewModelObject.GetComponent<IViewModel>();
            if (vmComponent != null)
            {
                viewModel = vmComponent;
            }
        }
        
        var bindingManager = ServiceLocator.GetService<IBindingManager>();
        if (bindingManager != null)
        {
            bindingManager.RegisterBinding(this, viewModel, gameObject);
        }
    }
    
    private void OnDestroy()
    {
        var bindingManager = ServiceLocator.GetService<IBindingManager>();
        if (bindingManager != null)
        {
            bindingManager.UnregisterBinding(this);
        }
        
        Unbind();
    }
    
    // IBinding实现...
}
```

## 4. ECS集成改进示例

### 4.1 EntityChangeDetectionSystem基础实现

```csharp
using Unity.Collections;
using Unity.Entities;
using Unity.Burst;
using System.Collections.Generic;

namespace ECS.Integration
{
    // ECS组件变化事件定义
    public struct ComponentChangedEvent : IComponentData
    {
        public Entity Entity;
        public ComponentType ComponentType;
        public ChangeType ChangeType; // 创建、更新、销毁
    }
    
    public enum ChangeType
    {
        Created,
        Updated,
        Destroyed
    }
    
    // 变化检测系统
    [BurstCompile]
    public partial struct EntityChangeDetectionSystem : ISystem
    {
        private EntityQuery _changedEntitiesQuery;
        private NativeHashMap<Entity, ulong> _lastFrameComponentMask;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // 查询所有有变化的实体
            _changedEntitiesQuery = state.GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new ComponentType[] 
                    { 
                        ComponentType.ReadOnly<IChangeTrackedComponent>() 
                    }
                }
            );
            
            _lastFrameComponentMask = new NativeHashMap<Entity, ulong>(1000, Allocator.Persistent);
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var entityTypeHandle = state.GetEntityTypeHandle();
            var changedEvents = new NativeList<ComponentChangedEvent>(Allocator.TempJob);
            
            var detectionJob = new ChangeDetectionJob
            {
                EntityTypeHandle = entityTypeHandle,
                LastFrameComponentMask = _lastFrameComponentMask,
                ChangedEvents = changedEvents,
                EntityManager = state.EntityManager
            };
            
            detectionJob.Schedule(_changedEntitiesQuery, state.Dependency).Complete();
            
            // 处理检测到的事件
            if (changedEvents.Length > 0)
            {
                ProcessChangedEvents(changedEvents);
            }
            
            changedEvents.Dispose();
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _lastFrameComponentMask.Dispose();
        }
        
        private void ProcessChangedEvents(NativeList<ComponentChangedEvent> events)
        {
            // 这里可以将事件发布到EventCenter
            // 例如：EventCenter.Publish(new EcsComponentChangedEvent(events));
        }
        
        [BurstCompile]
        private struct ChangeDetectionJob : IJobEntityBatch
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public NativeHashMap<Entity, ulong> LastFrameComponentMask;
            public NativeList<ComponentChangedEvent> ChangedEvents;
            public EntityManager EntityManager;
            
            public void Execute(ArchetypeChunk batch, int batchIndex)
            {
                var entities = batch.GetNativeArray(EntityTypeHandle);
                
                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    var currentMask = CalculateComponentMask(entity);
                    
                    if (LastFrameComponentMask.TryGetValue(entity, out var previousMask))
                    {
                        var changes = currentMask ^ previousMask;
                        if (changes != 0)
                        {
                            // 检测到变化，创建事件
                            ChangedEvents.Add(new ComponentChangedEvent
                            {
                                Entity = entity,
                                ComponentType = ComponentType.ReadOnly<IChangeTrackedComponent>(),
                                ChangeType = ChangeType.Updated
                            });
                        }
                        LastFrameComponentMask[entity] = currentMask;
                    }
                    else
                    {
                        // 新实体
                        ChangedEvents.Add(new ComponentChangedEvent
                        {
                            Entity = entity,
                            ComponentType = ComponentType.ReadOnly<IChangeTrackedComponent>(),
                            ChangeType = ChangeType.Created
                        });
                        LastFrameComponentMask[entity] = currentMask;
                    }
                }
            }
            
            private ulong CalculateComponentMask(Entity entity)
            {
                // 简化版本：实际需要根据组件类型计算掩码
                return 0;
            }
        }
    }
    
    // 标记接口，表示需要跟踪变化的组件
    public interface IChangeTrackedComponent : IComponentData { }
}
```

## 5. 实施建议

### 5.1 实施顺序

1. **立即实施**（本周）：
   - DI系统的Scope嵌套支持
   - ViewModelBase命令系统
   - BindingManager完整实现

2. **短期实施**（下周）：
   - 自动绑定注册扩展
   - 值转换器系统
   - EntityChangeDetectionSystem基础

3. **长期规划**：
   - 完整的ECS事件系统
   - 高级验证系统
   - 性能优化

### 5.2 测试策略

1. **DI系统测试**：
   ```csharp
   // 测试循环依赖检测
   public class ServiceA { public ServiceA(ServiceB b) { } }
   public class ServiceB { public ServiceB(ServiceA a) { } }
   // 应抛出InvalidOperationException
   ```

2. **命令系统测试**：
   ```csharp
   // 测试AsyncCommand
   var commandExecuted = false;
   var command = new AsyncCommand(async () => 
   {
       await Task.Delay(100);
       commandExecuted = true;
   });
   command.Execute(null);
   // 验证commandExecuted变为true
   ```

3. **绑定管理器测试**：
   ```csharp
   // 测试绑定清理
   var manager = new BindingManager(eventCenter);
   var binding = new MockBinding();
   manager.RegisterBinding(binding, viewModel, gameObject);
   manager.UnregisterAllForViewModel(viewModel);
   // 验证binding.IsBound为false
   ```

### 5.3 注意事项

1. **向后兼容性**：所有改进应保持与现有代码的兼容性
2. **性能考虑**：在高频更新的系统中注意性能影响
3. **内存管理**：使用弱引用避免内存泄漏
4. **错误处理**：提供详细的错误信息和恢复机制

这些代码示例可以直接集成到现有项目中，逐步改进架构的完整性和技术深度。