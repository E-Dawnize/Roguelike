# DOTS集成设计文档

## 设计目标与挑战

### 设计目标
1. **建立完整的双向集成**：实现MVVM↔DOTS的双向数据流和事件通信
2. **高性能数据同步**：在保持DOTS性能优势的同时，实现与MVVM的高效数据同步
3. **松耦合架构**：通过事件驱动的设计，保持MVVM与DOTS间的松耦合
4. **完整生命周期管理**：通过DI系统管理ECS相关服务的生命周期

### 技术挑战
1. **范式差异**：MVVM基于对象导向，DOTS基于数据导向
2. **线程安全**：DOTS系统在工作线程运行，MVVM在主线程运行
3. **性能平衡**：数据同步的频率和开销需要精细控制
4. **内存管理**：跨范式数据转换的内存分配优化

## 总体架构

### 集成架构图
```
┌───────────────── MVVM层 ─────────────────┐
│  ViewModel ↔ Binding ↔ View (UI)        │
└─────────────────┬────────────────────────┘
                  │ EventCenter事件
┌─────────────────┼────────────────────────┐
│     Application层 (Bridge服务)          │
│  EntityDataSyncService ↔ EcsEventBridge │
└─────────────────┼────────────────────────┘
                  │ ECS事件/数据同步
┌─────────────────┼────────────────────────┐
│         DOTS层 (ECS系统)                │
│  EntityChangeSystem ↔ Component数据     │
└──────────────────────────────────────────┘
```

### 数据流设计
1. **正向流 (MVVM→DOTS)**：
   - MVVM事件 → EventCenter → EcsInputBridge → ECS组件数据
   
2. **反向流 (DOTS→MVVM)**：
   - ECS组件变化 → EntityChangeDetectionSystem → EcsEventBridge → EventCenter → EntityModel更新 → UI绑定更新

## ECS事件系统详细设计

### EntityChangeDetectionSystem
监听ECS组件变化，转换为事件通知。

```csharp
[BurstCompile]
public partial struct EntityChangeDetectionSystem : ISystem
{
    // 定义组件变化事件委托
    public delegate void EntityChangedHandler(Entity entity, ComponentType componentType);
    public event EntityChangedHandler EntityChanged;
    
    private EntityQuery _healthQuery;
    private EntityQuery _positionQuery;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // 构建查询，只监控需要同步的组件
        _healthQuery = state.GetEntityQuery(
            ComponentType.ReadWrite<HealthComponent>());
            
        _positionQuery = state.GetEntityQuery(
            ComponentType.ReadWrite<LocalTransform>());
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 检测血量组件变化
        CheckHealthChanges(ref state);
        
        // 检测位置组件变化
        CheckPositionChanges(ref state);
    }
    
    [BurstCompile]
    private void CheckHealthChanges(ref SystemState state)
    {
        foreach (var (health, entity) in 
                 SystemAPI.Query<RefRW<HealthComponent>>()
                     .WithEntityAccess())
        {
            // 使用Previous字段跟踪变化
            if (health.ValueRO.Current != health.ValueRO.Previous)
            {
                EntityChanged?.Invoke(entity, ComponentType.ReadWrite<HealthComponent>());
                health.ValueRW.Previous = health.ValueRO.Current;
            }
        }
    }
    
    // 类似方法检测其他组件变化...
}
```

### 组件变化检测策略
1. **脏标记系统**：组件内添加Previous字段存储上次值
2. **变化频率控制**：可配置的检测频率，避免每帧检查
3. **批处理通知**：收集一帧内所有变化，批量发送事件
4. **选择性监控**：只监控需要同步到MVVM的组件

## 数据同步服务详细设计

### EntityDataSyncService
监听ECS事件，同步数据到EntityModel。

