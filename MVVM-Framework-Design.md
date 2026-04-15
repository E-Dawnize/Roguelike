# MVVM框架详细设计

## 概述

MVVM（Model-View-ViewModel）框架是本项目的表示层核心，负责处理用户界面逻辑和数据绑定。本框架专为Unity游戏开发优化，提供完整的**数据绑定**、**命令系统**和**UI逻辑分离**支持。

## 设计目标

### 核心目标
1. **UI逻辑分离**：清晰分离UI显示和业务逻辑
2. **数据绑定自动化**：减少UI状态管理的样板代码
3. **命令模式支持**：统一处理用户交互
4. **可测试性**：ViewModel独立于UI，便于单元测试
5. **性能优化**：高效的数据绑定和事件处理

### Unity特定目标
1. **UGUI集成**：无缝集成Unity UI系统
2. **编辑器支持**：Inspector配置和可视化调试
3. **场景生命周期**：与Unity场景管理集成
4. **序列化支持**：支持Prefab和场景保存

## 架构设计

### 框架组件关系图

```
┌─────────────────────────────────────────────────────────┐
│                     View Layer                           │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐  │
│  │   UI        │───▶│  Binding    │───▶│   View      │  │
│  │ Components  │    │  Components │    │   Model     │  │
│  │ (Button等)   │    │ (Property/  │    │  (逻辑层)    │  │
│  └─────────────┘    │  Command)   │    └──────┬──────┘  │
└─────────────────────┴─────────────┴───────────┼─────────┘
                                                │
┌───────────────────────────────────────────────┼─────────┐
│                Binding Layer                   │         │
│  ┌─────────────┐    ┌─────────────┐    ┌──────┴──────┐  │
│  │ Binding     │───▶│  Binding    │───▶│  Value      │  │
│  │ Manager     │    │  Context    │    │  Converter  │  │
│  │ (集中管理)    │    │  (分组)      │    │  (转换器)    │  │
│  └─────────────┘    └─────────────┘    └─────────────┘  │
└───────────────────────────────────────────────┬─────────┘
                                                │
┌───────────────────────────────────────────────┼─────────┐
│               ViewModel Layer                  │         │
│  ┌─────────────┐    ┌─────────────┐    ┌──────┴──────┐  │
│  │ ViewModel   │───▶│   Command   │───▶│   Model     │  │
│  │   Base      │    │   System    │    │  (数据层)    │  │
│  │ (基类)       │    │  (ICommand)  │    │             │  │
│  └─────────────┘    └─────────────┘    └─────────────┘  │
└─────────────────────────────────────────────────────────┘
```

### 核心类层次结构

```
IViewModel (接口)
├── ViewModelBase (抽象基类)
│   ├── PlayerViewModel (示例实现)
│   ├── EnemyViewModel
│   └── ...
│
IModel (接口)
├── ModelBase (抽象基类)
│   ├── EntityModel (ECS集成)
│   ├── PlayerModel
│   └── ...
│
IBinding (接口)
├── IPropertyBinding
│   └── PropertyBinding (实现)
├── ICommandBinding
│   └── CommandBinding (实现)
└── IBindingManager
    └── BindingManager (实现)
```

## ViewModel设计

### 1. ViewModelBase基类
**核心职责**：提供数据绑定基础支持

```csharp
public abstract class ViewModelBase : INotifyPropertyChanged, IViewModel
{
    // 属性变更通知
    public event PropertyChangedEventHandler PropertyChanged;
    
    // 依赖注入支持
    [Inject] protected IEventCenter EventCenter;
    
    // 核心方法：属性设置与通知
    protected virtual bool SetProperty<T>(ref T property, T value, 
                                        [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(property, value))
            return false;
            
        property = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    
    // 属性变更触发
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    // 生命周期接口
    public virtual void Initialize() { }
    public virtual void OnStart() { }
    public virtual void Dispose() { }
}
```

