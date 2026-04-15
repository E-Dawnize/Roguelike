# 阶段二实施指导：ECS系统集成

## 概述

本指南基于《阶段二任务规划：ECS系统集成》（`Task-Phase2-ECS-Integration.md`）和《DOTS集成设计》（`DOTS-Integration-Design.md`）提供具体的实施步骤和代码示例。阶段二的目标是实现DOTS与MVVM的双向集成，建立高性能的数据同步通道。

## 实施前检查

### 1. 阶段一完成验证

在开始阶段二前，确保阶段一任务已完成：
- [ ] DI容器功能完整，支持所有生命周期
- [ ] ViewModelBase提供完整属性通知和命令系统
- [ ] 基础绑定组件（PropertyBinding、CommandBinding）正常工作
- [ ] BindingManager有基本框架

### 2. 现有ECS代码分析

检查当前ECS实现：
- **现有系统**：MovementSystem等基础ECS系统
- **组件**：MoveComponents、PlayerInputState等
- **桥接**：EcsInputBridge（MVVM→ECS单向）
- **缺失**：ECS→MVVM数据同步、事件系统、DI集成

## 任务2.1：ECS事件系统开发

### 子任务1：组件变化检测系统

#### EntityChangeDetectionSystem实现

创建 `EntityChangeDetectionSystem.cs`：

```csharp
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Core.Events.EventInterfaces;

namespace ECS.Systems
{
    [BurstCompile]
    public partial struct EntityChangeDetectionSystem : ISystem
    {
        private EntityQuery _entityQuery;
        private NativeHashMap<Entity, ComponentHash> _previousState;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // 查询所有需要跟踪变化的实体
            _entityQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                Any = new ComponentType[]
                {
                    ComponentType.ReadOnly<ChangeTrackedComponent>(),
                    ComponentType.ReadOnly<IChangeTracked>()
                },
                Options = EntityQueryOptions.IncludeDisabledEntities | 
                         EntityQueryOptions.IncludePrefab
            });
            
            _previousState = new NativeHashMap<Entity, ComponentHash>(1000, Allocator.Persistent);
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!_entityQuery.IsEmptyIgnoreFilter)
            {
                var detectionJob = new ChangeDetectionJob
                {
                    EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                    PreviousState = _previousState,
                    ChangeEvents = new NativeList<ComponentChangeEvent>(Allocator.TempJob),
                    EventQueue = SystemAPI.GetSingletonRW<EventQueue>().ValueRW
                };
                
                detectionJob.Schedule(_entityQuery, state.Dependency).Complete();
                
                // 处理检测到的事件
                ProcessDetectedChanges(ref state, detectionJob.ChangeEvents);
                
                detectionJob.ChangeEvents.Dispose();
            }
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_previousState.IsCreated)
                _previousState.Dispose();
        }
        
        private void ProcessDetectedChanges(ref SystemState state, NativeList<ComponentChangeEvent> changes)
        {
            if (changes.Length == 0)
                return;
                
            // 发布到事件系统
            var eventCenter = SystemAPI.GetSingleton<EventCenterRef>().Value;
            
            for (int i = 0; i < changes.Length; i++)
            {
                var change = changes[i];
                eventCenter.Publish(new EntityChangedEvent
                {
                    Entity = change.Entity,
                    ChangedComponents = change.ChangedComponents,
                    WorldIndex = state.WorldUnmanaged.Index
                });
            }
        }
        
        [BurstCompile]
        private struct ChangeDetectionJob : IJobEntityBatch
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public NativeHashMap<Entity, ComponentHash> PreviousState;
            public NativeList<ComponentChangeEvent> ChangeEvents;
            public RefRW<EventQueue> EventQueue;
            
            public void Execute(ArchetypeChunk batch, int batchIndex)
            {
                var entities = batch.GetNativeArray(EntityTypeHandle);
                var currentHash = CalculateComponentHash(batch);
                
                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    
                    if (PreviousState.TryGetValue(entity, out var previousHash))
                    {
                        if (!currentHash.Equals(previousHash))
                        {
                            var changedComponents = currentHash.Difference(previousHash);
                            ChangeEvents.Add(new ComponentChangeEvent(entity, changedComponents));
                            PreviousState[entity] = currentHash;
                        }
                    }
                    else
                    {
                        ChangeEvents.Add(new ComponentChangeEvent(entity, currentHash));
                        PreviousState[entity] = currentHash;
                    }
                }
            }
            
            private ComponentHash CalculateComponentHash(ArchetypeChunk chunk)
            {
                // 计算组件哈希值用于变化检测
                var hash = new ComponentHash();
                var componentTypes = chunk.Archetype.GetComponentTypes();
                
                for (int i = 0; i < componentTypes.Length; i++)
                {
                    if (componentTypes[i].IsManagedComponent)
                        continue;
                        
                    // 获取组件数据并计算哈希
                    // 简化实现，实际需要根据组件类型处理
                    hash.AddComponent(componentTypes[i].TypeIndex);
                }
                
                return hash;
            }
        }
    }
    
    public struct ComponentChangeEvent
    {
        public Entity Entity;
        public ComponentHash ChangedComponents;
        public double Timestamp;
        
        public ComponentChangeEvent(Entity entity, ComponentHash changedComponents)
        {
            Entity = entity;
            ChangedComponents = changedComponents;
            Timestamp = Time.ElapsedTime;
        }
    }
    
    public struct ComponentHash : IEquatable<ComponentHash>
    {
        public ulong HashValue;
        
        public void AddComponent(TypeIndex typeIndex)
        {
            HashValue ^= (ulong)typeIndex.Value * 0x9e3779b97f4a7c15UL;
            HashValue = (HashValue << 31) | (HashValue >> 33);
        }
        
        public ComponentHash Difference(ComponentHash other)
        {
            // 计算哈希差异
            var diff = new ComponentHash();
            diff.HashValue = HashValue ^ other.HashValue;
            return diff;
        }
        
        public bool Equals(ComponentHash other) => HashValue == other.HashValue;
        public override bool Equals(object obj) => obj is ComponentHash other && Equals(other);
        public override int GetHashCode() => HashValue.GetHashCode();
    }
}
```

