# 可扩展性设计文档

## 设计目标

### 核心目标
1. **模块化架构**：支持功能模块的独立开发、测试和部署
2. **插件化扩展**：支持第三方扩展和自定义模块
3. **配置驱动**：通过配置文件调整系统行为，无需代码修改
4. **API稳定性**：保持公共API的向后兼容性
5. **渐进式增强**：支持从简单到复杂的功能演进路径

### 技术挑战
1. **架构演进兼容性**：确保新功能不破坏现有系统
2. **依赖管理**：模块间依赖关系的动态管理
3. **性能与扩展性平衡**：扩展性设计不牺牲系统性能
4. **配置复杂性管理**：避免过度配置导致的复杂性爆炸
5. **第三方集成**：外部模块的安全和稳定集成

## 扩展性架构设计

### 分层扩展架构
```
┌─────────────────────────────────────────────────────────┐
│                 扩展接口层 (Extension API)              │
│  ├─ IExtension (扩展接口)                              │
│  ├─ IExtensionContext (扩展上下文)                    │
│  └─ IExtensionManager (扩展管理器)                    │
└─────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────┐
│                 扩展注册层 (Registry)                   │
│  ├─ ExtensionRegistry (扩展注册表)                     │
│  ├─ FeatureRegistry (功能注册表)                       │
│  └─ ServiceRegistry (服务注册表)                       │
└─────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────┐
│                 扩展发现层 (Discovery)                  │
│  ├─ AssemblyScanner (程序集扫描器)                     │
│  ├─ AttributeScanner (特性扫描器)                      │
│  └─ ConfigurationScanner (配置扫描器)                  │
└─────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────┐
│                 核心服务层 (Core Services)              │
│  ├─ DI Container (依赖注入容器)                        │
│  ├─ Event System (事件系统)                            │
│  └─ Configuration System (配置系统)                    │
└─────────────────────────────────────────────────────────┘
```

### 扩展点设计
1. **服务扩展点**：通过DI容器注册新服务或替换现有服务
2. **事件扩展点**：通过事件系统添加新的事件处理器
3. **配置扩展点**：通过配置文件添加新功能配置
4. **UI扩展点**：通过View系统添加新的UI组件
5. **数据模型扩展点**：通过EntityModel系统添加新的数据模型

## 插件系统设计

### 插件接口定义
```csharp
public interface IPlugin : IDisposable
{
    /// <summary>
    /// 插件唯一标识符
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// 插件名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 插件版本
    /// </summary>
    Version Version { get; }
    
    /// <summary>
    /// 插件描述
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// 插件依赖的其他插件
    /// </summary>
    IReadOnlyList<PluginDependency> Dependencies { get; }
    
    /// <summary>
    /// 初始化插件
    /// </summary>
    void Initialize(IPluginContext context);
    
    /// <summary>
    /// 启动插件
    /// </summary>
    void Start();
    
    /// <summary>
    /// 停止插件
    /// </summary>
    void Stop();
    
    /// <summary>
    /// 获取插件配置
    /// </summary>
    IPluginConfiguration GetConfiguration();
}

public interface IPluginContext
{
    /// <summary>
    /// DI容器
    /// </summary>
    IDIContainer Container { get; }
    
    /// <summary>
    /// 事件中心
    /// </summary>
    IEventCenter EventCenter { get; }
    
    /// <summary>
    /// 配置管理器
    /// </summary>
    IConfigurationManager Configuration { get; }
    
    /// <summary>
    /// 日志记录器
    /// </summary>
    ILogger Logger { get; }
    
    /// <summary>
    /// 注册服务
    /// </summary>
    void RegisterService<TService, TImplementation>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TService : class
        where TImplementation : class, TService;
    
    /// <summary>
    /// 订阅事件
    /// </summary>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler, EventPriority priority = EventPriority.Normal)
        where TEvent : IEvent;
}
```

