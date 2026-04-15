classDiagram
    %% ==================== 基础设施层 (Infrastructure) ====================
    note for UpdateRunner "RuntimeInitializeOnLoadMethod创建\n管理Unity生命周期"
    class UpdateRunner {
        -static UpdateRunner _instance
        +static Initialize()
        +Update()
        +LateUpdate()
        +FixedUpdate()
    }
    
    note for ProjectContext "MonoBehaviour单例\n全局DI容器管理"
    class ProjectContext {
        -static ProjectContext _instance
        -DIContainer _globalContainer
        +static Ensure()
        -Boot()
        -LoadInstallerConfig()
        -InstallGlobal()
        -Initialize()
        -StartAll()
    }
    
    note for InputInitializer "独立初始化输入系统"
    class InputInitializer {
        -static PlayerInputManager _instance
        +static Initialize()
        +static GetInput() IPlayerInput
    }
    
    class PlayerInputManager {
        +Initialize()
        +IPlayerInput接口实现
    }
    
    %% ==================== DI容器层 (Dependency Injection) ====================
    note for DIContainer "核心DI容器\n支持Singleton/Scoped/Transient"
    class DIContainer {
        -ConcurrentDictionary~Type, List~ServiceDescriptor~~ _serviceDescriptors
        -ConcurrentDictionary~int, object~ _singletonInstances
        +RegisterSingleton~TService, TImplementation~()
        +RegisterScoped~TService, TImplementation~()
        +RegisterTransient~TService, TImplementation~()
        +GetService~T~() T
        +ResolveAll~T~() IEnumerable~T~
        +CreateScope() IScope
        +Inject(object target)
    }
    
    class ServiceDescriptor {
        +Type ServiceType
        +ServiceLifetime Lifetime
        +Type ImplementationType
        +object ImplementationInstance
        +Func~IServiceProvider, object~ ImplementationFactory
    }
    
    class IScope {
        <<interface>>
        +IServiceProvider ServiceProvider
        +Dispose()
    }
    
    %% ==================== 安装器系统 (Installer System) ====================
    note for InstallerAsset "ScriptableObject安装器基类"
    class InstallerAsset {
        <<abstract>>
        +Register(DIContainer container)*
    }
    
    class InstallerConfig {
        +List~InstallerAsset~ Installers
        +List~InstallerAsset~ GlobalInstallersSorted
    }
    
    class CoreInstaller {
        +Register(DIContainer container)
        -CreatePlayerInputManager() IPlayerInput
    }
    
    class ControllerInstaller {
        +Register(DIContainer container)
        -RegisterController~T~()
    }
    
    class InputInstaller {
        +Register(DIContainer container)
    }
    
    class EcsInstaller {
        +Register(DIContainer container)
    }
    
    %% ==================== 核心接口层 (Core Interfaces) ====================
    class IInstaller {
        <<interface>>
        +Register(DIContainer container)
    }
    
    class IInitializable {
        <<interface>>
        +Initialize()
    }
    
    class IStartable {
        <<interface>>
        +OnStart()
    }
    
    class ITickable {
        <<interface>>
        +Tick(float deltaTime)
    }
    
    class IDisposable {
        <<interface>>
        +Dispose()
    }
    
    %% ==================== 事件系统 (Event System) ====================
    note for IEventCenter "基于类型的事件系统"
    class IEventCenter {
        <<interface>>
        +Subscribe~T~(Action~T~ handler)
        +Unsubscribe~T~(Action~T~ handler)
        +Publish~T~(T message)
    }
    
    class EventManager {
        -Dictionary~Type, Delegate~ _eventHandlers
        +Subscribe~T~(Action~T~ handler)
        +Unsubscribe~T~(Action~T~ handler)
        +Publish~T~(T message)
        +Dispose()
    }
    
    %% ==================== 控制器系统 (Controller System) ====================
    note for IController "MVC控制器接口"
    class IController {
        <<interface>>
        +Bind()
        +Unbind()
        +Tick(float dt)
    }
    
    class IControllerManager {
        <<interface>>
        +List~IController~ _controllers
        +Add(IController controller)
        +Remove(IController controller)
        +Shutdown()
        +Tick(float dt)
    }
    
    class ControllerBase {
        <<abstract>>
        #IEventCenter EventCenter
        #bool IsBound
        +Bind()
        +Unbind()
        +Tick(float dt)
        #OnDestroy()
    }
    
    class ControllerBase~TModel~ {
        <<abstract>>
        #TModel Model
    }
    
    class ControllerManager {
        -List~IController~ _controllers
        -IEnumerable~IController~ _injectedControllers
        +Add(IController controller)
        +Remove(IController controller)
        +Shutdown()
        +OnStart()
        +Tick(float dt)
        +Dispose()
    }
    
    class PlayerController {
        +Bind()
        +Unbind()
        +Tick(float dt)
    }
    
    class CombatController {
        +Bind()
        +Unbind()
        +Tick(float dt)
    }
    
    class GameFlowController {
        +Bind()
        +Unbind()
        +Tick(float dt)
    }
    
    class HudController {
        +Bind()
        +Unbind()
        +Tick(float dt)
    }
    
    %% ==================== 桥接层 (Bridge Layer) ====================
    note for IEcsInputBridge "连接MVC和ECS的桥梁"
    class IEcsInputBridge {
        <<interface>>
        +SetMove(Vector2 dir, bool isActive)
        +bool IsReady
    }
    
    class EcsInputBridge {
        -EntityManager _em
        -Entity _inputEntity
        -bool _initialized
        +bool IsReady
        +Initialize()
        +SetMove(Vector2 dir, bool isActive)
    }
    
    %% ==================== 输入系统 (Input System) ====================
    class IPlayerInput {
        <<interface>>
        +Vector2 Move { get; }
        +bool Attack { get; }
        +bool Jump { get; }
    }
    
    class ISettingInput {
        <<interface>>
        +bool Pause { get; }
        +bool OpenSettings { get; }
    }
    
    %% ==================== 模型层 (Model Layer) ====================
    note for ModelBase "数据模型基类"
    class ModelBase {
        <<abstract>>
    }
    
    class EntityModel {
        +Vector3 Position
        +Quaternion Rotation
        +float Health
        +float MaxHealth
    }
    
    %% ==================== 视图层 (View Layer) ====================
    note for PlayerView "MVC视图层"
    class PlayerView {
        +Transform Target
        +Animator Animator
        +void UpdatePosition(Vector3 position)
        +void UpdateRotation(Quaternion rotation)
        +void PlayAnimation(string animationName)
    }
    
    %% ==================== 工具类 (Utility Classes) ====================
    class MonoSingleton~T~ {
        <<abstract>>
        -static T _instance
        +static T Instance
        +virtual void Awake()
    }
    
    class GlobalTime {
        +static float TimeScale
        +static float DeltaTime
        +static float FixedDeltaTime
        +static void Pause()
        +static void Resume()
    }
    
    %% ==================== 依赖关系 ====================
    %% 基础设施层依赖
    UpdateRunner --> ProjectBootstrap : 查询状态
    ProjectContext --> InstallerConfig : 加载配置
    ProjectContext --> DIContainer : 创建和管理
    InputInitializer --> PlayerInputManager : 创建实例
    
    %% DI容器依赖
    DIContainer --> ServiceDescriptor : 包含
    DIContainer ..> IScope : 实现
    
    %% 安装器依赖
    InstallerAsset ..|> IInstaller : 实现
    CoreInstaller --|> InstallerAsset : 继承
    ControllerInstaller --|> InstallerAsset : 继承
    InputInstaller --|> InstallerAsset : 继承
    EcsInstaller --|> InstallerAsset : 继承
    InstallerConfig --> InstallerAsset : 包含列表
    
    %% 控制器系统依赖
    ControllerManager ..|> IControllerManager : 实现
    ControllerManager ..|> IStartable : 实现
    ControllerManager ..|> ITickable : 实现
    ControllerManager ..|> IDisposable : 实现
    ControllerManager --> IController : 管理
    ControllerBase ..|> IController : 实现
    ControllerBase~TModel~ --|> ControllerBase : 继承
    PlayerController --|> ControllerBase : 继承
    CombatController --|> ControllerBase : 继承
    GameFlowController --|> ControllerBase : 继承
    HudController --|> ControllerBase : 继承
    
    %% 事件系统依赖
    EventManager ..|> IEventCenter : 实现
    EventManager ..|> IDisposable : 实现
    
    %% 桥接层依赖
    EcsInputBridge ..|> IEcsInputBridge : 实现
    
    %% 输入系统依赖
    PlayerInputManager ..|> IPlayerInput : 实现
    
    %% 模型层依赖
    EntityModel --|> ModelBase : 继承
    
    %% 工具类依赖
    MonoSingleton~T~ --> MonoBehaviour : 继承
    
    %% 接口实现关系
    class IInitializable {
        <<interface>>
        +Initialize()
    }
    
    class IStartable {
        <<interface>>
        +OnStart()
    }
    
    class ITickable {
        <<interface>>
        +Tick(float deltaTime)
    }
    
    class IDisposable {
        <<interface>>
        +Dispose()
    }
    
    %% 标记哪些类实现了这些接口
    ControllerManager ..|> IInitializable : 实现
    ControllerManager ..|> IStartable : 实现
    ControllerManager ..|> ITickable : 实现
    ControllerManager ..|> IDisposable : 实现
    EventManager ..|> IDisposable : 实现
    
    %% 依赖注入关系
    ControllerManager --> IEventCenter : [Inject]
    ControllerBase --> IEventCenter : [Inject]
    ControllerBase~TModel~ --> TModel : [Inject]
    
    %% 启动流程
    ProjectBootstrap --> ProjectContext : 调用Ensure()
    ProjectContext --> CoreInstaller : 执行注册
    ProjectContext --> ControllerInstaller : 执行注册
    ProjectContext --> ControllerManager : 解析并启动
    UpdateRunner --> ControllerManager : 调用Tick()
    
    %% 事件通信
    PlayerController --> IEventCenter : 订阅/发布
    CombatController --> IEventCenter : 订阅/发布
    GameFlowController --> IEventCenter : 订阅/发布
    HudController --> IEventCenter : 订阅/发布
    
    %% 输入流
    PlayerInputManager --> IEcsInputBridge : 传递输入
    IEcsInputBridge --> ECS.World : 设置组件数据