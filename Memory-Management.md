# 内存管理方案设计文档

## 设计目标

### 核心目标
1. **高效内存使用**：最小化托管堆和Native内存分配
2. **避免内存泄漏**：严格的生命周期管理和资源清理
3. **减少GC压力**：优化托管对象创建，避免GC触发
4. **内存监控**：运行时内存使用监控和警报
5. **跨平台优化**：针对不同平台的内存特性优化

### 技术挑战
1. **混合内存模型**：托管对象、Native内存、Unity资源的混合管理
2. **跨线程内存安全**：多线程环境下的内存访问安全性
3. **内存碎片化**：长期运行后的内存碎片问题
4. **内存泄漏检测**：复杂引用链导致的内存泄漏检测
5. **性能与内存平衡**：在性能和内存使用间找到最佳平衡点

## 内存管理架构

### 分层内存管理
```
┌─────────────────────────────────────────────────────────┐
│                  内存监控层 (Monitoring)                │
│  ├─ MemoryProfiler (性能分析器)                        │
│  ├─ LeakDetector (泄漏检测器)                          │
│  └─ AlertSystem (警报系统)                             │
└─────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────┐
│                  内存管理策略层 (Strategy)              │
│  ├─ PoolingStrategy (对象池策略)                       │
│  ├─ AllocationStrategy (分配策略)                      │
│  └─ CleanupStrategy (清理策略)                         │
└─────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────┐
│                  内存分配器层 (Allocator)               │
│  ├─ ManagedAllocator (托管分配器)                      │
│  ├─ NativeAllocator (Native分配器)                     │
│  └─ ResourceAllocator (资源分配器)                     │
└─────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────┐
│                  基础数据结构层 (Data Structures)       │
│  ├─ ObjectPools (对象池集合)                           │
│  ├─ ReferenceTrackers (引用追踪器)                     │
│  └─ MemoryRegions (内存区域管理)                       │
└─────────────────────────────────────────────────────────┘
```

### 核心组件职责
1. **MemoryManager**：内存管理的统一入口，协调各组件
2. **ObjectPoolManager**：管理所有对象池，提供对象的借出和归还
3. **NativeMemoryManager**：管理Native内存分配和释放
4. **ReferenceTracker**：追踪对象引用关系，检测内存泄漏
5. **MemoryProfiler**：运行时内存使用分析和监控

## 托管对象管理策略

### 对象池系统
```csharp
public class AdvancedObjectPool<T> : IDisposable where T : class, new()
{
    private readonly ConcurrentBag<T> _pool = new();
    private readonly Func<T> _createFunc;
    private readonly Action<T> _resetAction;
    private readonly Action<T> _disposeAction;
    private readonly int _maxSize;
    private int _rentedCount;
    
    public AdvancedObjectPool(
        Func<T> createFunc = null,
        Action<T> resetAction = null,
        Action<T> disposeAction = null,
        int maxSize = 1000)
    {
        _createFunc = createFunc ?? (() => new T());
        _resetAction = resetAction;
        _disposeAction = disposeAction;
        _maxSize = maxSize;
    }
    
    public T Rent()
    {
        Interlocked.Increment(ref _rentedCount);
        
        if (_pool.TryTake(out var item))
        {
            _resetAction?.Invoke(item);
            return item;
        }
        
        return _createFunc();
    }
    
    public void Return(T item)
    {
        Interlocked.Decrement(ref _rentedCount);
        
        if (_pool.Count < _maxSize)
        {
            _pool.Add(item);
        }
        else
        {
            _disposeAction?.Invoke(item);
        }
    }
    
    public PoolStatistics GetStatistics()
    {
        return new PoolStatistics
        {
            TotalCreated = _rentedCount + _pool.Count,
            CurrentlyRented = _rentedCount,
            AvailableInPool = _pool.Count,
            PoolSize = _maxSize
        };
    }
}
```

### 池化对象生命周期
```csharp
public interface IPoolableObject : IDisposable
{
    void Reset();  // 对象归还到池时重置状态
    bool IsValid { get; }  // 检查对象是否仍有效
}

public class PoolableComponent<T> : IPoolableObject where T : Component
{
    private readonly T _component;
    private bool _isRented;
    
    public PoolableComponent(T component)
    {
        _component = component;
    }
    
    public T Rent()
    {
        if (_isRented)
            throw new InvalidOperationException("对象已被借出");
            
        _isRented = true;
        _component.gameObject.SetActive(true);
        return _component;
    }
    
    public void Return()
    {
        if (!_isRented)
            throw new InvalidOperationException("对象未被借出");
            
        _isRented = false;
        _component.gameObject.SetActive(false);
        Reset();
    }
    
    public void Reset()
    {
        // 重置组件状态
        var resettable = _component as IResettable;
        resettable?.Reset();
    }
    
    public void Dispose()
    {
        if (_component != null)
            UnityEngine.Object.Destroy(_component.gameObject);
    }
}
```