### 子任务2：ECS事件定义

#### ECS事件类型定义

创建 `EcsEventTypes.cs`：

```csharp
using Unity.Entities;
using Core.Events.EventInterfaces;

namespace ECS.Events
{
    // 基础ECS事件接口
    public interface IEcsEvent : IEvent
    {
        Entity Entity { get; }
        int WorldIndex { get; }
        double Timestamp { get; }
    }
    
    // 具体事件类型
    public struct EntityCreatedEvent : IEcsEvent
    {
        public Entity Entity { get; set; }
        public int WorldIndex { get; set; }
        public double Timestamp { get; set; }
        public EntityArchetype Archetype;
        
        public static EntityCreatedEvent Create(Entity entity, int worldIndex)
        {
            return new EntityCreatedEvent
            {
                Entity = entity,
                WorldIndex = worldIndex,
                Timestamp = Time.ElapsedTime
            };
        }
    }
    
    public struct EntityDestroyedEvent : IEcsEvent
    {
        public Entity Entity { get; set; }
        public int WorldIndex { get; set; }
        public double Timestamp { get; set; }
        
        public static EntityDestroyedEvent Create(Entity entity, int worldIndex)
        {
            return new EntityDestroyedEvent
            {
                Entity = entity,
                WorldIndex = worldIndex,
                Timestamp = Time.ElapsedTime
            };
        }
    }
    
    public struct ComponentAddedEvent<T> : IEcsEvent where T : struct, IComponentData
    {
        public Entity Entity { get; set; }
        public int WorldIndex { get; set; }
        public double Timestamp { get; set; }
        public T ComponentData;
        
        public static ComponentAddedEvent<T> Create(Entity entity, int worldIndex, T component)
        {
            return new ComponentAddedEvent<T>
            {
                Entity = entity,
                WorldIndex = worldIndex,
                Timestamp = Time.ElapsedTime,
                ComponentData = component
            };
        }
    }
    
    public struct ComponentRemovedEvent<T> : IEcsEvent where T : struct, IComponentData
    {
        public Entity Entity { get; set; }
        public int WorldIndex { get; set; }
        public double Timestamp { get; set; }
        
        public static ComponentRemovedEvent<T> Create(Entity entity, int worldIndex)
        {
            return new ComponentRemovedEvent<T>
            {
                Entity = entity,
                WorldIndex = worldIndex,
                Timestamp = Time.ElapsedTime
            };
        }
    }
    
    public struct ComponentChangedEvent<T> : IEcsEvent where T : struct, IComponentData
    {
        public Entity Entity { get; set; }
        public int WorldIndex { get; set; }
        public double Timestamp { get; set; }
        public T PreviousValue;
        public T NewValue;
        
        public static ComponentChangedEvent<T> Create(Entity entity, int worldIndex, T previous, T current)
        {
            return new ComponentChangedEvent<T>
            {
                Entity = entity,
                WorldIndex = worldIndex,
                Timestamp = Time.ElapsedTime,
                PreviousValue = previous,
                NewValue = current
            };
        }
    }
}
```

