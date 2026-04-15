flowchart BT
AttachBuffEvent(AttachBuffEvent)
AttackConfig(AttackConfig)
AttackEvent(AttackEvent)
BuffComponents(BuffComponents)
BuffConfig(BuffConfig)
BuffEffectConfig(BuffEffectConfig)
BuffEffectType(BuffEffectType)
BuffType(BuffType)
CombatController(CombatController)
ControllerBase(ControllerBase)
ControllerBase(ControllerBase)
ControllerFactory(ControllerFactory)
ControllerInstaller(ControllerInstaller)
ControllerManager(ControllerManager)
CoreInstaller(CoreInstaller)
DIContainer(DIContainer)
DamageEnum(DamageEnum)
EcsInputBridge(EcsInputBridge)
EcsInstaller(EcsInstaller)
EntityHpChangedEvent(EntityHpChangedEvent)
EntityModel(EntityModel)
EventManager(EventManager)
GameFlowController(GameFlowController)
GameplayActions(GameplayActions)
GlobalTime(GlobalTime)
HudController(HudController)
IController(IController)
IControllerManager(IControllerManager)
IEcsInputBridge(IEcsInputBridge)
IEventCenter(IEventCenter)
IGameplayActions(IGameplayActions)
IInitializable(IInitializable)
IInstaller(IInstaller)
IPlayerInput(IPlayerInput)
IScope(IScope)
ISettingActions(ISettingActions)
ISettingInput(ISettingInput)
IStartable(IStartable)
ITickable(ITickable)
InjectAttribute(InjectAttribute)
InjectMember(InjectMember)
InjectOptionalAttribute(InjectOptionalAttribute)
InputInstaller(InputInstaller)
InstallerAsset(InstallerAsset)
InstallerConfig(InstallerConfig)
ModelBase(ModelBase)
ModelChangeType(ModelChangeType)
ModelChanged(ModelChanged)
MonoSingleton(MonoSingleton)
MoveSpeed(MoveSpeed)
MovementSystem(MovementSystem)
PlayerController(PlayerController)
PlayerInputActions(PlayerInputActions)
PlayerInputManager(PlayerInputManager)
PlayerInputSingletonTag(PlayerInputSingletonTag)
PlayerInputState(PlayerInputState)
PlayerInputStateAuthoring(PlayerInputStateAuthoring)
PlayerInputStateBaker(PlayerInputStateBaker)
PlayerTag(PlayerTag)
PlayerView(PlayerView)
ProjectBootstrap(ProjectBootstrap)
ProjectContext(ProjectContext)
SceneScopeRunner(SceneScopeRunner)
Scope(Scope)
ScopedServiceProvider(ScopedServiceProvider)
ServiceDescriptor(ServiceDescriptor)
ServiceLifetime(ServiceLifetime)
ServiceProviderExtensions(ServiceProviderExtensions)
SettingActions(SettingActions)
UpdateRunner(UpdateRunner)