### 高频对象池配置
```csharp
public static class CommonPools
{
    // Vector3对象池（高频使用）
    public static readonly ObjectPool<Vector3> Vector3Pool = new(
        () => new Vector3(),
        v => { v.x = 0; v.y = 0; v.z = 0; },
        maxSize: 10000
    );
    
    // List对象池
    public static readonly ObjectPool<List<object>> ListPool = new(
        () => new List<object>(),
        list => list.Clear(),
        maxSize: 1000
    );
    
    // 字符串构建器池
    public static readonly ObjectPool<StringBuilder> StringBuilderPool = new(
        () => new StringBuilder(),
        sb => sb.Clear(),
        maxSize: 100
    );
    
    // 事件对象池
    private static readonly Dictionary<Type, object> _eventPools = new();
    
    public static ObjectPool<T> GetEventPool<T>() where T : class, IEvent, new()
    {
        var type = typeof(T);
        if (!_eventPools.TryGetValue(type, out var pool))
        {
            pool = new ObjectPool<T>(
                () => new T(),
                e => (e as IResettable)?.Reset(),
                maxSize: 100
            );
            _eventPools[type] = pool;
        }
        return (ObjectPool<T>)pool;
    }
}
```

## Native内存管理

### Native内存分配器
```csharp
public class NativeMemoryAllocator : IDisposable
{
    private readonly Dictionary<IntPtr, AllocationInfo> _allocations = new();
    private readonly object _lock = new();
    private long _totalAllocated;
    private long _peakAllocation;
    
    public IntPtr Allocate(long size, Allocator allocator = Allocator.Persistent)
    {
        var ptr = UnsafeUtility.Malloc(size, 16, allocator);
        
        if (ptr == IntPtr.Zero)
            throw new OutOfMemoryException($"无法分配 {size} 字节内存");
            
        lock (_lock)
        {
            _allocations[ptr] = new AllocationInfo(size, allocator, Environment.StackTrace);
            _totalAllocated += size;
            _peakAllocation = Math.Max(_peakAllocation, _totalAllocated);
        }
        
        return ptr;
    }
    
    public void Free(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
            return;
            
        lock (_lock)
        {
            if (_allocations.TryGetValue(ptr, out var info))
            {
                UnsafeUtility.Free(ptr, info.Allocator);
                _totalAllocated -= info.Size;
                _allocations.Remove(ptr);
            }
            else
            {
                Debug.LogWarning($"尝试释放未追踪的内存指针: {ptr}");
            }
        }
    }
    
    public void CheckLeaks()
    {
        lock (_lock)
        {
            if (_allocations.Count > 0)
            {
                Debug.LogError($"检测到内存泄漏! 未释放的分配: {_allocations.Count}");
                
                foreach (var kvp in _allocations)
                {
                    Debug.LogError($"泄漏: 地址={kvp.Key}, 大小={kvp.Value.Size}, 分配器={kvp.Value.Allocator}");
                    Debug.LogError($"分配堆栈:\n{kvp.Value.StackTrace}");
                }
            }
        }
    }
}
```

### Native集合池
```csharp
public class NativeCollectionPool
{
    private readonly Dictionary<Type, object> _pools = new();
    
    public NativeList<T> RentNativeList<T>(int initialCapacity = 10, Allocator allocator = Allocator.TempJob) 
        where T : unmanaged
    {
        var pool = GetOrCreatePool<NativeList<T>>(() => 
            new ObjectPool<NativeList<T>>(
                () => new NativeList<T>(initialCapacity, allocator),
                list => list.Clear(),
                maxSize: 100
            ));
            
        var list = pool.Rent();
        return list;
    }
    
    public void ReturnNativeList<T>(NativeList<T> list) where T : unmanaged
    {
        if (list.IsCreated)
        {
            var pool = GetOrCreatePool<NativeList<T>>(() => null);
            if (pool != null)
            {
                pool.Return(list);
            }
            else
            {
                list.Dispose();
            }
        }
    }
    
    private ObjectPool<T> GetOrCreatePool<T>(Func<ObjectPool<T>> createPool) where T : class
    {
        var type = typeof(T);
        if (!_pools.TryGetValue(type, out var poolObj))
        {
            var pool = createPool();
            if (pool != null)
            {
                _pools[type] = pool;
                return pool;
            }
            return null;
        }
        return (ObjectPool<T>)poolObj;
    }
}
```