### 2. 典型ViewModel示例
```csharp
public class PlayerViewModel : ViewModelBase
{
    private string _playerName;
    private int _health;
    private int _maxHealth;
    private ICommand _attackCommand;
    
    // 可绑定属性
    public string PlayerName
    {
        get => _playerName;
        set => SetProperty(ref _playerName, value);
    }
    
    public int Health
    {
        get => _health;
        set => SetProperty(ref _health, value);
    }
    
    public int MaxHealth
    {
        get => _maxHealth;
        set => SetProperty(ref _maxHealth, value);
    }
    
    // 计算属性（只读）
    public float HealthPercentage => (float)Health / MaxHealth;
    
    // 命令属性
    public ICommand AttackCommand
    {
        get => _attackCommand;
        set => SetProperty(ref _attackCommand, value);
    }
    
    // 初始化
    public override void Initialize()
    {
        // 初始化命令
        AttackCommand = new RelayCommand(ExecuteAttack, CanExecuteAttack);
        
        // 订阅事件
        EventCenter.Subscribe<PlayerHealthChangedEvent>(OnHealthChanged);
    }
    
    private void ExecuteAttack(object parameter)
    {
        // 执行攻击逻辑
        EventCenter.Publish(new AttackEvent { Target = parameter as string });
    }
    
    private bool CanExecuteAttack(object parameter)
    {
        return Health > 0; // 只有活着才能攻击
    }
    
    private void OnHealthChanged(PlayerHealthChangedEvent evt)
    {
        Health = evt.NewHealth;
    }
}
```

### 3. 命令系统设计

#### ICommand接口实现
```csharp
public interface ICommand
{
    event EventHandler CanExecuteChanged;
    bool CanExecute(object parameter);
    void Execute(object parameter);
}

public class RelayCommand : ICommand
{
    private readonly Action<object> _execute;
    private readonly Func<object, bool> _canExecute;
    
    public event EventHandler CanExecuteChanged;
    
    public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }
    
    public bool CanExecute(object parameter)
        => _canExecute?.Invoke(parameter) ?? true;
    
    public void Execute(object parameter)
        => _execute(parameter);
    
    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
```

#### 异步命令支持
```csharp
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object, Task> _execute;
    private readonly Func<object, bool> _canExecute;
    private bool _isExecuting;
    
    public event EventHandler CanExecuteChanged;
    
    public AsyncRelayCommand(Func<object, Task> execute, Func<object, bool> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }
    
    public bool CanExecute(object parameter)
        => (_canExecute?.Invoke(parameter) ?? true) && !_isExecuting;
    
    public async void Execute(object parameter)
    {
        if (!CanExecute(parameter)) return;
        
        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();
            
            await _execute(parameter);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }
    
    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
```

## Model设计

### 1. ModelBase基类
**核心职责**：提供数据模型基础，支持变更通知

```csharp
public enum ModelChangeType { Hp, MaxHp, Attack, Speed, Position }

public readonly struct ModelChanged
{
    public readonly ModelChangeType Type;
    public readonly int Delta;
    public readonly int Current;
    
    public ModelChanged(ModelChangeType type, int delta, int current)
    {
        Type = type;
        Delta = delta;
        Current = current;
    }
}

public abstract class ModelBase
{
    // 模型变更事件
    public event Action<ModelChanged> Changed;
    
    // 触发变更通知
    protected void NotifyChanged(ModelChanged evt)
        => Changed?.Invoke(evt);
}
```

### 2. EntityModel（ECS集成）
**核心职责**：桥接ECS Entity和MVVM Model

```csharp
public class EntityModel : ModelBase
{
    #region ECS集成属性
    
    public Entity EntityId { get; private set; }
    
    private int _currentHealth;
    public int CurrentHealth
    {
        get => _currentHealth;
        set
        {
            int delta = value - _currentHealth;
            _currentHealth = value;
            NotifyChanged(new ModelChanged(ModelChangeType.Hp, delta, _currentHealth));
        }
    }
    
    private int _maxHealth;
    public int MaxHealth
    {
        get => _maxHealth;
        set
        {
            _maxHealth = value;
            NotifyChanged(new ModelChanged(ModelChangeType.MaxHp, 0, _maxHealth));
        }
    }
    
    #endregion
    
    #region 业务方法
    
    public EntityModel(Entity entity, int maxHealth)
    {
        EntityId = entity;
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
    }
    
    public void TakeDamage(int damage)
    {
        CurrentHealth -= damage;
        
        if (CurrentHealth <= 0)
        {
            CurrentHealth = 0;
            // 触发死亡事件
        }
    }
    
    public void Heal(int amount)
    {
        CurrentHealth = Math.Min(CurrentHealth + amount, MaxHealth);
    }
    
    #endregion
    
    #region 生命周期管理
    
    public void Reinitialize(Entity newEntity, int newMaxHealth)
    {
        EntityId = newEntity;
        MaxHealth = newMaxHealth;
        CurrentHealth = newMaxHealth;
    }
    
    public void Reset()
    {
        EntityId = Entity.Null;
        _currentHealth = 0;
        _maxHealth = 0;
    }
    
    #endregion
}
```