### 插件管理器
```csharp
public class PluginManager : IPluginManager
{
    private readonly Dictionary<string, IPlugin> _plugins = new();
    private readonly Dictionary<string, PluginState> _pluginStates = new();
    private readonly IPluginLoader _pluginLoader;
    private readonly IDependencyResolver _dependencyResolver;
    private readonly ILogger<PluginManager> _logger;
    
    public PluginManager(
        IPluginLoader pluginLoader,
        IDependencyResolver dependencyResolver,
        ILogger<PluginManager> logger)
    {
        _pluginLoader = pluginLoader;
        _dependencyResolver = dependencyResolver;
        _logger = logger;
    }
    
    public async Task LoadPluginsAsync(string pluginsDirectory, CancellationToken cancellationToken = default)
    {
        var pluginDescriptors = await _pluginLoader.DiscoverPluginsAsync(pluginsDirectory, cancellationToken);
        
        // 解决依赖关系
        var loadOrder = _dependencyResolver.ResolveLoadOrder(pluginDescriptors);
        
        foreach (var descriptor in loadOrder)
        {
            try
            {
                await LoadPluginAsync(descriptor, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"加载插件失败: {descriptor.Id}");
            }
        }
    }
    
    private async Task LoadPluginAsync(PluginDescriptor descriptor, CancellationToken cancellationToken)
    {
        var plugin = _pluginLoader.LoadPlugin(descriptor);
        
        // 创建插件上下文
        var context = new PluginContext(this);
        
        // 初始化插件
        plugin.Initialize(context);
        
        _plugins[descriptor.Id] = plugin;
        _pluginStates[descriptor.Id] = PluginState.Loaded;
        
        _logger.LogInformation($"插件加载成功: {plugin.Name} v{plugin.Version}");
    }
    
    public void StartAllPlugins()
    {
        var startOrder = _dependencyResolver.ResolveStartOrder(_plugins.Values);
        
        foreach (var plugin in startOrder)
        {
            try
            {
                plugin.Start();
                _pluginStates[plugin.Id] = PluginState.Started;
                _logger.LogInformation($"插件启动成功: {plugin.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"插件启动失败: {plugin.Name}");
                _pluginStates[plugin.Id] = PluginState.Error;
            }
        }
    }
    
    public IPlugin GetPlugin(string pluginId)
    {
        return _plugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
    }
    
    public IReadOnlyList<IPlugin> GetPlugins()
    {
        return _plugins.Values.ToList();
    }
}
```

### 插件加载器
```csharp
public class AssemblyPluginLoader : IPluginLoader
{
    private readonly IAssemblyLoader _assemblyLoader;
    private readonly ILogger<AssemblyPluginLoader> _logger;
    
    public AssemblyPluginLoader(IAssemblyLoader assemblyLoader, ILogger<AssemblyPluginLoader> logger)
    {
        _assemblyLoader = assemblyLoader;
        _logger = logger;
    }
    
    public async Task<IReadOnlyList<PluginDescriptor>> DiscoverPluginsAsync(
        string directory, 
        CancellationToken cancellationToken = default)
    {
        var descriptors = new List<PluginDescriptor>();
        var assemblyFiles = Directory.GetFiles(directory, "*.dll");
        
        foreach (var assemblyFile in assemblyFiles)
        {
            try
            {
                var descriptor = await DiscoverPluginAsync(assemblyFile, cancellationToken);
                if (descriptor != null)
                    descriptors.Add(descriptor);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"扫描插件文件失败: {assemblyFile}");
            }
        }
        
        return descriptors;
    }
    
    private async Task<PluginDescriptor> DiscoverPluginAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        var assembly = _assemblyLoader.LoadAssembly(assemblyPath);
        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .ToList();
        
        if (pluginTypes.Count == 0)
            return null;
            
        if (pluginTypes.Count > 1)
            throw new InvalidOperationException($"程序集 {assemblyPath} 包含多个插件类型");
            
        var pluginType = pluginTypes[0];
        
        // 从特性获取插件信息
        var pluginAttribute = pluginType.GetCustomAttribute<PluginAttribute>();
        if (pluginAttribute == null)
            throw new InvalidOperationException($"插件类型 {pluginType} 缺少 PluginAttribute");
            
        return new PluginDescriptor
        {
            Id = pluginAttribute.Id,
            Name = pluginAttribute.Name,
            Version = pluginAttribute.Version,
            Description = pluginAttribute.Description,
            AssemblyPath = assemblyPath,
            PluginType = pluginType,
            Dependencies = GetDependencies(pluginType)
        };
    }
    
    public IPlugin LoadPlugin(PluginDescriptor descriptor)
    {
        var assembly = _assemblyLoader.LoadAssembly(descriptor.AssemblyPath);
        var plugin = (IPlugin)Activator.CreateInstance(descriptor.PluginType);
        return plugin;
    }
}
```