## 内存泄漏检测

### 引用追踪系统
```csharp
public class ReferenceTracker : IDisposable
{
    private readonly ConditionalWeakTable<object, TrackingInfo> _trackedObjects = new();
    private readonly ConcurrentDictionary<long, WeakReference> _objectRegistry = new();
    private readonly Timer _cleanupTimer;
    
    public ReferenceTracker()
    {
        // 每5分钟清理一次死亡对象
        _cleanupTimer = new Timer(CleanupDeadObjects, null, 
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }
    
    public void TrackObject(object obj, string category, string context = null)
    {
        if (obj == null) return;
        
        var info = new TrackingInfo
        {
            Category = category,
            Context = context,
            CreationTime = DateTime.UtcNow,
            CreationStackTrace = Environment.StackTrace
        };
        
        _trackedObjects.Add(obj, info);
        _objectRegistry[GetObjectId(obj)] = new WeakReference(obj);
    }
    
    public void ReportPotentialLeaks()
    {
        var aliveObjects = new List<KeyValuePair<object, TrackingInfo>>();
        
        foreach (var kvp in _trackedObjects)
        {
            if (IsAlive(kvp.Key))
                aliveObjects.Add(kvp);
        }
        
        // 按类别分组
        var grouped = aliveObjects.GroupBy(kvp => kvp.Value.Category);
        
        foreach (var group in grouped)
        {
            var category = group.Key;
            var count = group.Count();
            var oldest = group.Min(kvp => kvp.Value.CreationTime);
            var age = DateTime.UtcNow - oldest;
            
            if (age > TimeSpan.FromMinutes(10)) // 10分钟以上的对象可能是泄漏
            {
                Debug.LogWarning($"潜在内存泄漏: 类别={category}, 数量={count}, 最老对象存在时间={age}");
                
                // 输出详细信息
                foreach (var kvp in group.Take(5)) // 显示前5个
                {
                    Debug.LogWarning($"泄漏对象: 上下文={kvp.Value.Context}, 创建时间={kvp.Value.CreationTime}");
                    Debug.LogWarning($"创建堆栈:\n{kvp.Value.CreationStackTrace}");
                }
            }
        }
    }
    
    private void CleanupDeadObjects(object state)
    {
        var deadIds = new List<long>();
        
        foreach (var kvp in _objectRegistry)
        {
            if (!kvp.Value.IsAlive)
                deadIds.Add(kvp.Key);
        }
        
        foreach (var id in deadIds)
        {
            _objectRegistry.TryRemove(id, out _);
        }
    }
}
```

## 内存使用监控

### 运行时内存分析器
```csharp
public class RuntimeMemoryProfiler : MonoBehaviour
{
    [SerializeField] private bool _enableMonitoring = true;
    [SerializeField] private float _updateInterval = 5f; // 每5秒更新一次
    [SerializeField] private int _historySize = 60; // 保留最近60个采样点
    
    private readonly List<MemorySnapshot> _history = new();
    private float _timer;
    private MemorySnapshot _current;
    
    private void Update()
    {
        if (!_enableMonitoring) return;
        
        _timer += Time.deltaTime;
        if (_timer >= _updateInterval)
        {
            _timer = 0f;
            TakeSnapshot();
        }
    }
    
    private void TakeSnapshot()
    {
        _current = new MemorySnapshot
        {
            Timestamp = DateTime.UtcNow,
            TotalMemory = Profiler.GetTotalAllocatedMemoryLong(),
            GCMemory = Profiler.GetTotalReservedMemoryLong(),
            MonoHeapSize = Profiler.GetMonoHeapSizeLong(),
            MonoUsedSize = Profiler.GetMonoUsedSizeLong(),
            TextureMemory = GetTextureMemory(),
            MeshMemory = GetMeshMemory(),
            AudioMemory = GetAudioMemory(),
            GameObjectCount = CountGameObjects(),
            ComponentCount = CountComponents()
        };
        
        _history.Add(_current);
        if (_history.Count > _historySize)
            _history.RemoveAt(0);
            
        CheckMemoryThresholds();
    }
    
    private void CheckMemoryThresholds()
    {
        long warningThreshold = 512 * 1024 * 1024; // 512MB
        long criticalThreshold = 768 * 1024 * 1024; // 768MB
        
        if (_current.TotalMemory > criticalThreshold)
        {
            Debug.LogError($"内存使用临界! 总内存: {FormatMemory(_current.TotalMemory)}");
            TriggerMemoryCleanup(MemoryCleanupLevel.Aggressive);
        }
        else if (_current.TotalMemory > warningThreshold)
        {
            Debug.LogWarning($"内存使用警告! 总内存: {FormatMemory(_current.TotalMemory)}");
            TriggerMemoryCleanup(MemoryCleanupLevel.Moderate);
        }
    }
    
    public MemoryReport GenerateReport()
    {
        if (_history.Count == 0)
            return null;
            
        var report = new MemoryReport
        {
            Current = _current,
            Average = CalculateAverage(),
            Peak = CalculatePeak(),
            Trends = CalculateTrends(),
            Recommendations = GenerateRecommendations()
        };
        
        return report;
    }
}
```