```csharp
public class EntityDataSyncService : IInitializable, IDisposable
{
    private readonly IEventCenter _eventCenter;
    private readonly Dictionary<Entity, EntityModel> _entityModels;
    private readonly EntityModelPool _modelPool;
    
    public EntityDataSyncService(IEventCenter eventCenter)
    {
        _eventCenter = eventCenter;
        _entityModels = new Dictionary<Entity, EntityModel>();
        _modelPool = new EntityModelPool(maxSize: 100);
    }
    
    public void Initialize()
    {
        // 订阅ECS组件变化事件
        _eventCenter.Subscribe<EntityComponentChangedEvent>(OnEntityChanged);
        
        // 订阅实体创建/销毁事件
        _eventCenter.Subscribe<EntityCreatedEvent>(OnEntityCreated);
        _eventCenter.Subscribe<EntityDestroyedEvent>(OnEntityDestroyed);
    }
    
    private void OnEntityChanged(EntityComponentChangedEvent evt)
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        
        if (evt.ComponentType == typeof(HealthComponent))
        {
            var health = entityManager.GetComponentData<HealthComponent>(evt.Entity);
            
            if (_entityModels.TryGetValue(evt.Entity, out var model))
            {
                // 更新EntityModel数据
                model.CurrentHealth = health.Current;
                model.MaxHealth = health.Max;
                
                // 触发属性变更通知
                model.NotifyPropertyChanged(nameof(EntityModel.CurrentHealth));
                model.NotifyPropertyChanged(nameof(EntityModel.MaxHealth));
            }
        }
        else if (evt.ComponentType == typeof(LocalTransform))
        {
            var transform = entityManager.GetComponentData<LocalTransform>(evt.Entity);
            
            if (_entityModels.TryGetValue(evt.Entity, out var model))
            {
                model.Position = transform.Position;
                model.Rotation = transform.Rotation;
                
                model.NotifyPropertyChanged(nameof(EntityModel.Position));
                model.NotifyPropertyChanged(nameof(EntityModel.Rotation));
            }
        }
    }
    
    private void OnEntityCreated(EntityCreatedEvent evt)
    {
        // 从对象池获取或创建新的EntityModel
        var model = _modelPool.Get(evt.Entity, initialMaxHealth: 100);
        _entityModels[evt.Entity] = model;
        
        // 发布Model创建事件，供ViewModel订阅
        _eventCenter.Publish(new EntityModelCreatedEvent(model));
    }
    
    private void OnEntityDestroyed(EntityDestroyedEvent evt)
    {
        if (_entityModels.TryGetValue(evt.Entity, out var model))
        {
            // 返回对象池
            _modelPool.Return(model);
            _entityModels.Remove(evt.Entity);
            
            // 发布Model销毁事件
            _eventCenter.Publish(new EntityModelDestroyedEvent(model));
        }
    }
    
    public void Dispose()
    {
        // 清理所有缓存
        foreach (var model in _entityModels.Values)
            _modelPool.Return(model);
        _entityModels.Clear();
    }
}
```

### EntityModel对象池设计
```csharp
public class EntityModelPool : IDisposable
{
    private readonly Stack<EntityModel> _pool = new();
    private readonly int _maxSize;
    
    public EntityModelPool(int maxSize = 100)
    {
        _maxSize = maxSize;
    }
    
    public EntityModel Get(Entity entity, int initialMaxHealth)
    {
        if (_pool.Count > 0)
        {
            var model = _pool.Pop();
            model.Reinitialize(entity, initialMaxHealth);
            return model;
        }
        return new EntityModel(entity, initialMaxHealth);
    }
    
    public void Return(EntityModel model)
    {
        if (_pool.Count < _maxSize)
        {
            model.Reset();
            _pool.Push(model);
        }
    }
    
    public void Dispose()
    {
        _pool.Clear();
    }
}
```

## ECS DI集成设计

### EcsInstaller实现
通过DI容器注册ECS相关服务。