### 子任务3：事件序列化优化

#### 事件对象池实现

创建 `EcsEventPool.cs`：

```csharp
using System;
using System.Collections.Concurrent;
using Unity.Collections;
using Unity.Entities;

namespace ECS.Events
{
    public class EcsEventPool : IDisposable
    {
        private readonly ConcurrentDictionary<Type, object> _pools = new();
        private readonly int _initialPoolSize = 100;
        private readonly int _maxPoolSize = 1000;
        
        public EcsEventPool()
        {
            // 预创建常用事件类型的池
            CreatePool<EntityCreatedEvent>(_initialPoolSize);
            CreatePool<EntityDestroyedEvent>(_initialPoolSize);
            CreatePool<ComponentChangedEvent<HealthComponent>>(_initialPoolSize);
            CreatePool<ComponentChangedEvent<PositionComponent>>(_initialPoolSize);
        }
        
        public T Rent<T>() where T : struct, IEcsEvent
        {
            if (!_pools.TryGetValue(typeof(T), out var poolObj))
            {
                poolObj = CreatePool<T>(_initialPoolSize);
            }
            
            var pool = (ConcurrentQueue<T>)poolObj;
            if (pool.TryDequeue(out var item))
            {
                return item;
            }
            
            // 池为空，创建新实例
            return new T();
        }
        
        public void Return<T>(T item) where T : struct, IEcsEvent
        {
            if (!_pools.TryGetValue(typeof(T), out var poolObj))
            {
                poolObj = CreatePool<T>(_initialPoolSize);
            }
            
            var pool = (ConcurrentQueue<T>)poolObj;
            
            // 限制池大小，避免内存无限增长
            if (pool.Count < _maxPoolSize)
            {
                pool.Enqueue(item);
            }
        }
        
        private object CreatePool<T>(int initialSize) where T : struct, IEcsEvent
        {
            var pool = new ConcurrentQueue<T>();
            
            for (int i = 0; i < initialSize; i++)
            {
                pool.Enqueue(new T());
            }
            
            _pools[typeof(T)] = pool;
            return pool;
        }
        
        public void Dispose()
        {
            foreach (var pool in _pools.Values)
            {
                if (pool is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            
            _pools.Clear();
        }
    }
}
```

## 任务2.2：EntityDataSyncService开发

### 子任务1：数据同步服务

#### EntityDataSyncService核心实现

创建 `EntityDataSyncService.cs`：