### 3. 集合Model支持
```csharp
public class ObservableCollectionModel<T> : ModelBase
{
    private readonly ObservableCollection<T> _items = new();
    
    public ObservableCollection<T> Items => _items;
    
    public ObservableCollectionModel()
    {
        _items.CollectionChanged += OnCollectionChanged;
    }
    
    private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        // 集合变更通知
        NotifyChanged(new ModelChanged(ModelChangeType.Collection, e.Action, _items.Count));
    }
    
    public void Add(T item) => _items.Add(item);
    public void Remove(T item) => _items.Remove(item);
    public void Clear() => _items.Clear();
}
```

## 绑定系统设计

### 1. PropertyBinding设计
**核心职责**：实现属性值绑定，支持双向数据同步

#### 绑定模式
```csharp
public enum BindingMode
{
    OneWay,      // ViewModel → View
    TwoWay,      // ViewModel ↔ View
    OneTime,     // ViewModel → View（仅一次）
    OneWayToSource // View → ViewModel
}
```

#### 核心实现
```csharp
public class PropertyBinding : MonoBehaviour, IPropertyBinding
{
    #region 序列化字段
    
    [Header("绑定配置")]
    [SerializeField] private MonoBehaviour _viewModel;
    [SerializeField] private string _viewModelPropertyName;
    
    [Header("目标组件")]
    [SerializeField] private Component _targetComponent;
    [SerializeField] private string _targetPropertyName;
    
    [Header("绑定模式")]
    [SerializeField] private BindingMode _bindingMode = BindingMode.OneWay;
    
    [Header("值转换")]
    [SerializeField] private string _valueConverterName;
    [SerializeField] private bool _useConverter;
    
    #endregion
    
    #region 运行时字段
    
    private INotifyPropertyChanged _notifyViewModel;
    private PropertyInfo _viewModelProperty;
    private PropertyInfo _targetProperty;
    private IValueConverter _valueConverter;
    private bool _isUpdating;
    
    #endregion
    
    #region 生命周期
    
    private void Awake()
    {
        // 自动注册到BindingManager
        if (BindingSystem.Instance != null)
            BindingSystem.Instance.RegisterBinding(this, _viewModel);
    }
    
    private void Start()
    {
        SetupBinding();
    }
    
    private void OnDestroy()
    {
        CleanupBinding();
        
        // 从BindingManager注销
        if (BindingSystem.Instance != null)
            BindingSystem.Instance.UnregisterBinding(this);
    }
    
    #endregion
    
    #region 绑定逻辑
    
    private void SetupBinding()
    {
        // 验证组件
        if (_viewModel == null || _targetComponent == null)
        {
            Debug.LogError("PropertyBinding: ViewModel or TargetComponent is null", this);
            return;
        }
        
        // 获取ViewModel属性
        _viewModelProperty = _viewModel.GetType().GetProperty(_viewModelPropertyName);
        if (_viewModelProperty == null)
        {
            Debug.LogError($"PropertyBinding: Property '{_viewModelPropertyName}' not found on {_viewModel.GetType().Name}", this);
            return;
        }
        
        // 获取目标属性
        _targetProperty = _targetComponent.GetType().GetProperty(_targetPropertyName);
        if (_targetProperty == null)
        {
            Debug.LogError($"PropertyBinding: Property '{_targetPropertyName}' not found on {_targetComponent.GetType().Name}", this);
            return;
        }
        
        // 设置值转换器
        if (_useConverter && !string.IsNullOrEmpty(_valueConverterName))
        {
            _valueConverter = ValueConverterRegistry.Instance?.GetConverter(_valueConverterName);
        }
        
        // 订阅PropertyChanged事件
        _notifyViewModel = _viewModel as INotifyPropertyChanged;
        if (_notifyViewModel != null)
        {
            _notifyViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
        
        // 初始同步
        UpdateTarget();
        
        // 双向绑定：订阅UI变化
        if (_bindingMode == BindingMode.TwoWay || _bindingMode == BindingMode.OneWayToSource)
        {
            SetupUIBinding();
        }
    }
    
    private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == _viewModelPropertyName || string.IsNullOrEmpty(e.PropertyName))
        {
            UpdateTarget();
        }
    }
    
    private void UpdateTarget()
    {
        if (_isUpdating) return;
        
        try
        {
            _isUpdating = true;
            
            var value = _viewModelProperty.GetValue(_viewModel);
            
            // 值转换
            if (_valueConverter != null)
            {
                value = _valueConverter.Convert(value, _targetProperty.PropertyType);
            }
            
            _targetProperty.SetValue(_targetComponent, value);
        }
        catch (Exception ex)
        {
            Debug.LogError($"PropertyBinding: Failed to update target - {ex.Message}", this);
        }
        finally
        {
            _isUpdating = false;
        }
    }
    
    private void UpdateSource()
    {
        if (_isUpdating) return;
        
        try
        {
            _isUpdating = true;
            
            var value = _targetProperty.GetValue(_targetComponent);
            
            // 值转换（反向）
            if (_valueConverter != null)
            {
                value = _valueConverter.ConvertBack(value, _viewModelProperty.PropertyType);
            }
            
            _viewModelProperty.SetValue(_viewModel, value);
        }
        catch (Exception ex)
        {
            Debug.LogError($"PropertyBinding: Failed to update source - {ex.Message}", this);
        }
        finally
        {
            _isUpdating = false;
        }
    }
    
    #endregion
}
```