## 配置系统扩展

### 可扩展配置系统
```csharp
public class ExtensibleConfiguration : IConfiguration
{
    private readonly Dictionary<string, IConfigurationSection> _sections = new();
    private readonly List<IConfigurationProvider> _providers = new();
    private readonly object _lock = new();
    
    public ExtensibleConfiguration()
    {
        // 添加默认配置提供者
        AddProvider(new JsonConfigurationProvider());
        AddProvider(new EnvironmentConfigurationProvider());
        AddProvider(new CommandLineConfigurationProvider());
    }
    
    public void AddProvider(IConfigurationProvider provider)
    {
        lock (_lock)
        {
            _providers.Add(provider);
            Reload();
        }
    }
    
    public void Reload()
    {
        lock (_lock)
        {
            _sections.Clear();
            
            // 从所有提供者加载配置
            foreach (var provider in _providers)
            {
                var sections = provider.Load();
                foreach (var section in sections)
                {
                    if (!_sections.TryGetValue(section.Key, out var existing))
                    {
                        _sections[section.Key] = section;
                    }
                    else
                    {
                        // 合并配置
                        existing.Merge(section);
                    }
                }
            }
        }
    }
    
    public IConfigurationSection GetSection(string key)
    {
        lock (_lock)
        {
            return _sections.TryGetValue(key, out var section) ? section : null;
        }
    }
    
    public T GetValue<T>(string key, T defaultValue = default)
    {
        var section = GetSection(key);
        if (section == null)
            return defaultValue;
            
        try
        {
            return section.GetValue<T>();
        }
        catch
        {
            return defaultValue;
        }
    }
    
    public void SetValue<T>(string key, T value)
    {
        lock (_lock)
        {
            if (!_sections.TryGetValue(key, out var section))
            {
                section = new ConfigurationSection(key);
                _sections[key] = section;
            }
            
            section.SetValue(value);
            
            // 保存到所有提供者
            foreach (var provider in _providers.OfType<IPersistableConfigurationProvider>())
            {
                provider.Save(key, value);
            }
        }
    }
}
```

### 动态配置提供者
```csharp
public class RuntimeConfigurationProvider : IConfigurationProvider, IPersistableConfigurationProvider
{
    private readonly ConcurrentDictionary<string, object> _values = new();
    private readonly string _storagePath;
    
    public RuntimeConfigurationProvider(string storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(Application.persistentDataPath, "runtime-config.json");
        LoadFromStorage();
    }
    
    public IEnumerable<IConfigurationSection> Load()
    {
        return _values.Select(kvp => new ConfigurationSection(kvp.Key, kvp.Value));
    }
    
    public void Save(string key, object value)
    {
        _values[key] = value;
        SaveToStorage();
    }
    
    public void SaveAll()
    {
        SaveToStorage();
    }
    
    private void LoadFromStorage()
    {
        if (!File.Exists(_storagePath))
            return;
            
        try
        {
            var json = File.ReadAllText(_storagePath);
            var data = JsonUtility.FromJson<Dictionary<string, object>>(json);
            
            foreach (var kvp in data)
            {
                _values[kvp.Key] = kvp.Value;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"加载运行时配置失败: {ex.Message}");
        }
    }
    
    private void SaveToStorage()
    {
        try
        {
            var data = _values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(_storagePath, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"保存运行时配置失败: {ex.Message}");
        }
    }
}
```