```csharp
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using MVVM.Model;
using Core.DI;
using Core.Events.EventInterfaces;

namespace ECS.Services
{
    public interface IEntityDataSyncService : IInitializable, IDisposable
    {
        void RegisterSyncHandler<T>(IComponentSyncHandler handler) where T : struct, IComponentData;
        void UnregisterSyncHandler<T>() where T : struct, IComponentData;
        void SyncEntity(Entity entity);
        void SyncAllEntities();
        void SetSyncFrequency(float updatesPerSecond);
    }
    
    public class EntityDataSyncService : IEntityDataSyncService
    {
        private readonly World _world;
        private readonly IEventCenter _eventCenter;
        private readonly Dictionary<Type, IComponentSyncHandler> _syncHandlers;
        private readonly Dictionary<Entity, EntityModel> _entityModels;
        private readonly EntityQuery _syncEntitiesQuery;
        
        private float _syncInterval = 0.1f; // 默认100ms同步一次
        private float _lastSyncTime;
        
        [Inject]
        public EntityDataSyncService(World world, IEventCenter eventCenter)
        {
            _world = world;
            _eventCenter = eventCenter;
            _syncHandlers = new Dictionary<Type, IComponentSyncHandler>();
            _entityModels = new Dictionary<Entity, EntityModel>();
            
            // 查询所有需要同步的实体
            _syncEntitiesQuery = _world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SyncToViewModelComponent>());
            
            RegisterDefaultHandlers();
            SubscribeToEvents();
        }
        
        public void Initialize()
        {
            // 初始同步所有实体
            SyncAllEntities();
        }
        
        private void RegisterDefaultHandlers()
        {
            RegisterSyncHandler<HealthComponent>(new HealthSyncHandler());
            RegisterSyncHandler<PositionComponent>(new PositionSyncHandler());
            RegisterSyncHandler<InventoryComponent>(new InventorySyncHandler());
        }
        
        private void SubscribeToEvents()
        {
            _eventCenter.Subscribe<EntityCreatedEvent>(OnEntityCreated);
            _eventCenter.Subscribe<EntityDestroyedEvent>(OnEntityDestroyed);
            _eventCenter.Subscribe<ComponentChangedEvent<HealthComponent>>(OnHealthChanged);
            _eventCenter.Subscribe<ComponentChangedEvent<PositionComponent>>(OnPositionChanged);
        }
        
        public void RegisterSyncHandler<T>(IComponentSyncHandler handler) where T : struct, IComponentData
        {
            _syncHandlers[typeof(T)] = handler;
        }
        
        public void UnregisterSyncHandler<T>() where T : struct, IComponentData
        {
            _syncHandlers.Remove(typeof(T));
        }
        
        public void SyncEntity(Entity entity)
        {
            if (!_world.EntityManager.Exists(entity))
                return;
            
            if (!_entityModels.TryGetValue(entity, out var model))
            {
                model = new EntityModel(entity, _world);
                _entityModels[entity] = model;
            }
            
            // 同步所有注册的组件类型
            foreach (var kvp in _syncHandlers)
            {
                if (_world.EntityManager.HasComponent(entity, kvp.Key))
                {
                    kvp.Value.SyncToModel(entity, model, _world.EntityManager);
                }
            }
            
            // 发布同步完成事件
            _eventCenter.Publish(new EntitySyncCompletedEvent
            {
                Entity = entity,
                Model = model
            });
        }
        
        public void SyncAllEntities()
        {
            var entities = _syncEntitiesQuery.ToEntityArray(Allocator.Temp);
            
            foreach (var entity in entities)
            {
                SyncEntity(entity);
            }
            
            entities.Dispose();
        }
        
        public void SetSyncFrequency(float updatesPerSecond)
        {
            _syncInterval = 1.0f / updatesPerSecond;
        }
        
        public void Update()
        {
            // 在Update中检查是否需要同步
            if (Time.time - _lastSyncTime >= _syncInterval)
            {
                SyncChangedEntities();
                _lastSyncTime = Time.time;
            }
        }
        
        private void SyncChangedEntities()
        {
            // 实现增量同步逻辑
            // 只同步发生变化的实体
            // 可以使用EntityChangeDetectionSystem检测的变化
        }
        
        private void OnEntityCreated(EntityCreatedEvent evt)
        {
            if (_world.EntityManager.HasComponent<SyncToViewModelComponent>(evt.Entity))
            {
                SyncEntity(evt.Entity);
            }
        }
        
        private void OnEntityDestroyed(EntityDestroyedEvent evt)
        {
            if (_entityModels.Remove(evt.Entity, out var model))
            {
                model.Dispose();
            }
        }
        
        private void OnHealthChanged(ComponentChangedEvent<HealthComponent> evt)
        {
            if (_entityModels.TryGetValue(evt.Entity, out var model))
            {
                // 更新EntityModel中的健康值
                if (_syncHandlers.TryGetValue(typeof(HealthComponent), out var handler))
                {
                    handler.SyncToModel(evt.Entity, model, _world.EntityManager);
                }
            }
        }
        
        private void OnPositionChanged(ComponentChangedEvent<PositionComponent> evt)
        {
            // 类似处理位置变化
        }
        
        public void Dispose()
        {
            foreach (var model in _entityModels.Values)
            {
                model.Dispose();
            }
            
            _entityModels.Clear();
            _syncHandlers.Clear();
            
            _eventCenter.Unsubscribe<EntityCreatedEvent>(OnEntityCreated);
            _eventCenter.Unsubscribe<EntityDestroyedEvent>(OnEntityDestroyed);
        }
    }
    
    // 组件同步处理器接口
    public interface IComponentSyncHandler
    {
        void SyncToModel(Entity entity, EntityModel model, EntityManager entityManager);
        void SyncToEcs(Entity entity, EntityModel model, EntityManager entityManager);
    }
    
    // 健康组件同步处理器示例
    public class HealthSyncHandler : IComponentSyncHandler
    {
        public void SyncToModel(Entity entity, EntityModel model, EntityManager entityManager)
        {
            if (entityManager.HasComponent<HealthComponent>(entity))
            {
                var health = entityManager.GetComponentData<HealthComponent>(entity);
                model.Health = health.Current;
                model.MaxHealth = health.Max;
            }
        }
        
        public void SyncToEcs(Entity entity, EntityModel model, EntityManager entityManager)
        {
            if (entityManager.HasComponent<HealthComponent>(entity))
            {
                var health = entityManager.GetComponentData<HealthComponent>(entity);
                health.Current = model.Health;
                entityManager.SetComponentData(entity, health);
            }
        }
    }
}
```