```csharp
[CreateAssetMenu(fileName = "EcsInstaller", menuName = "Installers/EcsInstaller")]
public class EcsInstaller : InstallerAsset
{
    public override void Register(DIContainer container)
    {
        // 注册ECS事件桥梁服务
        container.RegisterSingleton<EcsEventBridge>();
        container.RegisterSingleton<EntityDataSyncService>();
        container.RegisterSingleton<EcsBridgeManager>();
        
        // 注册需要World访问的服务
        container.RegisterSingleton<IEcsWorldAccessor>(sp =>
        {
            var world = World.DefaultGameObjectInjectionWorld;
            return new EcsWorldAccessor(world);
        });
        
        // 注册ECS系统管理器
        container.RegisterSingleton<IEcsSystemManager, EcsSystemManager>();
        
        // 注册组件数据访问器工厂
        container.RegisterFactory<ComponentDataAccessorFactory>();
    }
}
```

### EcsSystemManager
管理ECS系统的初始化和启动顺序。

```csharp
public interface IEcsSystemManager
{
    void InitializeAllSystems();
    void StartAllSystems();
    void StopAllSystems();
}

public class EcsSystemManager : IEcsSystemManager, IInitializable, IDisposable
{
    private readonly World _world;
    private readonly List<SystemHandle> _systemHandles = new();
    
    public EcsSystemManager(IEcsWorldAccessor worldAccessor)
    {
        _world = worldAccessor.World;
    }
    
    public void Initialize()
    {
        // 按依赖顺序创建和初始化系统
        var changeSystem = _world.CreateSystem<EntityChangeDetectionSystem>();
        _systemHandles.Add(changeSystem);
        
        var syncSystem = _world.CreateSystem<DataSyncSystem>();
        _systemHandles.Add(syncSystem);
        
        // 初始化所有系统
        foreach (var handle in _systemHandles)
            _world.Unmanaged.ResolveSystemStateRef(handle).Enabled = true;
    }
    
    public void Dispose()
    {
        // 清理所有系统
        foreach (var handle in _systemHandles)
            _world.DestroySystem(handle);
        _systemHandles.Clear();
    }
}
```

## 性能优化策略

### 1. 数据同步频率控制
```csharp
public class DataSyncFrequencyController
{
    private float _syncInterval = 0.1f; // 100ms同步一次
    private float _lastSyncTime;
    
    public bool ShouldSync()
    {
        var currentTime = Time.time;
        if (currentTime - _lastSyncTime >= _syncInterval)
        {
            _lastSyncTime = currentTime;
            return true;
        }
        return false;
    }
}
```

### 2. 批处理数据更新
```csharp
public class BatchedEntityUpdateService
{
    private readonly List<EntityUpdate> _pendingUpdates = new();
    private readonly object _lock = new();
    
    public void QueueUpdate(Entity entity, ComponentType componentType, object value)
    {
        lock (_lock)
        {
            _pendingUpdates.Add(new EntityUpdate(entity, componentType, value));
        }
    }
    
    public void ProcessBatchUpdates()
    {
        List<EntityUpdate> batch;
        lock (_lock)
        {
            if (_pendingUpdates.Count == 0) return;
            batch = new List<EntityUpdate>(_pendingUpdates);
            _pendingUpdates.Clear();
        }
        
        // 批量处理更新
        ProcessUpdates(batch);
    }
}
```

### 3. 内存分配优化
- **对象池**：EntityModel和事件对象使用对象池
- **结构体事件**：使用struct而非class定义事件，避免堆分配
- **数组复用**：重用数组和列表，避免频繁分配

### 4. 查询优化
```csharp
// 使用EntityQuery缓存避免每帧重建
public class OptimizedEntityQuery
{
    private EntityQuery _cachedQuery;
    
    public EntityQuery GetHealthQuery(EntityManager entityManager)
    {
        if (_cachedQuery == default)
        {
            _cachedQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadWrite<HealthComponent>());
        }
        return _cachedQuery;
    }
}
```

## 使用示例

