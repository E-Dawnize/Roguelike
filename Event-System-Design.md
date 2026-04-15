# 事件系统设计文档

## 设计目标

### 核心目标
1. **跨层通信**：建立MVVM、DOTS、UI之间的松耦合通信机制
2. **线程安全**：支持多线程环境下的安全事件处理
3. **性能优化**：高频率事件处理的性能优化和内存管理
4. **可扩展性**：支持动态添加事件类型和处理器
5. **错误恢复**：事件处理异常的恢复机制

### 技术挑战
1. **跨线程事件传递**：DOTS工作线程 ↔ 主线程 ↔ UI线程
2. **事件优先级**：不同类型事件的处理优先级和顺序
3. **内存管理**：高频事件的对象池和内存分配优化
4. **依赖管理**：事件处理器间的依赖关系和执行顺序
5. **调试支持**：事件流的可视化监控和调试工具

## 系统架构

### 分层事件架构
```
┌───────────────── 事件发布层 ─────────────────┐
│ MVVM层 │ DOTS层 │ UI层 │                 │
└─────────┬──────────┬──────────┬──────────────┘
          │          │          │
┌─────────┼──────────┼──────────┼──────────────┐
│          事件中心层 (EventCenter)           │
│  ├─ EventBus (核心总线)                    │
│  ├─ EventDispatcher (派发器)               │
│  └─ EventQueue (异步队列)                  │
└─────────┬────────────────────────────────────┘
          │
┌─────────┼────────────────────────────────────┐
│        事件处理层                          │
│  ├─ SynchronousHandlers (同步处理器)       │
│  ├─ AsynchronousHandlers (异步处理器)      │
│  └─ PipelineHandlers (管道处理器)          │
└──────────────────────────────────────────────┘
```

### 核心组件职责
1. **EventBus**：事件注册和派发的核心组件
2. **EventDispatcher**：管理事件处理器的执行和调度
3. **EventQueue**：异步事件队列，支持优先级
4. **EventProcessor**：事件处理器的基类接口
5. **EventMiddleware**：事件处理中间件，支持AOP

## EventCenter详细设计

### 核心接口定义
```csharp
public interface IEventCenter : IDisposable
{
    // 订阅事件
    IDisposable Subscribe<TEvent>(Action<TEvent> handler, 
        EventPriority priority = EventPriority.Normal,
        string handlerName = null) where TEvent : IEvent;
    
    // 发布事件
    void Publish<TEvent>(TEvent @event) where TEvent : IEvent;
    
    // 异步发布事件
    Task PublishAsync<TEvent>(TEvent @event, 
        CancellationToken cancellationToken = default) where TEvent : IEvent;
    
    // 查询事件处理器
    IReadOnlyList<EventHandlerInfo> GetHandlers<TEvent>() where TEvent : IEvent;
    
    // 移除事件处理器
    bool RemoveHandler<TEvent>(string handlerName) where TEvent : IEvent;
}

public interface IEvent
{
    DateTime Timestamp { get; }
    string EventId { get; }
    object Source { get; }
}
```