由于文档长度限制，这里只提供了部分实施指导。完整的实施指南应包含：

## 后续内容概览

### 任务2.3：ECS DI集成
- EcsInstaller实现
- ECS服务通过DI容器管理
- 生命周期同步
- 场景切换资源管理

### 任务2.4：跨线程绑定支持
- MainThreadDispatcher实现
- 线程安全数据绑定
- 双缓冲数据交换
- 内存屏障使用

### 任务2.5：集成测试与性能验证
- ECS集成测试套件
- 性能基准测试
- 压力测试场景
- 内存泄漏检测

## 实施建议

### 1. 实施顺序
1. 先实现ECS事件系统（任务2.1）
2. 再实现数据同步服务（任务2.2）  
3. 然后集成DI容器（任务2.3）
4. 最后实现跨线程支持（任务2.4）

### 2. 性能考虑
- 使用Burst编译关键系统
- 实现事件批处理减少开销
- 使用对象池避免GC分配
- 控制同步频率避免过度更新

### 3. 测试策略
- 单元测试每个事件类型
- 集成测试完整数据流
- 性能测试大规模实体场景
- 压力测试长时间运行

## 验收检查点

完成阶段二后，检查以下验收标准：

### ECS事件系统
- [ ] 组件变化正确触发事件
- [ ] 事件发布性能达标（<0.1ms/事件）
- [ ] 内存使用符合预期（无持续增长）
- [ ] 支持高频组件变化（1000+实体）

### 数据同步服务
- [ ] ECS数据正确同步到EntityModel
- [ ] EntityModel变更正确反映到ECS
- [ ] 双向数据流延迟<50ms
- [ ] 支持1000+实体的同时同步

### DI集成
- [ ] ECS系统可通过DI容器配置
- [ ] DI服务可在ECS系统中正确注入
- [ ] 生命周期管理正确工作
- [ ] 场景切换无资源泄漏

## 下一步准备

完成阶段二后，可以开始阶段三：UI系统重构。参考 `Task-Phase3-UI-Refactoring.md` 和 `Binding-Manager-Design.md` 进行准备。

---
*文档版本：1.0*
*最后更新：2026-04-12*
*相关文档：Task-Phase2-ECS-Integration.md, DOTS-Integration-Design.md, Event-System-Design.md*