AttachBuffEvent  --*  BuffType 
AttachBuffEvent  -..->  BuffType 
AttackConfig  -->  BuffConfig 
BuffConfig  -->  BuffEffectConfig 
BuffConfig  -..->  BuffType 
BuffEffectConfig  -..->  BuffEffectType 
ControllerBase  -..-|>  ControllerBase 
ControllerBase  -->  ControllerBase 
ControllerBase  -..-|>  IController 
ControllerBase  --*  IEventCenter 
ControllerBase  -..->  IEventCenter 
ControllerBase  --*  IEventCenter 
ControllerInstaller  -->  ControllerManager 
ControllerInstaller  -->  DIContainer 
ControllerInstaller  -->  IController 
ControllerInstaller  -->  IControllerManager 
ControllerInstaller  -->  IStartable 
ControllerInstaller  -..-|>  InstallerAsset 
ControllerInstaller  -->  PlayerController 
ControllerManager  -->  IController 
ControllerManager  -..-|>  IControllerManager 
ControllerManager  -..-|>  IStartable 
ControllerManager  -->  InjectAttribute 
CoreInstaller  -->  DIContainer 
CoreInstaller  -->  EcsInputBridge 
CoreInstaller  -->  EventManager 
CoreInstaller  -->  IEcsInputBridge 
CoreInstaller  -->  IEventCenter 
CoreInstaller  -->  IPlayerInput 
CoreInstaller  -..-|>  InstallerAsset 
CoreInstaller  -->  PlayerInputManager 
DIContainer  -->  DIContainer 
DIContainer  -..->  DIContainer 
DIContainer  -->  IScope 
DIContainer  -->  IScope 
DIContainer  -->  InjectAttribute 
DIContainer  -->  InjectMember 
DIContainer  -->  InjectMember 
DIContainer  -->  InjectOptionalAttribute 
DIContainer  -->  Scope 
DIContainer  -->  ScopedServiceProvider 
DIContainer  -->  ServiceDescriptor 
DIContainer  -->  ServiceDescriptor 
DIContainer  -->  ServiceLifetime 
EcsInputBridge  -..-|>  IEcsInputBridge 
EcsInputBridge  -->  PlayerInputSingletonTag 
EcsInputBridge  -->  PlayerInputState 
EcsInstaller  -->  DIContainer 
EcsInstaller  -..-|>  InstallerAsset 
EntityModel  -->  ModelBase 
EntityModel  -..-|>  ModelBase 
EntityModel  -->  ModelChangeType 
EntityModel  -->  ModelChanged 
EventManager  -..-|>  IEventCenter 
GameplayActions  -->  IGameplayActions 
GameplayActions  -..->  PlayerInputActions 
GameplayActions  --*  PlayerInputActions 
GameplayActions  -->  PlayerInputActions 
GlobalTime  -->  GlobalTime 
GlobalTime  -..-|>  MonoSingleton 
IControllerManager  -->  IController 
IInstaller  -->  DIContainer 
InputInstaller  -->  DIContainer 
InputInstaller  -->  IInitializable 
InputInstaller  -->  IPlayerInput 
InputInstaller  -..-|>  InstallerAsset 
InputInstaller  -->  PlayerInputManager 
InstallerAsset  -->  DIContainer 
InstallerAsset  -..-|>  IInstaller 
InstallerConfig  -->  InstallerAsset 
ModelBase  -->  ModelChanged 
ModelChanged  -..->  ModelChangeType 
ModelChanged  --*  ModelChangeType 
MovementSystem  -->  MoveSpeed 
MovementSystem  -->  PlayerInputState 
MovementSystem  -->  PlayerTag 
PlayerController  -->  AttackEvent 
PlayerController  -->  ControllerBase 
PlayerController  -..-|>  ControllerBase 
PlayerController  -->  ControllerBase 
PlayerController  -->  EntityHpChangedEvent 
PlayerController  -->  EntityModel 
PlayerController  --*  EntityModel 
PlayerController  -..->  IEcsInputBridge 
PlayerController  -->  IEcsInputBridge 
PlayerController  --*  IEcsInputBridge 
PlayerController  -->  IEventCenter 
PlayerController  --*  IEventCenter 
PlayerController  -..->  IPlayerInput 
PlayerController  -->  IPlayerInput 
PlayerController  --*  IPlayerInput 
PlayerController  -->  ModelBase 
PlayerController  -->  ModelChanged 
PlayerInputActions  -->  GameplayActions 
PlayerInputActions  -->  GameplayActions 
PlayerInputActions  -->  IGameplayActions 
PlayerInputActions  -->  ISettingActions 
PlayerInputActions  -->  SettingActions 
PlayerInputActions  -->  SettingActions 
PlayerInputManager  -->  GameplayActions 
PlayerInputManager  -..-|>  IInitializable 
PlayerInputManager  -..-|>  IPlayerInput 
PlayerInputManager  -..->  PlayerInputActions 
PlayerInputManager  -->  PlayerInputActions 
PlayerInputStateBaker  -->  PlayerInputSingletonTag 
PlayerInputStateBaker  -->  PlayerInputState 
PlayerInputStateBaker  -->  PlayerInputStateAuthoring 
ProjectBootstrap  -->  ProjectContext 
ProjectContext  -->  DIContainer 
ProjectContext  -..->  DIContainer 
ProjectContext  -->  IInitializable 
ProjectContext  -->  IStartable 
ProjectContext  -->  InstallerAsset 
ProjectContext  -->  InstallerConfig 
ProjectContext  -->  InstallerConfig 
ProjectContext  -..->  ProjectContext 
ProjectContext  -->  SceneScopeRunner 
SceneScopeRunner  -->  DIContainer 
SceneScopeRunner  -..->  DIContainer 
SceneScopeRunner  -->  IInitializable 
SceneScopeRunner  -..->  IScope 
SceneScopeRunner  -->  IScope 
SceneScopeRunner  -->  IStartable 
SceneScopeRunner  -->  InstallerAsset 
SceneScopeRunner  -..->  InstallerConfig 
SceneScopeRunner  -->  InstallerConfig 
Scope  -->  DIContainer 
Scope  --*  DIContainer 
Scope  -->  DIContainer 
Scope  -..-|>  IScope 
Scope  -->  ScopedServiceProvider 
ScopedServiceProvider  -->  DIContainer 
ScopedServiceProvider  -..->  Scope 
ScopedServiceProvider  -->  Scope 
ScopedServiceProvider  --*  Scope 
ServiceDescriptor  --*  ServiceLifetime 
ServiceDescriptor  -..->  ServiceLifetime 
ServiceDescriptor  -->  ServiceLifetime 
SettingActions  -->  ISettingActions 
SettingActions  -->  PlayerInputActions 
SettingActions  --*  PlayerInputActions 
SettingActions  -..->  PlayerInputActions 
UpdateRunner  -..->  UpdateRunner 