### EventBus实现
```csharp
public class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, EventHandlerCollection> _handlers;
    private readonly EventDispatcher _dispatcher;
    private readonly EventQueue _eventQueue;
    private readonly ILogger<EventBus> _logger;
    
    public EventBus(EventDispatcher dispatcher, EventQueue eventQueue, ILogger<EventBus> logger)
    {
        _handlers = new ConcurrentDictionary<Type, EventHandlerCollection>();
        _dispatcher = dispatcher;
        _eventQueue = eventQueue;
        _logger = logger;
    }
    
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler, 
        EventPriority priority = EventPriority.Normal,
        string handlerName = null) where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        var collection = _handlers.GetOrAdd(eventType, _ => new EventHandlerCollection());
        
        var handlerWrapper = new EventHandlerWrapper<TEvent>(
            handler, priority, handlerName ?? Guid.NewGuid().ToString());
        
        var subscription = collection.Add(handlerWrapper);
        
        _logger.LogDebug($"订阅事件: {eventType.Name}, 处理器: {handlerWrapper.Name}");
        
        return new EventSubscription(() =>
        {
            collection.Remove(handlerWrapper);
            _logger.LogDebug($"取消订阅事件: {eventType.Name}, 处理器: {handlerWrapper.Name}");
        });
    }
    
    public void Publish<TEvent>(TEvent @event) where TEvent : IEvent
    {
        if (@event == null) throw new ArgumentNullException(nameof(@event));
        
        var eventType = typeof(TEvent);
        if (!_handlers.TryGetValue(eventType, out var collection))
            return;
        
        // 根据优先级排序处理器
        var handlers = collection.GetHandlersOrderedByPriority();
        
        // 使用分发器执行
        _dispatcher.Dispatch(@event, handlers);
    }
    
    public async Task PublishAsync<TEvent>(TEvent @event, 
        CancellationToken cancellationToken = default) where TEvent : IEvent
    {
        if (@event == null) throw new ArgumentNullException(nameof(@event));
        
        // 将事件加入异步队列
        await _eventQueue.EnqueueAsync(@event, cancellationToken);
        
        _logger.LogDebug($"异步发布事件: {typeof(TEvent).Name}, ID: {@event.EventId}");
    }
}
```

### EventHandlerCollection实现
```csharp
internal class EventHandlerCollection
{
    private readonly List<IEventHandlerWrapper> _handlers = new();
    private readonly object _lock = new();
    
    public IDisposable Add(IEventHandlerWrapper handler)
    {
        lock (_lock)
        {
            _handlers.Add(handler);
            _handlers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
        
        return new EventSubscription(() => Remove(handler));
    }
    
    public bool Remove(IEventHandlerWrapper handler)
    {
        lock (_lock)
        {
            return _handlers.Remove(handler);
        }
    }
    
    public IReadOnlyList<IEventHandlerWrapper> GetHandlersOrderedByPriority()
    {
        lock (_lock)
        {
            return _handlers.ToList();
        }
    }
}

internal interface IEventHandlerWrapper
{
    EventPriority Priority { get; }
    string Name { get; }
    void Invoke(IEvent @event);
}

internal class EventHandlerWrapper<TEvent> : IEventHandlerWrapper where TEvent : IEvent
{
    private readonly Action<TEvent> _handler;
    
    public EventHandlerWrapper(Action<TEvent> handler, EventPriority priority, string name)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        Priority = priority;
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
    
    public EventPriority Priority { get; }
    public string Name { get; }
    
    public void Invoke(IEvent @event)
    {
        if (@event is TEvent typedEvent)
            _handler(typedEvent);
    }
}
```

## 跨线程事件处理设计

### 主线程事件调度器
```csharp
public class MainThreadEventDispatcher : EventDispatcher
{
    private readonly SynchronizationContext _mainThreadContext;
    private readonly ConcurrentQueue<EventDispatchTask> _pendingTasks;
    private readonly object _lock = new();
    
    public MainThreadEventDispatcher()
    {
        _mainThreadContext = SynchronizationContext.Current ?? 
            throw new InvalidOperationException("必须在主线程创建MainThreadEventDispatcher");
        _pendingTasks = new ConcurrentQueue<EventDispatchTask>();
    }
    
    public override void Dispatch<TEvent>(TEvent @event, IReadOnlyList<IEventHandlerWrapper> handlers)
    {
        // 如果当前在主线程，直接执行
        if (IsMainThread())
        {
            ExecuteHandlers(@event, handlers);
            return;
        }
        
        // 否则调度到主线程执行
        var task = new EventDispatchTask(@event, handlers);
        _pendingTasks.Enqueue(task);
        
        _mainThreadContext.Post(_ =>
        {
            if (_pendingTasks.TryDequeue(out var pendingTask))
                ExecuteHandlers(pendingTask.Event, pendingTask.Handlers);
        }, null);
    }
    
    private bool IsMainThread()
    {
        return Thread.CurrentThread.ManagedThreadId == 1; // Unity主线程ID通常为1
    }
    
    private record EventDispatchTask(IEvent Event, IReadOnlyList<IEventHandlerWrapper> Handlers);
}
```