## 服务扩展系统

### 服务注册扩展
```csharp
public class ServiceRegistry : IServiceRegistry
{
    private readonly Dictionary<Type, ServiceRegistration> _registrations = new();
    private readonly List<IServiceRegistrationFilter> _filters = new();
    private readonly object _lock = new();
    
    public void Register<TService, TImplementation>(
        ServiceLifetime lifetime = ServiceLifetime.Singleton,
        string name = null)
        where TService : class
        where TImplementation : class, TService
    {
        lock (_lock)
        {
            var registration = new ServiceRegistration
            {
                ServiceType = typeof(TService),
                ImplementationType = typeof(TImplementation),
                Lifetime = lifetime,
                Name = name
            };
            
            // 应用过滤器
            foreach (var filter in _filters)
            {
                registration = filter.Filter(registration);
                if (registration == null)
                    return; // 过滤器拒绝了注册
            }
            
            _registrations[typeof(TService)] = registration;
        }
    }
    
    public void RegisterFactory<TService>(
        Func<IServiceProvider, TService> factory,
        ServiceLifetime lifetime = ServiceLifetime.Singleton,
        string name = null)
        where TService : class
    {
        lock (_lock)
        {
            var registration = new ServiceRegistration
            {
                ServiceType = typeof(TService),
                Factory = provider => factory(provider),
                Lifetime = lifetime,
                Name = name
            };
            
            _registrations[typeof(TService)] = registration;
        }
    }
    
    public void AddFilter(IServiceRegistrationFilter filter)
    {
        lock (_lock)
        {
            _filters.Add(filter);
        }
    }
    
    public void RemoveFilter(IServiceRegistrationFilter filter)
    {
        lock (_lock)
        {
            _filters.Remove(filter);
        }
    }
    
    public bool TryGetRegistration(Type serviceType, out ServiceRegistration registration)
    {
        lock (_lock)
        {
            return _registrations.TryGetValue(serviceType, out registration);
        }
    }
    
    public IReadOnlyList<ServiceRegistration> GetAllRegistrations()
    {
        lock (_lock)
        {
            return _registrations.Values.ToList();
        }
    }
}
```

### 服务发现与注册
```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class ServiceAttribute : Attribute
{
    public Type ServiceType { get; }
    public ServiceLifetime Lifetime { get; }
    public string Name { get; set; }
    
    public ServiceAttribute(Type serviceType, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ServiceType = serviceType;
        Lifetime = lifetime;
    }
}

public class ServiceDiscovery
{
    private readonly IAssemblyScanner _assemblyScanner;
    private readonly IServiceRegistry _serviceRegistry;
    
    public ServiceDiscovery(IAssemblyScanner assemblyScanner, IServiceRegistry serviceRegistry)
    {
        _assemblyScanner = assemblyScanner;
        _serviceRegistry = serviceRegistry;
    }
    
    public void DiscoverAndRegisterServices(IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            DiscoverServicesInAssembly(assembly);
        }
    }
    
    private void DiscoverServicesInAssembly(Assembly assembly)
    {
        var serviceTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetCustomAttributes<ServiceAttribute>().Any())
            .ToList();
        
        foreach (var implementationType in serviceTypes)
        {
            var attributes = implementationType.GetCustomAttributes<ServiceAttribute>();
            foreach (var attribute in attributes)
            {
                RegisterService(implementationType, attribute);
            }
        }
    }
    
    private void RegisterService(Type implementationType, ServiceAttribute attribute)
    {
        if (!attribute.ServiceType.IsAssignableFrom(implementationType))
            throw new InvalidOperationException(
                $"{implementationType} 不能赋值给 {attribute.ServiceType}");
        
        if (attribute.ServiceType.IsGenericTypeDefinition)
        {
            // 注册泛型服务
            RegisterGenericService(implementationType, attribute);
        }
        else
        {
            // 注册具体服务
            _serviceRegistry.Register(
                attribute.ServiceType,
                implementationType,
                attribute.Lifetime,
                attribute.Name);
        }
    }
}
```