### 2. CommandBinding设计
**核心职责**：将UI事件绑定到ViewModel命令

#### 参数类型支持
```csharp
public enum BindingParameterType
{
    None,           // 无参数
    FixedValue,     // 固定值
    PropertyPath,   // ViewModel属性路径
    EventArgument   // UI事件参数
}
```

#### 核心实现
```csharp
public class CommandBinding : MonoBehaviour, ICommandBinding
{
    #region 序列化字段
    
    [Header("绑定配置")]
    [SerializeField] private MonoBehaviour _viewModel;
    [SerializeField] private string _commandName;
    [SerializeField] private string _eventName = "onClick";
    
    [Header("目标组件")]
    [SerializeField] private Component _targetComponent;
    
    [Header("命令参数")]
    [SerializeField] private BindingParameterType _parameterType = BindingParameterType.None;
    [SerializeField] private string _parameterPropertyPath;
    [SerializeField] private string _parameterValue;
    
    #endregion
    
    #region 运行时字段
    
    private ICommand _command;
    private MethodInfo _methodInfo;
    private PropertyInfo _propertyInfo;
    private UnityEvent _unityEvent;
    
    #endregion
    
    #region 绑定逻辑
    
    private void SetupCommandBinding()
    {
        // 获取命令（ICommand属性或普通方法）
        _propertyInfo = _viewModel.GetType().GetProperty(_commandName);
        if (_propertyInfo != null && typeof(ICommand).IsAssignableFrom(_propertyInfo.PropertyType))
        {
            _command = (ICommand)_propertyInfo.GetValue(_viewModel);
            _command.CanExecuteChanged += OnCanExecuteChanged;
        }
        else
        {
            // 尝试作为方法
            _methodInfo = _viewModel.GetType().GetMethod(_commandName);
            if (_methodInfo == null)
            {
                Debug.LogError($"CommandBinding: Command '{_commandName}' not found on {_viewModel.GetType().Name}", this);
                return;
            }
        }
        
        // 获取UI事件
        var eventProperty = _targetComponent.GetType().GetProperty(_eventName);
        if (eventProperty != null && typeof(UnityEvent).IsAssignableFrom(eventProperty.PropertyType))
        {
            _unityEvent = (UnityEvent)eventProperty.GetValue(_targetComponent);
            _unityEvent.AddListener(OnUIEvent);
        }
        else
        {
            Debug.LogError($"CommandBinding: Event '{_eventName}' not found on {_targetComponent.GetType().Name}", this);
            return;
        }
        
        // 初始更新UI状态
        UpdateTarget();
    }
    
    private void OnUIEvent()
    {
        object parameter = GetCommandParameter();
        
        if (_command != null)
        {
            if (_command.CanExecute(parameter))
                _command.Execute(parameter);
        }
        else if (_methodInfo != null)
        {
            _methodInfo.Invoke(_viewModel, new object[] { parameter });
        }
    }
    
    private object GetCommandParameter()
    {
        return _parameterType switch
        {
            BindingParameterType.None => null,
            BindingParameterType.FixedValue => ConvertParameterValue(_parameterValue),
            BindingParameterType.PropertyPath => GetPropertyValue(_parameterPropertyPath),
            BindingParameterType.EventArgument => GetEventArgument(),
            _ => null
        };
    }
    
    private void OnCanExecuteChanged(object sender, EventArgs e)
    {
        UpdateTarget();
    }
    
    private void UpdateTarget()
    {
        if (_targetComponent is MonoBehaviour mb)
        {
            bool canExecute = _command?.CanExecute(null) ?? true;
            if (mb is Button button) button.interactable = canExecute;
            else if (mb is Selectable selectable) selectable.interactable = canExecute;
        }
    }
    
    #endregion
}
```