### 异步事件队列
```csharp
public class EventQueue : IEventQueue
{
    private readonly PriorityQueue<QueuedEvent, EventPriority> _queue;
    private readonly SemaphoreSlim _semaphore;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _processingTask;
    
    public EventQueue()
    {
        _queue = new PriorityQueue<QueuedEvent, EventPriority>();
        _semaphore = new SemaphoreSlim(0);
        _cancellationTokenSource = new CancellationTokenSource();
        _processingTask = Task.Run(ProcessEventsAsync);
    }
    
    public async Task EnqueueAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) 
        where TEvent : IEvent
    {
        var queuedEvent = new QueuedEvent(@event, DateTime.UtcNow);
        lock (_queue)
        {
            _queue.Enqueue(queuedEvent, GetEventPriority(@event));
        }
        
        _semaphore.Release();
        
        await Task.CompletedTask;
    }
    
    private async Task ProcessEventsAsync()
    {
        var token = _cancellationTokenSource.Token;
        
        while (!token.IsCancellationRequested)
        {
            await _semaphore.WaitAsync(token);
            
            if (token.IsCancellationRequested)
                break;
            
            QueuedEvent queuedEvent;
            lock (_queue)
            {
                if (_queue.Count == 0)
                    continue;
                    
                queuedEvent = _queue.Dequeue();
            }
            
            try
            {
                await ProcessEventAsync(queuedEvent.Event, token);
            }
            catch (Exception ex)
            {
                // 记录错误但不中断处理
                Debug.LogError($"事件处理失败: {ex.Message}");
            }
        }
    }
    
    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _processingTask.Wait(TimeSpan.FromSeconds(5));
        _semaphore.Dispose();
        _cancellationTokenSource.Dispose();
    }
}
```

## 事件中间件系统

### 中间件接口
```csharp
public interface IEventMiddleware
{
    Task HandleAsync<TEvent>(TEvent @event, EventContext context, EventMiddlewareDelegate next) 
        where TEvent : IEvent;
}

public delegate Task EventMiddlewareDelegate<TEvent>(TEvent @event, EventContext context) 
    where TEvent : IEvent;

public class EventContext
{
    public DateTime StartTime { get; }
    public DateTime? EndTime { get; set; }
    public bool IsHandled { get; set; }
    public Exception Error { get; set; }
    public Dictionary<string, object> Properties { get; }
    
    public EventContext()
    {
        StartTime = DateTime.UtcNow;
        Properties = new Dictionary<string, object>();
    }
}
```