## 跨平台内存优化

### 平台特定优化策略
```csharp
public class PlatformMemoryOptimizer
{
    public void ApplyPlatformOptimizations()
    {
        var platform = Application.platform;
        
        switch (platform)
        {
            case RuntimePlatform.Android:
                ApplyAndroidOptimizations();
                break;
                
            case RuntimePlatform.IPhonePlayer:
                ApplyIOSOptimizations();
                break;
                
            case RuntimePlatform.WebGLPlayer:
                ApplyWebGLOptimizations();
                break;
                
            default:
                ApplyDesktopOptimizations();
                break;
        }
    }
    
    private void ApplyAndroidOptimizations()
    {
        // Android特定优化
        QualitySettings.masterTextureLimit = 1; // 降低纹理质量
        Application.targetFrameRate = 30; // 降低目标帧率
        Screen.sleepTimeout = SleepTimeout.NeverSleep; // 防止休眠
        
        // 使用更小的对象池
        CommonPools.Vector3Pool.MaxSize = 5000;
        CommonPools.ListPool.MaxSize = 500;
        
        Debug.Log("应用Android内存优化配置");
    }
    
    private void ApplyIOSOptimizations()
    {
        // iOS特定优化
        Application.targetFrameRate = 60;
        
        // Metal API特定优化
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal)
        {
            // 启用Metal内存优化
            // (这里可以添加Metal特定的内存优化代码)
        }
        
        Debug.Log("应用iOS内存优化配置");
    }
    
    private void ApplyWebGLOptimizations()
    {
        // WebGL内存限制更严格
        CommonPools.Vector3Pool.MaxSize = 1000;
        CommonPools.ListPool.MaxSize = 100;
        
        // 使用更小的纹理尺寸
        QualitySettings.masterTextureLimit = 2;
        
        Debug.Log("应用WebGL内存优化配置");
    }
}
```

## 内存优化最佳实践

### 编码规范
1. **避免装箱拆箱**
   ```csharp
   // 错误：使用object导致装箱
   List<object> mixedList = new();
   mixedList.Add(10); // 装箱
   mixedList.Add("text"); // 装箱
   
   // 正确：使用泛型避免装箱
   List<int> intList = new();
   List<string> stringList = new();
   ```

2. **重用对象**
   ```csharp
   // 错误：每帧创建新对象
   void Update()
   {
       var list = new List<Vector3>(); // 每帧分配
       // 使用list...
   }
   
   // 正确：重用对象
   private List<Vector3> _reusableList = new();
   void Update()
   {
       _reusableList.Clear(); // 重用现有对象
       // 使用_reusableList...
   }
   ```

3. **使用结构体替代类**
   ```csharp
   // 当数据量小且频繁创建时，使用结构体
   public struct DamageInfo
   {
       public float Amount;
       public DamageType Type;
       public Vector3 Position;
   }
   
   // 使用NativeArray处理大量数据
   NativeArray<DamageInfo> damageEvents = new(1000, Allocator.TempJob);
   ```

### 资源加载策略
```csharp
public class SmartResourceLoader : IDisposable
{
    private readonly Dictionary<string, ResourceHandle> _loadedResources = new();
    private readonly Dictionary<string, int> _referenceCounts = new();
    
    public T Load<T>(string path) where T : UnityEngine.Object
    {
        if (_loadedResources.TryGetValue(path, out var handle))
        {
            _referenceCounts[path]++;
            return handle.Resource as T;
        }
        
        var resource = Resources.Load<T>(path);
        if (resource != null)
        {
            _loadedResources[path] = new ResourceHandle(resource);
            _referenceCounts[path] = 1;
        }
        
        return resource;
    }
    
    public void Unload(string path)
    {
        if (_referenceCounts.TryGetValue(path, out var count))
        {
            count--;
            if (count <= 0)
            {
                if (_loadedResources.TryGetValue(path, out var handle))
                {
                    Resources.UnloadAsset(handle.Resource);
                    _loadedResources.Remove(path);
                }
                _referenceCounts.Remove(path);
            }
            else
            {
                _referenceCounts[path] = count;
            }
        }
    }
    
    public void UnloadUnused()
    {
        var unusedPaths = _referenceCounts
            .Where(kvp => kvp.Value <= 0)
            .Select(kvp => kvp.Key)
            .ToList();
            
        foreach (var path in unusedPaths)
        {
            if (_loadedResources.TryGetValue(path, out var handle))
            {
                Resources.UnloadAsset(handle.Resource);
                _loadedResources.Remove(path);
            }
            _referenceCounts.Remove(path);
        }
        
        Resources.UnloadUnusedAssets();
    }
}
```