### 3. 值转换器系统

#### 值转换器接口
```csharp
public interface IValueConverter
{
    object Convert(object value, Type targetType);
    object ConvertBack(object value, Type targetType);
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType)
    {
        if (value is bool boolValue)
            return boolValue ? 1f : 0f; // 透明度
        return 1f;
    }
    
    public object ConvertBack(object value, Type targetType)
    {
        if (value is float floatValue)
            return floatValue > 0.5f;
        return true;
    }
}

public class IntToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType)
    {
        return value?.ToString() ?? string.Empty;
    }
    
    public object ConvertBack(object value, Type targetType)
    {
        if (value is string str && int.TryParse(str, out int result))
            return result;
        return 0;
    }
}
```

#### 转换器注册表
```csharp
public class ValueConverterRegistry
{
    private static ValueConverterRegistry _instance;
    public static ValueConverterRegistry Instance => _instance ??= new ValueConverterRegistry();
    
    private readonly Dictionary<string, IValueConverter> _converters = new();
    
    private ValueConverterRegistry()
    {
        // 注册内置转换器
        Register("BoolToVisibility", new BoolToVisibilityConverter());
        Register("IntToString", new IntToStringConverter());
        Register("FloatToString", new FloatToStringConverter());
    }
    
    public void Register(string name, IValueConverter converter)
    {
        _converters[name] = converter;
    }
    
    public IValueConverter GetConverter(string name)
    {
        return _converters.TryGetValue(name, out var converter) ? converter : null;
    }
}
```

## BindingManager设计

### 1. 核心接口
```csharp
public interface IBindingManager : IDisposable
{
    // 绑定管理
    void RegisterBinding(IBinding binding, object context = null);
    void UnregisterBinding(IBinding binding);
    
    // 上下文管理
    void BindAllInContext(object context);
    void UnbindAllInContext(object context);
    void RebindAllInContext(object context);
    
    // 批量操作
    void BindAll();
    void UnbindAll();
    void RebindAll();
    
    // 查询与调试
    int GetBindingCount();
    List<IBinding> GetBindingsInContext(object context);
    string GetDebugInfo();
}
```