### 常用中间件实现
```csharp
// 1. 日志中间件
public class LoggingMiddleware : IEventMiddleware
{
    private readonly ILogger<LoggingMiddleware> _logger;
    
    public LoggingMiddleware(ILogger<LoggingMiddleware> logger)
    {
        _logger = logger;
    }
    
    public async Task HandleAsync<TEvent>(TEvent @event, EventContext context, 
        EventMiddlewareDelegate next) where TEvent : IEvent
    {
        _logger.LogInformation($"开始处理事件: {@event.GetType().Name}, ID: {@event.EventId}");
        
        try
        {
            await next(@event, context);
            
            if (context.IsHandled)
            {
                _logger.LogInformation($"事件处理成功: {@event.GetType().Name}, 耗时: {context.EndTime - context.StartTime}");
            }
            else
            {
                _logger.LogWarning($"事件未处理: {@event.GetType().Name}");
            }
        }
        catch (Exception ex)
        {
            context.Error = ex;
            _logger.LogError(ex, $"事件处理失败: {@event.GetType().Name}");
            throw;
        }
    }
}

// 2. 性能监控中间件
public class PerformanceMiddleware : IEventMiddleware
{
    private readonly IPerformanceMonitor _monitor;
    
    public PerformanceMiddleware(IPerformanceMonitor monitor)
    {
        _monitor = monitor;
    }
    
    public async Task HandleAsync<TEvent>(TEvent @event, EventContext context, 
        EventMiddlewareDelegate next) where TEvent : IEvent
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await next(@event, context);
        }
        finally
        {
            stopwatch.Stop();
            _monitor.RecordEventProcessingTime(@event.GetType(), stopwatch.Elapsed);
        }
    }
}

// 3. 重试中间件
public class RetryMiddleware : IEventMiddleware
{
    private readonly int _maxRetries;
    
    public RetryMiddleware(int maxRetries = 3)
    {
        _maxRetries = maxRetries;
    }
    
    public async Task HandleAsync<TEvent>(TEvent @event, EventContext context, 
        EventMiddlewareDelegate next) where TEvent : IEvent
    {
        int retryCount = 0;
        
        while (true)
        {
            try
            {
                await next(@event, context);
                return;
            }
            catch (Exception ex) when (retryCount < _maxRetries && ShouldRetry(ex))
            {
                retryCount++;
                await Task.Delay(TimeSpan.FromMilliseconds(100 * retryCount));
                continue;
            }
        }
    }
    
    private bool ShouldRetry(Exception ex)
    {
        // 判断是否应该重试的异常类型
        return ex is TimeoutException || 
               ex is NetworkException ||
               (ex is InvalidOperationException ioe && ioe.Message.Contains("temporarily"));
    }
}
```

## 事件管道构建器

### 管道配置
```csharp
public class EventPipelineBuilder
{
    private readonly List<IEventMiddleware> _middlewares = new();
    
    public EventPipelineBuilder AddMiddleware<TMiddleware>() where TMiddleware : IEventMiddleware
    {
        // 通过DI容器获取中间件实例
        var middleware = ServiceLocator.GetService<TMiddleware>();
        _middlewares.Add(middleware);
        return this;
    }
    
    public EventPipelineBuilder AddMiddleware(IEventMiddleware middleware)
    {
        _middlewares.Add(middleware);
        return this;
    }
    
    public EventMiddlewareDelegate Build()
    {
        EventMiddlewareDelegate pipeline = (e, c) => Task.CompletedTask;
        
        // 反向构建中间件链
        for (int i = _middlewares.Count - 1; i >= 0; i--)
        {
            var currentMiddleware = _middlewares[i];
            var next = pipeline;
            pipeline = (e, c) => currentMiddleware.HandleAsync(e, c, next);
        }
        
        return pipeline;
    }
}
```

## 性能优化策略

### 1. 事件对象池
```csharp
public class EventPool<TEvent> where TEvent : class, IEvent, new()
{
    private readonly ConcurrentBag<TEvent> _pool = new();
    private readonly int _maxSize;
    
    public EventPool(int maxSize = 100)
    {
        _maxSize = maxSize;
    }
    
    public TEvent Rent()
    {
        if (_pool.TryTake(out var @event))
        {
            return @event;
        }
        return new TEvent();
    }
    
    public void Return(TEvent @event)
    {
        if (_pool.Count < _maxSize)
        {
            // 重置事件状态
            if (@event is IResettable resettable)
                resettable.Reset();
                
            _pool.Add(@event);
        }
    }
}
```

### 2. 事件批处理
```csharp
public class BatchedEventProcessor
{
    private readonly List<IEvent> _batch = new();
    private readonly object _lock = new();
    private readonly int _batchSize;
    private readonly TimeSpan _batchTimeout;
    
    public BatchedEventProcessor(int batchSize = 10, TimeSpan batchTimeout = default)
    {
        _batchSize = batchSize;
        _batchTimeout = batchTimeout == default ? TimeSpan.FromMilliseconds(100) : batchTimeout;
    }
    
    public void ProcessEvent(IEvent @event)
    {
        lock (_lock)
        {
            _batch.Add(@event);
            
            if (_batch.Count >= _batchSize)
                ProcessBatch();
        }
    }
    
    private void ProcessBatch()
    {
        List<IEvent> batchToProcess;
        lock (_lock)
        {
            if (_batch.Count == 0) return;
            
            batchToProcess = new List<IEvent>(_batch);
            _batch.Clear();
        }
        
        // 批量处理事件
        var groupedEvents = batchToProcess.GroupBy(e => e.GetType());
        
        foreach (var group in groupedEvents)
        {
            ProcessEventGroup(group.Key, group.ToList());
        }
    }
}
```