## 测试与验证

### 内存泄漏测试
```csharp
[TestFixture]
public class MemoryLeakTests
{
    private MemoryProfiler _profiler;
    
    [SetUp]
    public void Setup()
    {
        _profiler = new MemoryProfiler();
    }
    
    [Test]
    public void EventSubscription_ShouldNotLeak()
    {
        // Arrange
        var eventCenter = new EventCenter();
        var initialMemory = GC.GetTotalMemory(true);
        
        // Act: 创建并订阅大量事件
        var subscriptions = new List<IDisposable>();
        for (int i = 0; i < 1000; i++)
        {
            var handler = new TestEventHandler();
            var subscription = eventCenter.Subscribe<TestEvent>(handler.Handle);
            subscriptions.Add(subscription);
        }
        
        // 清理订阅
        foreach (var subscription in subscriptions)
            subscription.Dispose();
        
        // 强制GC
        GC.Collect();
        GC.WaitForPendingFinalizers();
        
        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;
        
        // Assert: 内存增加应在可接受范围内
        Assert.Less(memoryIncrease, 1024 * 1024, $"内存泄漏: 增加了 {memoryIncrease} 字节");
    }
    
    [Test]
    public void NativeMemory_ShouldBeFreed()
    {
        // Arrange
        var allocator = new NativeMemoryAllocator();
        
        // Act: 分配并释放Native内存
        var pointers = new List<IntPtr>();
        for (int i = 0; i < 100; i++)
        {
            var ptr = allocator.Allocate(1024 * 1024); // 1MB each
            pointers.Add(ptr);
        }
        
        // 释放所有内存
        foreach (var ptr in pointers)
            allocator.Free(ptr);
        
        // Assert: 检查内存泄漏
        allocator.CheckLeaks();
        
        // 应该没有未释放的内存
        Assert.AreEqual(0, allocator.GetAllocationCount());
    }
}
```

### 性能基准测试
```csharp
[PerformanceTest]
public class MemoryAllocationPerformance()
{
    [Benchmark]
    public void ObjectPool_Performance()
    {
        var pool = new ObjectPool<Vector3>(() => new Vector3(), maxSize: 1000);
        
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < 10000; i++)
        {
            var vector = pool.Rent();
            // 使用vector...
            pool.Return(vector);
        }
        
        stopwatch.Stop();
        
        Assert.Less(stopwatch.ElapsedMilliseconds, 50, "对象池性能不达标");
    }
    
    [Benchmark]
    public void NativeArray_Performance()
    {
        var stopwatch = Stopwatch.StartNew();
        
        using (var array = new NativeArray<float>(100000, Allocator.TempJob))
        {
            // 访问Native数组
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = i;
            }
        }
        
        stopwatch.Stop();
        
        Assert.Less(stopwatch.ElapsedMilliseconds, 10, "Native数组性能不达标");
    }
}
```

## 总结

### 技术深度体现
1. **混合内存管理**：同时管理托管堆、Native内存和Unity资源
2. **智能对象池**：针对不同类型对象的自适应池化策略
3. **内存泄漏检测**：运行时引用追踪和泄漏检测机制
4. **跨平台优化**：针对不同平台的特定内存优化策略
5. **性能监控**：实时内存使用监控和预警系统

### 架构优势
1. **预防性设计**：通过对象池和重用机制预防内存问题
2. **诊断能力**：强大的内存泄漏检测和诊断工具
3. **自适应优化**：根据运行平台和设备自动调整内存策略
4. **可扩展性**：易于添加新的内存管理策略和监控指标

### 学习价值
1. **现代内存管理**：展示Unity中内存管理的最佳实践
2. **性能优化实战**：从理论到实践的内存优化方案
3. **诊断工具开发**：内存泄漏检测和性能分析工具的实现
4. **跨平台开发**：不同平台内存特性的理解和优化

这个内存管理方案为项目提供了全面、高效、可靠的内存管理能力，是大型Unity项目稳定运行的关键保障。