### 2. 实现核心
```csharp
public class BindingManager : IBindingManager
{
    private readonly IEventCenter _eventCenter;
    private readonly ILogger _logger;
    
    // 绑定存储
    private readonly List<IBinding> _activeBindings = new();
    private readonly Dictionary<object, List<IBinding>> _contextBindings = new();
    private readonly Dictionary<IBinding, object> _bindingContexts = new();
    
    // 性能优化
    private readonly Lazy<Dictionary<Type, List<IBinding>>> _bindingsByType;
    private readonly ReaderWriterLockSlim _lock = new();
    
    public BindingManager(IEventCenter eventCenter, ILogger logger = null)
    {
        _eventCenter = eventCenter;
        _logger = logger;
        
        _bindingsByType = new Lazy<Dictionary<Type, List<IBinding>>>(() => 
            new Dictionary<Type, List<IBinding>>());
    }
    
    public void RegisterBinding(IBinding binding, object context = null)
    {
        _lock.EnterWriteLock();
        try
        {
            _activeBindings.Add(binding);
            
            if (context != null)
            {
                if (!_contextBindings.TryGetValue(context, out var contextList))
                {
                    contextList = new List<IBinding>();
                    _contextBindings[context] = contextList;
                }
                contextList.Add(binding);
                _bindingContexts[binding] = context;
            }
            
            // 类型索引
            var bindingType = binding.GetType();
            if (!_bindingsByType.Value.TryGetValue(bindingType, out var typeList))
            {
                typeList = new List<IBinding>();
                _bindingsByType.Value[bindingType] = typeList;
            }
            typeList.Add(binding);
            
            _logger?.LogInfo($"Binding registered: {binding.GetType().Name} for context {context?.GetType().Name}");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
    
    public void UnbindAllInContext(object context)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_contextBindings.TryGetValue(context, out var bindings))
            {
                foreach (var binding in bindings.ToArray()) // ToArray避免修改集合
                {
                    UnregisterBinding(binding);
                }
                _contextBindings.Remove(context);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
    
    public string GetDebugInfo()
    {
        _lock.EnterReadLock();
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Binding Manager Debug Info ===");
            sb.AppendLine($"Total Bindings: {_activeBindings.Count}");
            sb.AppendLine($"Context Count: {_contextBindings.Count}");
            
            sb.AppendLine("\n=== Bindings by Context ===");
            foreach (var kvp in _contextBindings)
            {
                sb.AppendLine($"Context: {kvp.Key.GetType().Name} ({kvp.Key.GetHashCode()})");
                sb.AppendLine($"  Binding Count: {kvp.Value.Count}");
                foreach (var binding in kvp.Value)
                {
                    sb.AppendLine($"    - {binding.GetType().Name}");
                }
            }
            
            return sb.ToString();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}
```

### 3. 静态门面类
```csharp
public static class BindingSystem
{
    // 主访问点
    public static IBindingManager Instance 
        => ProjectContext.GetService<IBindingManager>();
    
    // 快捷方法
    public static void Register(IBinding binding, object context = null)
    {
        if (Instance == null)
        {
            Debug.LogWarning("BindingManager not available, binding will not be managed.");
            return;
        }
        Instance.RegisterBinding(binding, context);
    }
    
    public static void UnbindAllInContext(object context)
        => Instance?.UnbindAllInContext(context);
    
    public static void UnbindAll()
        => Instance?.UnbindAll();
    
    // 编辑器辅助
#if UNITY_EDITOR
    [MenuItem("Tools/MVVM/Show Binding Graph")]
    public static void ShowBindingGraph()
    {
        if (Instance != null)
        {
            BindingGraphWindow.ShowWindow(Instance);
        }
    }
#endif
}
```

## 编辑器集成

### 1. 自定义Inspector
```csharp
#if UNITY_EDITOR
[CustomEditor(typeof(PropertyBinding))]
public class PropertyBindingEditor : Editor
{
    private SerializedProperty _viewModelProp;
    private SerializedProperty _viewModelPropertyNameProp;
    private SerializedProperty _targetComponentProp;
    private SerializedProperty _targetPropertyNameProp;
    private SerializedProperty _bindingModeProp;
    
    private void OnEnable()
    {
        _viewModelProp = serializedObject.FindProperty("_viewModel");
        _viewModelPropertyNameProp = serializedObject.FindProperty("_viewModelPropertyName");
        _targetComponentProp = serializedObject.FindProperty("_targetComponent");
        _targetPropertyNameProp = serializedObject.FindProperty("_targetPropertyName");
        _bindingModeProp = serializedObject.FindProperty("_bindingMode");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        EditorGUILayout.LabelField("Property Binding", EditorStyles.boldLabel);
        
        // ViewModel选择
        EditorGUILayout.PropertyField(_viewModelProp, new GUIContent("ViewModel"));
        
        // 动态显示属性列表
        if (_viewModelProp.objectReferenceValue != null)
        {
            var viewModel = _viewModelProp.objectReferenceValue as MonoBehaviour;
            var properties = viewModel.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .Select(p => p.Name)
                .ToArray();
            
            var currentIndex = Array.IndexOf(properties, _viewModelPropertyNameProp.stringValue);
            var newIndex = EditorGUILayout.Popup("ViewModel Property", currentIndex, properties);
            
            if (newIndex != currentIndex)
            {
                _viewModelPropertyNameProp.stringValue = properties[newIndex];
            }
        }
        
        // 目标组件选择
        EditorGUILayout.PropertyField(_targetComponentProp, new GUIContent("Target Component"));
        
        // 绑定模式
        EditorGUILayout.PropertyField(_bindingModeProp);
        
        // 绑定状态显示
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Binding Status", EditorStyles.boldLabel);
        
        var binding = target as PropertyBinding;
        if (binding != null && binding.IsBound)
        {
            EditorGUILayout.HelpBox("Binding is active", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("Binding not active", MessageType.Warning);
        }
        
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
```