### 3. 事件类型缓存
```csharp
public class EventTypeCache
{
    private readonly ConcurrentDictionary<Type, EventTypeInfo> _cache = new();
    
    public EventTypeInfo GetOrAdd(Type eventType)
    {
        return _cache.GetOrAdd(eventType, type =>
        {
            var attributes = type.GetCustomAttributes(true);
            var priority = GetEventPriorityFromAttributes(attributes);
            var isAsync = typeof(IAsyncEvent).IsAssignableFrom(type);
            
            return new EventTypeInfo(type, priority, isAsync);
        });
    }
    
    private EventPriority GetEventPriorityFromAttributes(object[] attributes)
    {
        var priorityAttr = attributes.OfType<EventPriorityAttribute>().FirstOrDefault();
        return priorityAttr?.Priority ?? EventPriority.Normal;
    }
}
```

## 调试与监控

### 事件流监控器
```csharp
public class EventFlowMonitor : IEventFlowMonitor
{
    private readonly ConcurrentQueue<EventTrace> _traces = new();
    private readonly int _maxTraces = 1000;
    
    public void TraceEvent<TEvent>(TEvent @event, EventTraceAction action) where TEvent : IEvent
    {
        var trace = new EventTrace
        {
            EventId = @event.EventId,
            EventType = typeof(TEvent).Name,
            Timestamp = DateTime.UtcNow,
            Action = action,
            ThreadId = Thread.CurrentThread.ManagedThreadId
        };
        
        _traces.Enqueue(trace);
        
        // 保持队列大小
        while (_traces.Count > _maxTraces)
            _traces.TryDequeue(out _);
    }
    
    public IReadOnlyList<EventTrace> GetRecentTraces(int count = 100)
    {
        return _traces.TakeLast(count).ToList();
    }
    
    public EventStatistics GetStatistics(TimeSpan timeWindow)
    {
        var cutoffTime = DateTime.UtcNow - timeWindow;
        var relevantTraces = _traces.Where(t => t.Timestamp >= cutoffTime).ToList();
        
        return new EventStatistics
        {
            TotalEvents = relevantTraces.Count,
            EventTypeCounts = relevantTraces.GroupBy(t => t.EventType)
                .ToDictionary(g => g.Key, g => g.Count()),
            AverageProcessingTime = CalculateAverageTime(relevantTraces),
            ErrorCount = relevantTraces.Count(t => t.Action == EventTraceAction.Error)
        };
    }
}
```

## 使用示例

### 基本使用
```csharp
public class GameEventSystem
{
    private readonly IEventCenter _eventCenter;
    
    public GameEventSystem(IEventCenter eventCenter)
    {
        _eventCenter = eventCenter;
    }
    
    public void Initialize()
    {
        // 订阅玩家事件
        _eventCenter.Subscribe<PlayerHealthChangedEvent>(OnPlayerHealthChanged);
        _eventCenter.Subscribe<PlayerPositionChangedEvent>(OnPlayerPositionChanged);
        
        // 订阅敌人事件
        _eventCenter.Subscribe<EnemySpawnedEvent>(OnEnemySpawned);
        _eventCenter.Subscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
    }
    
    private void OnPlayerHealthChanged(PlayerHealthChangedEvent evt)
    {
        Debug.Log($"玩家血量变化: {evt.CurrentHealth}/{evt.MaxHealth}");
        
        // 更新UI
        EventCenter.Publish(new UIUpdateEvent 
        { 
            Type = UIUpdateType.Health, 
            Value = evt.CurrentHealth 
        });
    }
    
    private void OnPlayerPositionChanged(PlayerPositionChangedEvent evt)
    {
        // 更新小地图
        EventCenter.Publish(new MiniMapUpdateEvent 
        { 
            PlayerPosition = evt.NewPosition 
        });
    }
}
```

