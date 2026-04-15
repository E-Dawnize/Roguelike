# DI系统详细设计

## 概述

依赖注入（DI）系统是本项目的基础设施核心，负责管理所有组件的生命周期和依赖关系。本设计文档详细说明DI系统的架构设计、实现细节、使用模式和扩展机制。

## 设计目标

### 核心目标
1. **依赖管理**：清晰管理组件间的依赖关系
2. **生命周期控制**：支持Singleton、Scoped、Transient三种生命周期
3. **解耦测试**：支持Mock替换，便于单元测试
4. **配置驱动**：通过ScriptableObject可视化配置服务注册
5. **Unity集成**：与Unity生命周期无缝集成

### 非功能性目标
1. **性能**：服务解析快速，内存使用高效
2. **线程安全**：支持多线程环境下的安全访问
3. **可调试性**：提供详细的依赖图调试信息
4. **可扩展性**：支持自定义生命周期和解析策略

## 架构设计

### 核心组件关系图

```
┌─────────────────────────────────────────────────────────┐
│                   Application Layer                      │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐  │
│  │   Client    │───▶│   Service   │───▶│  Service    │  │
│  │   Code      │    │  Consumer   │    │  Provider   │  │
│  └─────────────┘    └─────────────┘    └─────────────┘  │
└─────────────────────────────┬─────────────────────────────┘
                              │
┌─────────────────────────────┼─────────────────────────────┐
│                  DI Container Layer                        │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐  │
│  │    Scope    │◀──▶│  Container  │◀──▶│Descriptor   │  │
│  │  Manager    │    │   Core      │    │   Store     │  │
│  └─────────────┘    └─────────────┘    └─────────────┘  │
└─────────────────────────────┬─────────────────────────────┘
                              │
┌─────────────────────────────┼─────────────────────────────┐
│                 Configuration Layer                        │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐  │
│  │ Installer   │───▶│   Config    │───▶│   Asset     │  │
│  │   System    │    │  Loader     │    │   Files     │  │
│  └─────────────┘    └─────────────┘    └─────────────┘  │
└─────────────────────────────────────────────────────────┘
```

### 核心类设计

#### 1. DIContainer - 核心容器
```csharp
public partial class DIContainer : IServiceProvider, IDisposable
{
    // 服务描述符存储
    private readonly ConcurrentDictionary<Type, List<ServiceDescriptor>> _serviceDescriptors;
    
    // 实例缓存
    private readonly ConcurrentDictionary<int, object> _singletonInstances;
    private readonly ConcurrentBag<IDisposable> _disposables;
    
    // 依赖解析状态
    private readonly ThreadLocal<Stack<Type>> _resolveStack;
    
    // 构造函数缓存
    private readonly ConcurrentDictionary<Type, ConstructorInfo> _constructorsCache;
    
    // 父容器引用（支持容器层次结构）
    private DIContainer _parentContainer;
}
```

#### 2. ServiceDescriptor - 服务描述符
```csharp
public class ServiceDescriptor
{
    public Type ServiceType { get; }
    public int Id { get; }  // 全局唯一ID
    public ServiceLifetime Lifetime { get; }
    public int Order { get; }  // 注册顺序，用于多实现选择
    
    // 三种实现方式
    public Type ImplementationType { get; }
    public object ImplementationInstance { get; }
    public Func<IServiceProvider, object> ImplementationFactory { get; }
    
    // 构造方法
    public ServiceDescriptor(int id, Type serviceType, Type implementationType, 
                           ServiceLifetime lifetime, int order = 0);
    public ServiceDescriptor(int id, Type serviceType, object implementationInstance, 
                           int order = 0);
    public ServiceDescriptor(int id, Type serviceType, 
                           Func<IServiceProvider, object> implementationFactory, 
                           ServiceLifetime lifetime, int order = 0);
}
```

#### 3. Scope - 作用域实现
```csharp
private class Scope : IScope
{
    public DIContainer Container { get; }
    public ConcurrentDictionary<int, object> ScopedInstances { get; }
    public ConcurrentBag<IDisposable> Disposables { get; }
    public IServiceProvider ServiceProvider { get; }
    
    // 资源释放
    public void Dispose()
    {
        foreach (var disposable in Disposables)
            disposable.Dispose();
        Disposables.Clear();
        ScopedInstances.Clear();
    }
}
```

## 生命周期管理

### 1. Singleton（单例）
**特性**：
- 全局唯一实例
- 首次解析时创建，后续复用
- 生命周期与容器相同