## 事件系统扩展

### 可扩展事件管道
```csharp
public class ExtensibleEventPipeline
{
    private readonly List<IEventMiddleware> _middlewares = new();
    private readonly Dictionary<Type, EventPipeline> _pipelines = new();
    private readonly object _lock = new();
    
    public void UseMiddleware<TMiddleware>() where TMiddleware : IEventMiddleware
    {
        lock (_lock)
        {
            var middleware = Activator.CreateInstance<TMiddleware>();
            _middlewares.Add(middleware);
            
            // 重建所有管道
            RebuildAllPipelines();
        }
    }
    
    public void UseMiddleware(IEventMiddleware middleware)
    {
        lock (_lock)
        {
            _middlewares.Add(middleware);
            RebuildAllPipelines();
        }
    }
    
    public EventPipeline GetPipeline<TEvent>() where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        
        lock (_lock)
        {
            if (!_pipelines.TryGetValue(eventType, out var pipeline))
            {
                pipeline = BuildPipeline(eventType);
                _pipelines[eventType] = pipeline;
            }
            
            return pipeline;
        }
    }
    
    private EventPipeline BuildPipeline(Type eventType)
    {
        // 创建基础处理器
        EventHandlerDelegate pipeline = (e, c) => Task.CompletedTask;
        
        // 添加特定于事件类型的中间件
        var eventSpecificMiddlewares = GetEventSpecificMiddlewares(eventType);
        var allMiddlewares = _middlewares.Concat(eventSpecificMiddlewares);
        
        // 反向构建管道
        foreach (var middleware in allMiddlewares.Reverse())
        {
            var next = pipeline;
            pipeline = (e, c) => middleware.HandleAsync(e, c, next);
        }
        
        return new EventPipeline(pipeline);
    }
    
    private IEnumerable<IEventMiddleware> GetEventSpecificMiddlewares(Type eventType)
    {
        // 从特性获取事件特定的中间件
        var middlewareAttributes = eventType.GetCustomAttributes<EventMiddlewareAttribute>();
        foreach (var attribute in middlewareAttributes)
        {
            yield return (IEventMiddleware)Activator.CreateInstance(attribute.MiddlewareType);
        }
    }
    
    private void RebuildAllPipelines()
    {
        foreach (var eventType in _pipelines.Keys.ToList())
        {
            _pipelines[eventType] = BuildPipeline(eventType);
        }
    }
}
```

### 动态事件处理器注册
```csharp
public class DynamicEventHandlerRegistry : IEventHandlerRegistry
{
    private readonly Dictionary<Type, List<EventHandlerWrapper>> _handlers = new();
    private readonly object _lock = new();
    
    public IDisposable RegisterHandler<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : IEvent
    {
        var wrapper = new EventHandlerWrapper<TEvent>(handler);
        
        lock (_lock)
        {
            var eventType = typeof(TEvent);
            if (!_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<EventHandlerWrapper>();
                _handlers[eventType] = handlers;
            }
            
            handlers.Add(wrapper);
        }
        
        return new EventHandlerRegistration(() => UnregisterHandler(wrapper));
    }
    
    public IDisposable RegisterHandler<TEvent>(Action<TEvent> handler, EventPriority priority = EventPriority.Normal)
        where TEvent : IEvent
    {
        var wrapper = new ActionEventHandlerWrapper<TEvent>(handler, priority);
        
        lock (_lock)
        {
            var eventType = typeof(TEvent);
            if (!_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<EventHandlerWrapper>();
                _handlers[eventType] = handlers;
            }
            
            handlers.Add(wrapper);
        }
        
        return new EventHandlerRegistration(() => UnregisterHandler(wrapper));
    }
    
    public IReadOnlyList<EventHandlerWrapper> GetHandlers(Type eventType)
    {
        lock (_lock)
        {
            return _handlers.TryGetValue(eventType, out var handlers) 
                ? handlers.ToList() 
                : new List<EventHandlerWrapper>();
        }
    }
    
    private void UnregisterHandler(EventHandlerWrapper wrapper)
    {
        lock (_lock)
        {
            var eventType = wrapper.EventType;
            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(wrapper);
                if (handlers.Count == 0)
                    _handlers.Remove(eventType);
            }
        }
    }
}
```