### 2. 绑定关系可视化窗口
```csharp
#if UNITY_EDITOR
public class BindingGraphWindow : EditorWindow
{
    private IBindingManager _bindingManager;
    private Vector2 _scrollPosition;
    private Dictionary<object, bool> _contextFoldouts = new();
    
    public static void ShowWindow(IBindingManager manager)
    {
        var window = GetWindow<BindingGraphWindow>("Binding Graph");
        window._bindingManager = manager;
        window.minSize = new Vector2(400, 300);
    }
    
    private void OnGUI()
    {
        if (_bindingManager == null)
        {
            EditorGUILayout.HelpBox("BindingManager not available", MessageType.Warning);
            return;
        }
        
        EditorGUILayout.LabelField("Binding Relationship Graph", EditorStyles.boldLabel);
        
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        
        // 显示绑定统计
        EditorGUILayout.LabelField($"Total Bindings: {_bindingManager.GetBindingCount()}");
        
        // 按上下文显示绑定
        var contexts = _bindingManager.GetAllContexts();
        foreach (var context in contexts)
        {
            var bindings = _bindingManager.GetBindingsInContext(context);
            
            var foldoutKey = context ?? "null";
            if (!_contextFoldouts.ContainsKey(foldoutKey))
                _contextFoldouts[foldoutKey] = false;
            
            var contextName = context?.GetType().Name ?? "Global";
            _contextFoldouts[foldoutKey] = EditorGUILayout.Foldout(
                _contextFoldouts[foldoutKey], $"{contextName} ({bindings.Count} bindings)");
            
            if (_contextFoldouts[foldoutKey])
            {
                EditorGUI.indentLevel++;
                foreach (var binding in bindings)
                {
                    EditorGUILayout.LabelField($"- {binding.GetType().Name}");
                }
                EditorGUI.indentLevel--;
            }
        }
        
        EditorGUILayout.EndScrollView();
        
        // 刷新按钮
        if (GUILayout.Button("Refresh"))
        {
            Repaint();
        }
    }
}
#endif
```

## 性能优化策略

### 1. 绑定性能优化
```csharp
// 使用缓存减少反射调用
private static class PropertyCache
{
    private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> _cache = new();
    
    public static PropertyInfo GetProperty(Type type, string propertyName)
    {
        if (!_cache.TryGetValue(type, out var typeCache))
        {
            typeCache = new Dictionary<string, PropertyInfo>();
            _cache[type] = typeCache;
        }
        
        if (!typeCache.TryGetValue(propertyName, out var property))
        {
            property = type.GetProperty(propertyName, 
                BindingFlags.Public | BindingFlags.Instance);
            typeCache[propertyName] = property;
        }
        
        return property;
    }
}
```

### 2. 事件订阅优化
```csharp
// 弱事件模式避免内存泄漏
public class WeakPropertyChangedEvent
{
    private readonly WeakReference<INotifyPropertyChanged> _source;
    private readonly PropertyChangedEventHandler _handler;
    
    public WeakPropertyChangedEvent(INotifyPropertyChanged source, 
                                   PropertyChangedEventHandler handler)
    {
        _source = new WeakReference<INotifyPropertyChanged>(source);
        _handler = handler;
        source.PropertyChanged += OnPropertyChanged;
    }
    
    private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (_source.TryGetTarget(out var target))
        {
            _handler?.Invoke(target, e);
        }
        else
        {
            // 源对象已被回收，清理事件
            if (sender is INotifyPropertyChanged notify)
                notify.PropertyChanged -= OnPropertyChanged;
        }
    }
}
```

### 3. 批量更新支持
```csharp
public class BatchUpdateScope : IDisposable
{
    private readonly BindingManager _manager;
    
    public BatchUpdateScope(BindingManager manager)
    {
        _manager = manager;
        _manager.BeginBatchUpdate();
    }
    
    public void Dispose()
    {
        _manager.EndBatchUpdate();
    }
}

// 使用示例
using (new BatchUpdateScope(bindingManager))
{
    // 批量更新多个属性
    viewModel.Property1 = value1;
    viewModel.Property2 = value2;
    viewModel.Property3 = value3;
    // 只触发一次UI更新
}
```