**实现**：
```csharp
private object ResolveSingleton(ServiceDescriptor descriptor)
{
    if (_singletonInstances.TryGetValue(descriptor.Id, out var instance))
        return instance;
    
    lock (_singletonInstances)
    {
        if (_singletonInstances.TryGetValue(descriptor.Id, out instance))
            return instance;
            
        instance = CreateInstance(descriptor, null);
        _singletonInstances[descriptor.Id] = instance;
        
        if (instance is IDisposable disposable)
            _disposables.Add(disposable);
            
        return instance;
    }
}
```

**使用场景**：
- 全局管理器（EventCenter、InputManager）
- 配置服务
- 共享资源访问器

### 2. Scoped（作用域）
**特性**：
- 每个作用域内唯一
- 作用域销毁时实例释放
- 适合场景级对象

**实现**：
```csharp
private object ResolveScope(ServiceDescriptor descriptor, Scope scope)
{
    if (scope == null) 
        throw new InvalidOperationException("Scoped service requires a scope.");
    
    if (scope.ScopedInstances.TryGetValue(descriptor.Id, out var instance))
        return instance;
        
    instance = CreateInstance(descriptor, scope);
    scope.ScopedInstances[descriptor.Id] = instance;
    
    if (instance is IDisposable disposable)
        scope.Disposables.Add(disposable);
        
    return instance;
}
```

**使用场景**：
- 场景特定的控制器
- UI视图模型
- 临时数据服务

### 3. Transient（瞬时）
**特性**：
- 每次解析创建新实例
- 由容器或作用域管理释放
- 适合无状态服务

**实现**：
```csharp
private object ResolveTransient(ServiceDescriptor descriptor, Scope scope)
{
    var instance = CreateInstance(descriptor, scope);
    
    if (instance is IDisposable disposable)
    {
        if (scope != null)
            scope.Disposables.Add(disposable);
        else
            _disposables.Add(disposable);
    }
    
    return instance;
}
```

**使用场景**：
- 值转换器
- 临时计算服务
- 工厂创建的对象

## 注入策略

### 1. 构造函数注入（已实现）
**优先级规则**：
1. 标记`[Inject]`的构造函数
2. 参数最多的构造函数
3. 无参构造函数

**实现**：
```csharp
private ConstructorInfo GetConstructor(Type type)
{
    return _constructorsCache.GetOrAdd(type, t =>
    {
        var constructors = t.GetConstructors();
        
        // 优先选择标记[Inject]的构造函数
        var injectConstructor = constructors.FirstOrDefault(c =>
            c.GetCustomAttributes(typeof(InjectAttribute), false).Length > 0);
            
        if (injectConstructor != null)
            return injectConstructor;
            
        // 否则选择参数最多的构造函数
        return constructors.OrderByDescending(c => c.GetParameters().Length).First();
    });
}
```

### 2. 字段/属性注入（计划实现）
**设计**：
```csharp
public void Inject(object instance, IScope scope = null)
{
    var type = instance.GetType();
    
    // 字段注入
    var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(f => f.GetCustomAttributes(typeof(InjectAttribute), false).Length > 0);
    
    foreach (var field in fields)
    {
        var attribute = field.GetCustomAttribute<InjectAttribute>();
        var service = ResolveService(field.FieldType, scope as Scope);
        
        if (service == null && !attribute.Optional)
            throw new InvalidOperationException($"Required dependency not found: {field.FieldType}");
            
        field.SetValue(instance, service);
    }
    
    // 属性注入（类似逻辑）
    var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(p => p.GetCustomAttributes(typeof(InjectAttribute), false).Length > 0
                 && p.CanWrite);
    
    foreach (var property in properties)
    {
        var attribute = property.GetCustomAttribute<InjectAttribute>();
        var service = ResolveService(property.PropertyType, scope as Scope);
        
        if (service == null && !attribute.Optional)
            throw new InvalidOperationException($"Required dependency not found: {property.PropertyType}");
            
        property.SetValue(instance, service);
    }
}
```

**使用场景**：
- MonoBehaviour组件（无法使用构造函数注入）
- ScriptableObject配置
- 已有对象的依赖注入

### 3. 方法注入（可选扩展）
**设计思路**：
```csharp
[AttributeUsage(AttributeTargets.Method)]
public class InjectMethodAttribute : Attribute { }

public class SomeClass
{
    [InjectMethod]
    public void Initialize(IService service, IOtherService other)
    {
        // 依赖通过方法参数注入
    }
}
```