## UI系统扩展

### 可扩展View系统
```csharp
public class ExtensibleViewRegistry : IViewRegistry
{
    private readonly Dictionary<Type, ViewRegistration> _viewRegistrations = new();
    private readonly Dictionary<string, Type> _viewTypeCache = new();
    private readonly object _lock = new();
    
    public void RegisterView<TView, TViewModel>()
        where TView : MonoBehaviour, IView
        where TViewModel : IViewModel
    {
        var viewType = typeof(TView);
        var viewModelType = typeof(TViewModel);
        
        lock (_lock)
        {
            _viewRegistrations[viewModelType] = new ViewRegistration
            {
                ViewType = viewType,
                ViewModelType = viewModelType,
                CreationMethod = CreateView<TView, TViewModel>
            };
            
            _viewTypeCache[viewType.Name] = viewType;
        }
    }
    
    public void RegisterView(Type viewType, Type viewModelType)
    {
        if (!typeof(IView).IsAssignableFrom(viewType))
            throw new ArgumentException($"View类型必须实现IView接口", nameof(viewType));
            
        if (!typeof(IViewModel).IsAssignableFrom(viewModelType))
            throw new ArgumentException($"ViewModel类型必须实现IViewModel接口", nameof(viewModelType));
        
        lock (_lock)
        {
            _viewRegistrations[viewModelType] = new ViewRegistration
            {
                ViewType = viewType,
                ViewModelType = viewModelType,
                CreationMethod = (viewModel, parent) => CreateViewInternal(viewType, viewModel, parent)
            };
            
            _viewTypeCache[viewType.Name] = viewType;
        }
    }
    
    public IView CreateView(IViewModel viewModel, Transform parent = null)
    {
        var viewModelType = viewModel.GetType();
        
        lock (_lock)
        {
            if (!_viewRegistrations.TryGetValue(viewModelType, out var registration))
            {
                // 尝试查找基类注册
                var baseType = viewModelType.BaseType;
                while (baseType != null && baseType != typeof(object))
                {
                    if (_viewRegistrations.TryGetValue(baseType, out registration))
                        break;
                    baseType = baseType.BaseType;
                }
                
                if (registration == null)
                    throw new InvalidOperationException($"找不到ViewModel类型 {viewModelType} 对应的View注册");
            }
            
            return registration.CreationMethod(viewModel, parent);
        }
    }
    
    private IView CreateView<TView, TViewModel>(IViewModel viewModel, Transform parent)
        where TView : MonoBehaviour, IView
        where TViewModel : IViewModel
    {
        var gameObject = new GameObject(typeof(TView).Name);
        if (parent != null)
            gameObject.transform.SetParent(parent, false);
            
        var view = gameObject.AddComponent<TView>();
        view.Initialize((TViewModel)viewModel);
        return view;
    }
}
```