### 跨层事件集成
```csharp
// MVVM层 → DOTS层
public class PlayerInputHandler
{
    public void HandleMoveInput(Vector2 direction)
    {
        // 发布移动事件
        EventCenter.Publish(new PlayerMoveEvent 
        { 
            Direction = direction,
            Timestamp = DateTime.UtcNow
        });
    }
}

// DOTS系统处理事件
public class PlayerMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 查询所有待处理的移动事件
        var moveEvents = state.EntityManager
            .CreateEntityQuery(typeof(PlayerMoveEvent))
            .ToComponentDataArray<PlayerMoveEvent>(Allocator.Temp);
        
        foreach (var moveEvent in moveEvents)
        {
            // 处理移动逻辑
            ProcessMovement(ref state, moveEvent);
        }
        
        moveEvents.Dispose();
    }
}
```

## 测试策略

### 单元测试
```csharp
[TestFixture]
public class EventSystemTests
{
    private IEventCenter _eventCenter;
    
    [SetUp]
    public void Setup()
    {
        _eventCenter = new EventCenter();
    }
    
    [Test]
    public void Subscribe_ShouldReceiveEvents()
    {
        // Arrange
        bool eventReceived = false;
        var testEvent = new TestEvent();
        
        _eventCenter.Subscribe<TestEvent>(e => eventReceived = true);
        
        // Act
        _eventCenter.Publish(testEvent);
        
        // Assert
        Assert.IsTrue(eventReceived);
    }
    
    [Test]
    public async Task PublishAsync_ShouldProcessInBackground()
    {
        // Arrange
        var tcs = new TaskCompletionSource<bool>();
        var testEvent = new TestEvent();
        
        _eventCenter.Subscribe<TestEvent>(e => tcs.SetResult(true));
        
        // Act
        await _eventCenter.PublishAsync(testEvent);
        
        // Assert
        var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsTrue(result);
    }
}
```

### 性能测试
```csharp
[Test]
[PerformanceTest]
public void PublishPerformance_10000Events()
{
    // Arrange
    int eventCount = 0;
    _eventCenter.Subscribe<TestEvent>(e => eventCount++);
    
    var stopwatch = Stopwatch.StartNew();
    
    // Act
    for (int i = 0; i < 10000; i++)
    {
        _eventCenter.Publish(new TestEvent());
    }
    
    stopwatch.Stop();
    
    // Assert
    Assert.AreEqual(10000, eventCount);
    Assert.Less(stopwatch.ElapsedMilliseconds, 100); // 应该在100ms内完成
}
```

## 总结

### 技术深度体现
1. **现代事件驱动架构**：完整的发布-订阅模式实现，支持同步/异步处理
2. **跨线程安全**：主线程调度器确保线程安全的事件处理
3. **中间件管道**：AOP设计，支持可插拔的中间件扩展
4. **性能优化**：对象池、批处理、缓存等全方位优化策略
5. **监控与调试**：完整的追踪和监控系统

### 架构优势
1. **松耦合**：事件驱动实现各层间的完全解耦
2. **高可扩展**：易于添加新的事件类型和处理器
3. **高可维护**：清晰的接口设计和模块化结构
4. **健壮性**：错误处理、重试机制确保系统稳定性

### 学习价值
1. **企业级事件系统设计**：展示完整的事件系统架构
2. **性能优化实战**：从理论到实践的优化方案
3. **异步编程模式**：现代异步事件处理的最佳实践
4. **AOP应用**：中间件模式的实战应用

这个事件系统设计为项目提供了强大、灵活、高性能的通信机制，是MVVM+DOTS集成的关键桥梁。