## 注册系统

### 1. 基础注册API
```csharp
public class DIContainer
{
    // 类型注册
    public void RegisterTransient<TService, TImplementation>() 
        where TImplementation : TService;
    
    public void RegisterSingleton<TService, TImplementation>() 
        where TImplementation : TService;
    
    public void RegisterScoped<TService, TImplementation>() 
        where TImplementation : TService;
    
    // 实例注册
    public void RegisterSingleton<TService>(TService implementationInstance) 
        where TService : class;
    
    // 工厂注册
    public void RegisterTransient<TService>(Func<IServiceProvider, object> implementationFactory) 
        where TService : class;
    
    public void RegisterSingleton<TService>(Func<IServiceProvider, object> implementationFactory) 
        where TService : class;
    
    public void RegisterScoped<TService>(Func<IServiceProvider, object> implementationFactory) 
        where TService : class;
}
```

### 2. Installer系统
**IInstaller接口**：
```csharp
public interface IInstaller
{
    void Register(DIContainer container);
}
```

**InstallerAsset基类**：
```csharp
public class InstallerAsset : ScriptableObject, IInstaller
{
    public int order = 0;
    
    public virtual void Register(DIContainer container)
    {
        Debug.Log($"Installer {name} not overridden");
    }
}
```

**配置驱动示例**：
```csharp
[CreateAssetMenu(fileName = "CoreInstaller", menuName = "Boot/CoreInstaller")]
public class CoreInstaller : InstallerAsset
{
    public override void Register(DIContainer container)
    {
        container.RegisterSingleton<IEventCenter>(new EventManager());
        container.RegisterSingleton<IPlayerInput>(sp =>
        {
            var go = new GameObject("PlayerInputManager");
            Object.DontDestroyOnLoad(go);
            var mgr = go.AddComponent<PlayerInputManager>();
            mgr.Initialize();
            return mgr;
        });
    }
}
```

### 3. 配置管理
**InstallerConfig**：
```csharp
[CreateAssetMenu(fileName = "BootConfig", menuName = "Boot/InstallerConfig")]
public class InstallerConfig : ScriptableObject
{
    public List<InstallerAsset> globalInstallers = new();
    public List<InstallerAsset> sceneInstallers = new();
    
    public IEnumerable<InstallerAsset> GlobalInstallersSorted =>
        globalInstallers.OrderBy(i => i.order);
    
    public IEnumerable<InstallerAsset> SceneInstallersSorted =>
        sceneInstallers.OrderBy(i => i.order);
}
```

**资源加载**：
```csharp
private InstallerConfig LoadInstallerConfig()
{
    // 从Resources加载配置
    return Resources.Load<InstallerConfig>("Configs/BootConfig");
}
```

## 解析策略

### 1. 单服务解析
```csharp
public object GetService(Type serviceType)
{
    return ResolveService(serviceType, null);
}

public T GetService<T>()
{
    return (T)ResolveService(typeof(T), null);
}

public object GetRequiredService(Type serviceType, IScope scope = null)
{
    var result = ResolveService(serviceType, scope as Scope);
    if (result == null)
        throw new InvalidOperationException($"Service not registered: {serviceType}");
    return result;
}
```

### 2. 多实现解析
```csharp
public IEnumerable<T> ResolveAll<T>(IScope scope = null)
{
    var s = scope as Scope;
    return ResolveAll(typeof(T), s).Cast<T>();
}

private IEnumerable<object> ResolveAll(Type type, Scope scope)
{
    var results = new List<object>();
    
    if (_serviceDescriptors.TryGetValue(type, out var descriptors))
    {
        // 按Order排序
        var ordered = descriptors.OrderBy(d => d.Order).ToArray();
        foreach (var descriptor in ordered)
        {
            var obj = ResolveService(descriptor.ServiceType, scope);
            if (obj != null) results.Add(obj);
        }
    }
    
    // 递归父容器
    if (_parentContainer != null)
        results.AddRange(_parentContainer.ResolveAll(type, scope));
        
    return results;
}
```

### 3. 集合类型支持
```csharp
private bool TryResolveEnumerable(Type paramType, Scope scope, out object value)
{
    value = null;
    
    // IEnumerable<T>
    if (paramType.IsGenericType && 
        paramType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
    {
        var elemType = paramType.GetGenericArguments()[0];
        value = BuildList(elemType, ResolveAll(elemType, scope));
        return true;
    }
    
    // List<T>
    if (paramType.IsGenericType && 
        paramType.GetGenericTypeDefinition() == typeof(List<>))
    {
        var elemType = paramType.GetGenericArguments()[0];
        value = BuildList(elemType, ResolveAll(elemType, scope));
        return true;
    }
    
    // T[]
    if (paramType.IsArray)
    {
        var elemType = paramType.GetElementType();
        var items = ResolveAll(elemType, scope);
        value = BuildArray(elemType, items);
        return true;
    }
    
    return false;
}
```