### UI组件插件系统
```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public class UIComponentAttribute : Attribute
{
    public string ComponentId { get; }
    public string Category { get; set; } = "General";
    public string Icon { get; set; }
    public int Order { get; set; }
    
    public UIComponentAttribute(string componentId)
    {
        ComponentId = componentId;
    }
}

public class UIComponentRegistry
{
    private readonly Dictionary<string, UIComponentRegistration> _components = new();
    private readonly Dictionary<string, List<UIComponentRegistration>> _categories = new();
    
    public void RegisterComponent<TComponent>() where TComponent : MonoBehaviour, IUIComponent
    {
        var componentType = typeof(TComponent);
        var attribute = componentType.GetCustomAttribute<UIComponentAttribute>();
        if (attribute == null)
            throw new InvalidOperationException($"UI组件 {componentType} 缺少 UIComponentAttribute");
        
        var registration = new UIComponentRegistration
        {
            ComponentId = attribute.ComponentId,
            ComponentType = componentType,
            Category = attribute.Category,
            Icon = attribute.Icon,
            Order = attribute.Order,
            CreateInstance = parent => CreateComponentInstance<TComponent>(parent)
        };
        
        _components[attribute.ComponentId] = registration;
        
        if (!_categories.TryGetValue(attribute.Category, out var categoryList))
        {
            categoryList = new List<UIComponentRegistration>();
            _categories[attribute.Category] = categoryList;
        }
        
        categoryList.Add(registration);
        categoryList.Sort((a, b) => a.Order.CompareTo(b.Order));
    }
    
    public IUIComponent CreateComponent(string componentId, Transform parent = null)
    {
        if (!_components.TryGetValue(componentId, out var registration))
            throw new KeyNotFoundException($"找不到UI组件: {componentId}");
            
        return registration.CreateInstance(parent);
    }
    
    public IReadOnlyList<UIComponentRegistration> GetComponentsByCategory(string category)
    {
        return _categories.TryGetValue(category, out var components) 
            ? components 
            : new List<UIComponentRegistration>();
    }
}
```

## 数据模型扩展

### 动态组件系统
```csharp
public class DynamicComponentSystem : IDisposable
{
    private readonly World _world;
    private readonly Dictionary<Type, ComponentType> _componentTypes = new();
    private readonly Dictionary<Entity, Dictionary<Type, IComponentData>> _entityComponents = new();
    
    public DynamicComponentSystem(World world)
    {
        _world = world;
    }
    
    public void RegisterComponentType<T>() where T : struct, IComponentData
    {
        var componentType = ComponentType.ReadWrite<T>();
        _componentTypes[typeof(T)] = componentType;
    }
    
    public bool HasComponent<T>(Entity entity) where T : struct, IComponentData
    {
        if (_world.EntityManager.HasComponent<T>(entity))
            return true;
            
        return _entityComponents.TryGetValue(entity, out var components) &&
               components.ContainsKey(typeof(T));
    }
    
    public T GetComponent<T>(Entity entity) where T : struct, IComponentData
    {
        if (_world.EntityManager.HasComponent<T>(entity))
            return _world.EntityManager.GetComponentData<T>(entity);
            
        if (_entityComponents.TryGetValue(entity, out var components) &&
            components.TryGetValue(typeof(T), out var component))
        {
            return (T)component;
        }
        
        throw new InvalidOperationException($"实体没有组件 {typeof(T).Name}");
    }
    
    public void AddComponent<T>(Entity entity, T component) where T : struct, IComponentData
    {
        if (_world.EntityManager.HasComponent<T>(entity))
        {
            _world.EntityManager.SetComponentData(entity, component);
        }
        else
        {
            if (!_entityComponents.TryGetValue(entity, out var components))
            {
                components = new Dictionary<Type, IComponentData>();
                _entityComponents[entity] = components;
            }
            
            components[typeof(T)] = component;
        }
    }
    
    public void RemoveComponent<T>(Entity entity) where T : struct, IComponentData
    {
        if (_world.EntityManager.HasComponent<T>(entity))
        {
            _world.EntityManager.RemoveComponent<T>(entity);
        }
        else if (_entityComponents.TryGetValue(entity, out var components))
        {
            components.Remove(typeof(T));
            if (components.Count == 0)
                _entityComponents.Remove(entity);
        }
    }
    
    public void SyncToECS()
    {
        foreach (var entityComponents in _entityComponents)
        {
            var entity = entityComponents.Key;
            var components = entityComponents.Value;
            
            foreach (var componentPair in components)
            {
                var componentType = _componentTypes[componentPair.Key];
                var componentData = componentPair.Value;
                
                // 将动态组件转换为ECS组件
                ConvertAndAddComponent(entity, componentType, componentData);
            }
        }
        
        // 清空动态组件缓存
        _entityComponents.Clear();
    }
    
    public void Dispose()
    {
        _entityComponents.Clear();
    }
}
```