## 使用指南

### 1. 基础使用示例
```csharp
// 创建ViewModel
public class PlayerViewModel : ViewModelBase
{
    private string _name;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
    
    private int _score;
    public int Score
    {
        get => _score;
        set => SetProperty(ref _score, value);
    }
    
    public ICommand AttackCommand { get; }
    
    public PlayerViewModel()
    {
        AttackCommand = new RelayCommand(ExecuteAttack);
    }
    
    private void ExecuteAttack(object parameter)
    {
        Score += 10;
    }
}

// 在场景中设置绑定
// 1. 创建PlayerViewModel组件
// 2. 添加PropertyBinding到Text组件，绑定到Name属性
// 3. 添加PropertyBinding到另一个Text组件，绑定到Score属性  
// 4. 添加CommandBinding到Button组件，绑定到AttackCommand
```

### 2. 高级使用示例
```csharp
// 集合绑定
public class InventoryViewModel : ViewModelBase
{
    public ObservableCollection<ItemModel> Items { get; } = new();
    
    public ICommand AddItemCommand { get; }
    public ICommand RemoveItemCommand { get; }
    
    public InventoryViewModel()
    {
        AddItemCommand = new RelayCommand(AddItem);
        RemoveItemCommand = new RelayCommand(RemoveItem);
    }
    
    private void AddItem(object parameter)
    {
        Items.Add(new ItemModel { Name = "New Item" });
    }
}

// 值转换器使用
// 在PropertyBinding中设置ValueConverter为"BoolToVisibility"
// 将bool属性绑定到UI元素的透明度
```

### 3. 测试示例
```csharp
[TestFixture]
public class PlayerViewModelTests
{
    [Test]
    public void Score_WhenSet_PropertyChangedFired()
    {
        var viewModel = new PlayerViewModel();
        var changedProperties = new List<string>();
        
        viewModel.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName);
        
        viewModel.Score = 100;
        
        Assert.That(changedProperties, Contains.Item(nameof(PlayerViewModel.Score)));
        Assert.That(viewModel.Score, Is.EqualTo(100));
    }
    
    [Test]
    public void AttackCommand_WhenExecuted_ScoreIncreases()
    {
        var viewModel = new PlayerViewModel();
        viewModel.Score = 0;
        
        viewModel.AttackCommand.Execute(null);
        
        Assert.That(viewModel.Score, Is.EqualTo(10));
    }
}
```

## 已知问题与改进计划

### 当前问题
1. **性能问题**：大量绑定时的反射开销
2. **内存泄漏风险**：事件订阅可能导致泄漏
3. **编辑器体验**：属性选择不够智能
4. **错误处理**：绑定失败时信息不够详细

### 改进计划
1. **性能优化**：添加属性访问缓存，减少反射
2. **内存安全**：实现弱事件模式，避免泄漏
3. **编辑器增强**：智能属性过滤和搜索
4. **错误恢复**：绑定失败时的优雅降级
5. **高级功能**：集合绑定、验证规则、动画绑定

## 总结

本MVVM框架为Unity游戏开发提供了完整的**数据绑定**和**命令系统**支持，具有以下特点：

### 技术优势
1. **完整的MVVM实现**：ViewModel、Model、View清晰分离
2. **高性能绑定**：优化的属性访问和事件处理
3. **丰富的绑定类型**：属性绑定、命令绑定、集合绑定
4. **强大的编辑器支持**：可视化配置和调试工具
5. **良好的扩展性**：支持自定义值转换器和绑定类型

### 工程价值
1. **提高开发效率**：减少UI状态管理代码
2. **提升代码质量**：清晰的关注点分离
3. **便于测试**：ViewModel独立于UI，易于单元测试
4. **降低维护成本**：松耦合的架构设计

### 学习价值
1. **现代UI架构实践**：MVVM模式在游戏开发中的应用
2. **数据绑定原理**：深入理解属性变更通知机制
3. **命令模式应用**：统一的用户交互处理
4. **性能优化技巧**：反射优化、内存管理、事件处理

这个框架不仅是一个功能实现，更是一个**工程化实践**和**技术能力展示**，适合作为高级Unity工程师的技术能力证明。