### 完整集成示例
```csharp
// 1. 在BootConfig中添加EcsInstaller
[CreateAssetMenu(fileName = "BootConfig", menuName = "Configs/BootConfig")]
public class BootConfig : InstallerConfig
{
    public override void ConfigureInstallers()
    {
        // 添加全局安装器
        globalInstallers.Add(Resources.Load<CoreInstaller>("Installers/CoreInstaller"));
        globalInstallers.Add(Resources.Load<EcsInstaller>("Installers/EcsInstaller"));
        globalInstallers.Add(Resources.Load<MvvmInstaller>("Installers/MvvmInstaller"));
    }
}

// 2. ViewModel中使用EntityModel
public class PlayerViewModel : ViewModelBase
{
    private EntityModel _playerModel;
    
    [Inject]
    private IEventCenter _eventCenter;
    
    public PlayerViewModel()
    {
        // 订阅EntityModel创建事件
        _eventCenter.Subscribe<EntityModelCreatedEvent>(OnEntityModelCreated);
    }
    
    private void OnEntityModelCreated(EntityModelCreatedEvent evt)
    {
        // 检查是否为Player实体
        if (evt.Model.EntityType == EntityType.Player)
        {
            _playerModel = evt.Model;
            
            // 绑定到UI属性
            BindProperty(nameof(Health), () => _playerModel.CurrentHealth);
            BindProperty(nameof(MaxHealth), () => _playerModel.MaxHealth);
            BindProperty(nameof(Position), () => _playerModel.Position);
        }
    }
    
    public float Health => _playerModel?.CurrentHealth ?? 0;
    public float MaxHealth => _playerModel?.MaxHealth ?? 0;
    public Vector3 Position => _playerModel?.Position ?? Vector3.zero;
}

// 3. UI绑定
public class PlayerHUD : MonoBehaviour
{
    [SerializeField] private Text _healthText;
    [SerializeField] private Slider _healthSlider;
    [SerializeField] private Text _positionText;
    
    private PropertyBinding _healthBinding;
    private PropertyBinding _maxHealthBinding;
    private PropertyBinding _positionBinding;
    
    private void Start()
    {
        // 创建绑定
        _healthBinding = gameObject.AddComponent<PropertyBinding>();
        _healthBinding.Bind("Health", _healthText, "text");
        
        _maxHealthBinding = gameObject.AddComponent<PropertyBinding>();
        _maxHealthBinding.Bind("MaxHealth", _healthSlider, "maxValue");
        _healthBinding.Bind("Health", _healthSlider, "value");
        
        _positionBinding = gameObject.AddComponent<PropertyBinding>();
        _positionBinding.Bind("Position", _positionText, "text", 
            converter: "Vector3ToString");
    }
}
```

## 测试与验证

### 单元测试策略
1. **ECS事件系统测试**：验证组件变化正确触发事件
2. **数据同步测试**：验证ECS数据正确同步到EntityModel
3. **性能测试**：大量实体时的帧率和内存测试
4. **集成测试**：完整MVVM↔DOTS数据流测试

### 测试场景设计
1. **基础同步测试**：创建100个实体，验证数据同步正确性
2. **性能压力测试**：创建1000个实体，监控帧率和内存
3. **场景切换测试**：验证场景切换时绑定和资源的正确清理
4. **错误恢复测试**：模拟异常情况，验证系统恢复能力

## 总结

本DOTS集成设计提供了完整的MVVM↔DOTS双向集成方案，具有以下特点：

### 技术深度体现
1. **跨范式集成**：对象导向与数据导向的有机融合
2. **性能优化**：批处理、对象池、查询优化等全方位优化
3. **工程化设计**：完整的DI集成、配置驱动、生命周期管理

### 架构优势
1. **松耦合**：事件驱动的架构保持组件间松耦合
2. **可扩展**：易于添加新的组件同步类型
3. **可维护**：清晰的关注点分离和接口设计

### 学习价值
1. **现代Unity架构**：展示DOTS与MVVM的协同工作
2. **性能优化实践**：从理论到实践的性能优化案例
3. **工程化思维**：完整的系统设计和实现考虑

这个设计为项目提供了强大的DOTS集成能力，同时保持了架构的清晰性和可维护性，适合作为高级工程师的技术能力展示。