## 扩展性测试策略

### 插件兼容性测试
```csharp
[TestFixture]
public class PluginCompatibilityTests
{
    private PluginManager _pluginManager;
    private TestPluginContext _pluginContext;
    
    [SetUp]
    public void Setup()
    {
        _pluginManager = new PluginManager();
        _pluginContext = new TestPluginContext();
    }
    
    [Test]
    public void Plugin_ShouldLoadSuccessfully()
    {
        // Arrange
        var plugin = new TestPlugin();
        
        // Act
        plugin.Initialize(_pluginContext);
        
        // Assert
        Assert.AreEqual(PluginState.Initialized, plugin.State);
        Assert.IsTrue(_pluginContext.Services.Any());
    }
    
    [Test]
    public void PluginDependencies_ShouldResolveCorrectly()
    {
        // Arrange
        var pluginA = new PluginA();
        var pluginB = new PluginB(); // 依赖于PluginA
        
        // Act & Assert
        Assert.DoesNotThrow(() => _pluginManager.LoadPlugin(pluginB));
        Assert.IsTrue(_pluginManager.IsPluginLoaded(pluginA.Id));
        Assert.IsTrue(_pluginManager.IsPluginLoaded(pluginB.Id));
    }
    
    [Test]
    public void PluginConflict_ShouldBeDetected()
    {
        // Arrange
        var plugin1 = new ConflictingPlugin { ServiceId = "TestService" };
        var plugin2 = new ConflictingPlugin { ServiceId = "TestService" };
        
        // Act & Assert
        _pluginManager.LoadPlugin(plugin1);
        Assert.Throws<PluginConflictException>(() => _pluginManager.LoadPlugin(plugin2));
    }
}
```

### API兼容性测试
```csharp
[TestFixture]
public class ApiCompatibilityTests
{
    [Test]
    public void PublicAPI_ShouldRemainBackwardCompatible()
    {
        // Arrange
        var assembly = typeof(DIContainer).Assembly;
        var publicTypes = assembly.GetTypes()
            .Where(t => t.IsPublic && !t.IsNested)
            .ToList();
        
        // Act & Assert: 检查每个公共类型的API稳定性
        foreach (var type in publicTypes)
        {
            AssertPublicApiStability(type);
        }
    }
    
    private void AssertPublicApiStability(Type type)
    {
        // 获取所有公共成员
        var publicMembers = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => !m.IsSpecialName) // 排除属性访问器等
            .ToList();
        
        // 验证成员签名没有破坏性变更
        foreach (var member in publicMembers)
        {
            // 这里可以添加更详细的API兼容性检查
            Assert.IsNotNull(member, $"公共API成员不能为null: {type.Name}.{member.Name}");
        }
    }
}
```

## 总结

### 扩展性设计优势
1. **模块化架构**：支持功能模块的独立开发和部署
2. **插件化系统**：支持第三方扩展和热插拔功能
3. **配置驱动**：无需代码修改即可调整系统行为
4. **API稳定性**：保持向后兼容，支持平滑升级
5. **渐进式增强**：支持从简单到复杂的功能演进

### 技术深度体现
1. **动态插件系统**：完整的插件生命周期管理和依赖解决
2. **可扩展配置**：支持多种配置源和运行时配置更新
3. **服务发现**：基于特性的自动化服务注册
4. **事件系统扩展**：动态中间件和处理器注册
5. **UI组件系统**：可扩展的UI组件注册和管理

### 学习价值
1. **企业级扩展架构**：展示完整的可扩展系统设计
2. **插件系统开发**：插件管理和依赖解决的实际应用
3. **配置系统设计**：灵活可扩展的配置管理方案
4. **API设计原则**：保持API稳定性的最佳实践

这个可扩展性设计方案为项目提供了强大的扩展能力，支持从个人项目到企业级应用的平滑演进。