## 启动流程设计

### 1. 全局启动（ProjectContext）
```csharp
public class ProjectContext : MonoBehaviour
{
    private static ProjectContext _instance;
    private DIContainer _globalContainer;
    private IScope _projectScope;
    
    public static void Ensure()
    {
        if (_instance != null) return;
        
        var go = new GameObject("ProjectContext");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ProjectContext>();
        _instance.Boot();
    }
    
    private void Boot()
    {
        _globalContainer = new DIContainer();
        var config = LoadInstallerConfig();
        
        // 阶段1：注册
        InstallGlobal(config, _globalContainer);
        
        // 阶段2：创建作用域
        _projectScope = _globalContainer.CreateScope();
        
        // 阶段3：初始化
        Initialize(_globalContainer);
        
        // 阶段4：启动
        StartAll(_globalContainer);
        
        // 阶段5：挂载场景运行器
        SetupSceneScopeRunner();
    }
}
```

### 2. 场景启动（SceneScopeRunner）
```csharp
public class SceneScopeRunner : MonoBehaviour
{
    [Inject] private DIContainer _globalContainer;
    private IScope _currentSceneScope;
    
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 创建场景作用域
        _currentSceneScope = _globalContainer.CreateScope();
        
        // 加载场景配置
        var config = LoadSceneInstallerConfig(scene);
        
        // 注册场景服务
        InstallScene(config, _currentSceneScope);
        
        // 注入场景对象
        InjectSceneObjects(_currentSceneScope);
        
        // 初始化场景服务
        InitializeScene(_currentSceneScope);
        
        // 启动场景服务
        StartScene(_currentSceneScope);
    }
    
    private void OnSceneUnloaded(Scene scene)
    {
        _currentSceneScope?.Dispose();
        _currentSceneScope = null;
    }
}
```

## 错误处理与调试

### 1. 循环依赖检测
```csharp
private object ResolveService(Type serviceType, Scope scope)
{
    if (_resolveStack.Value!.Contains(serviceType))
    {
        var stackTrace = string.Join(" -> ", _resolveStack.Value.Reverse());
        throw new InvalidOperationException(
            $"Circular dependency detected: {stackTrace} -> {serviceType}");
    }
    
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
```

### 2. 依赖缺失处理
```csharp
private object CreateInstance(ServiceDescriptor descriptor, Scope scope)
{
    // ... 参数解析
    for (int i = 0; i < parameters.Length; i++)
    {
        var paramType = parameters[i].ParameterType;
        object val = null;
        
        // 尝试解析集合类型
        if (TryResolveEnumerable(paramType, scope, out var enumerableValue))
            val = enumerableValue;
        else
            val = ResolveService(paramType, scope);
        
        // 处理默认值
        if (val == null && parameters[i].HasDefaultValue)
            val = parameters[i].DefaultValue;
            
        if (val == null)
        {
            throw new InvalidOperationException(
                $"Cannot resolve dependency '{paramType}' for service '{descriptor.ServiceType}'. " +
                $"Parameter: {parameters[i].Name} in constructor.");
        }
        
        paramValues[i] = val;
    }
    
    return ctor.Invoke(paramValues);
}
```

### 3. 调试信息
```csharp
public string GetDebugInfo()
{
    var sb = new StringBuilder();
    sb.AppendLine("=== DI Container Debug Info ===");
    sb.AppendLine($"Total Services: {_serviceDescriptors.Sum(p => p.Value.Count)}");
    sb.AppendLine($"Singleton Instances: {_singletonInstances.Count}");
    sb.AppendLine($"Disposable Services: {_disposables.Count}");
    
    sb.AppendLine("\n=== Service Registry ===");
    foreach (var kvp in _serviceDescriptors.OrderBy(k => k.Key.Name))
    {
        sb.AppendLine($"Service: {kvp.Key.Name}");
        foreach (var desc in kvp.Value.OrderBy(d => d.Order))
        {
            sb.AppendLine($"  - {desc.Lifetime} (Order: {desc.Order})");
        }
    }
    
    return sb.ToString();
}
```

## 性能优化

### 1. 缓存策略
- **构造函数缓存**：避免重复反射
- **类型查询结果缓存**：缓存GetCustomAttributes结果
- **解析路径缓存**：缓存常用服务的解析路径

### 2. 延迟初始化
```csharp
private Lazy<ConcurrentDictionary<Type, ConstructorInfo>> _lazyConstructorsCache = 
    new Lazy<ConcurrentDictionary<Type, ConstructorInfo>>(
        () => new ConcurrentDictionary<Type, ConstructorInfo>());
```

### 3. 线程安全设计
- **ConcurrentDictionary**：线程安全的字典实现
- **双重检查锁定**：Singleton实例创建
- **ThreadLocal**：线程特定的解析栈

## 扩展机制

### 1. 自定义生命周期
```csharp
public interface IServiceLifetime
{
    object GetInstance(ServiceDescriptor descriptor, DIContainer container, Scope scope);
    void ReleaseInstance(object instance);
}

public class PooledLifetime : IServiceLifetime
{
    private readonly ObjectPool<object> _pool;
    
    public object GetInstance(ServiceDescriptor descriptor, DIContainer container, Scope scope)
    {
        return _pool.Get();
    }
    
    public void ReleaseInstance(object instance)
    {
        _pool.Return(instance);
    }
}
```

### 2. 拦截器支持
```csharp
public interface IInterceptor
{
    object Intercept(ServiceDescriptor descriptor, Func<object> createInstance);
}

public class LoggingInterceptor : IInterceptor
{
    public object Intercept(ServiceDescriptor descriptor, Func<object> createInstance)
    {
        Debug.Log($"Creating instance of {descriptor.ServiceType.Name}");
        var instance = createInstance();
        Debug.Log($"Instance created: {instance.GetType().Name}");
        return instance;
    }
}
```

### 3. 条件注册
```csharp
public class ConditionalInstaller : InstallerAsset
{
    public override void Register(DIContainer container)
    {
        #if UNITY_EDITOR
        container.RegisterSingleton<IDebugService, EditorDebugService>();
        #else
        container.RegisterSingleton<IDebugService, ReleaseDebugService>();
        #endif
    }
}
```

## 使用指南

### 1. 基础使用
```csharp
// 注册服务
container.RegisterSingleton<IEventCenter, EventManager>();
container.RegisterScoped<IPlayerService, PlayerService>();

// 解析服务
var eventCenter = container.GetService<IEventCenter>();
var playerService = container.GetRequiredService<IPlayerService>();

// 使用作用域
using (var scope = container.CreateScope())
{
    var scopedService = container.GetRequiredService<IScopedService>(scope);
    // 使用作用域内服务
}
```

### 2. 在MonoBehaviour中使用
```csharp
public class PlayerController : MonoBehaviour
{
    [Inject] private IEventCenter _eventCenter;
    [Inject] private IPlayerService _playerService;
    
    private void Awake()
    {
        // 在SceneScopeRunner中会自动注入
    }
}
```

### 3. 编写自定义Installer
```csharp
[CreateAssetMenu(fileName = "MyInstaller", menuName = "Boot/MyInstaller")]
public class MyInstaller : InstallerAsset
{
    public GameObject playerPrefab;
    
    public override void Register(DIContainer container)
    {
        container.RegisterSingleton<IPlayerFactory>(sp =>
        {
            return new PlayerFactory(playerPrefab);
        });
    }
}
```

## 已知问题与改进计划

### 当前问题
1. **字段注入未实现**：目前仅支持构造函数注入
2. **资源路径问题**：InstallerConfig加载路径需要修正
3. **SceneScopeRunner未挂载**：场景作用域流程未激活
4. **错误信息不够详细**：依赖解析失败时信息不够明确

### 改进计划
1. **实现字段/属性注入**：支持MonoBehaviour依赖注入
2. **增强调试工具**：可视化依赖图和服务状态
3. **性能优化**：添加更多缓存和优化策略
4. **扩展生命周期**：支持自定义生命周期管理

## 总结

本DI系统设计提供了一个**完整**、**可扩展**、**高性能**的依赖注入解决方案，专门为Unity游戏开发优化。通过清晰的**生命周期管理**、灵活的**注册系统**和强大的**解析策略**，为项目提供了坚实的依赖管理基础。

系统的**可配置性**（ScriptableObject驱动）和**可调试性**（详细错误信息和调试工具）使其非常适合中大型项目的开发，同时保持足够的灵活性支持各种使用场景。