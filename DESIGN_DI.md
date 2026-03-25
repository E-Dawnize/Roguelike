# DI 与启动系统设计文档

本文档用于把当前项目的 DI、Installer、启动流程和场景作用域设计系统化记录下来，作为求职展示用的工程设计说明。内容基于现有代码与当前资源布局，并在“已知问题与改进计划”中明确指出下一步可优化点，强调工程可演进性。

## 1. 设计目标

- 明确 Composition Root，集中注册与初始化，避免隐式全局状态蔓延。
- 支持可重复、可预测的启动流程：注册、初始化、启动分离。
- 生命周期清晰：Singleton / Scoped / Transient，避免跨生命周期持有。
- 与 Unity 生命周期兼容：场景加载、卸载时自动建立/释放作用域。
- 强调可解释性与调试性：依赖图可追踪，注册顺序可控。

## 2. 关键概念

- `DIContainer`：核心容器，实现注册与解析。
- `ServiceDescriptor`：服务描述符，记录 ServiceType / Implementation / Lifetime / Order / Id。
- `IScope`：作用域抽象，负责 Scoped 生命周期隔离。
- `IInstaller`：注册单元，聚合一组服务注册逻辑。
- `InstallerAsset`：基于 `ScriptableObject` 的 Installer，允许用资源资产配置注册链。
- `InstallerConfig`：注册清单，区分 Global 与 Scene 两类 Installer。
- `ProjectContext`：全局 Composition Root，启动入口与全局容器持有者。
- `SceneScopeRunner`：场景级生命周期入口，负责创建/释放作用域。
- `IInitializable / IStartable`：可控生命周期接口，用于初始化与启动分阶段。

## 3. 代码与资源结构

- `Assets/Core/DI/DIContainer.cs`：DI 容器与作用域实现。
- `Assets/Core/Architecture/IInstaller.cs`：Installer 接口。
- `Assets/Core/Architecture/InstallerAsset.cs`：基于 SO 的 Installer（当前命名空间为 `Core.Architecture.Core.Architecture`）。
- `Assets/Core/Boot/InstallerConfig.cs`：Installer 清单。
- `Assets/Core/Boot/ProjectContext.cs`：全局启动与容器持有者。
- `Assets/Core/Boot/SceneScopeRunner.cs`：场景作用域运行器。
- `Assets/Core/Boot/ProjectBootstrap.cs`：运行时启动入口。
- `Assets/Resources/Configs/BootConfig.asset`：当前实际资源路径中的 InstallerConfig 资产。

## 4. 启动流程设计

### 4.1 全局启动（ProjectContext）

核心流程是“注册 -> 初始化 -> 启动”，由 `ProjectContext.Boot()` 调用：

```
Boot():
  _globalContainer = new DIContainer()
  config = Resources.Load<InstallerConfig>(...)
  InstallGlobal(config, _globalContainer)
  Initialize(_globalContainer)
  StartAll(_globalContainer)
```

设计动机：将注册阶段与运行阶段分离，避免注册时产生副作用，降低启动顺序不确定性。初始化与启动分离方便控制事件订阅、输入监听等外部副作用发生的时机。

### 4.2 场景启动（SceneScopeRunner）

场景生命周期通过 `SceneScopeRunner` 实现作用域隔离：

```
OnSceneLoaded:
  scope = globalContainer.CreateScope()
  InstallScene(config, scope)
  Initialize(scope)
  Start(scope)

OnSceneUnloaded:
  scope.Dispose()
```

设计动机：场景级对象天然是 Scoped 生命周期，加载即创建、卸载即释放；将其与全局单例隔离，避免场景对象泄漏到全局。

## 5. Installer 设计

### 5.1 IInstaller 与 InstallerAsset

- `IInstaller`：纯代码注册器，适合依赖复杂逻辑、构造参数或运行时对象。
- `InstallerAsset`：`ScriptableObject` 资产，可在编辑器内组合、排序注册链，便于可视化配置。

`InstallerAsset` 提供 `Order`，通过 `InstallerConfig` 排序实现可控注册顺序。设计动机是对复杂依赖图进行有序装配，降低 “先后顺序” 变成隐式约束的风险。

### 5.2 为什么使用 ScriptableObject

优点：

- 可在编辑器中集中配置，非程序员也可维护。
- 资产可复用（多场景、不同 Boot 方案）。
- 便于打包、版本控制与模块化拆分。

代价：

- 需要资源加载入口，运行时引用由资源系统驱动。
- 需要统一命名与路径，避免运行时加载失败。

### 5.3 资源加载策略

当前实现使用 `Resources.Load<InstallerConfig>("InstallerConfig")`，设计动机是“最简单可用”的启动加载方式，不依赖 Addressables 初始化，适合求职 Demo 与小型项目。

改进方向：

- 保留 `Resources` 作为早期引导配置。
- 中大型项目可迁移到 Addressables + Boot strap 两阶段启动。

## 6. DI 容器设计

### 6.1 ServiceDescriptor

当前描述符字段：

- `ServiceType`：服务接口或抽象类型。
- `ImplementationType` / `ImplementationInstance` / `ImplementationFactory`：三种实现来源。
- `Lifetime`：`Singleton` / `Scoped` / `Transient`。
- `Id`：全局唯一 ID，用于实例缓存键。
- `Order`：顺序字段，当前未被用于解析选择，预留用于多实现选择策略。

### 6.2 生命周期语义

- `Singleton`：全局唯一，存在于 `DIContainer._singletonInstances`。
- `Scoped`：每个作用域唯一，存在于 `Scope.ScopedInstances`。
- `Transient`：每次解析新实例，可进入作用域或全局释放队列。

规则约束：

- Singleton 不应依赖 Scoped。
- Scoped 可依赖 Singleton / Scoped。
- Transient 可依赖任何生命周期，但需注意反向持有导致的生命周期延长。

### 6.3 解析流程

解析核心逻辑：

```
Resolve(serviceType, scope):
  检查循环依赖
  若本容器注册 -> ResolveDescriptor
  否则 -> 递归父容器

ResolveDescriptor:
  取出 ServiceDescriptor
  根据 Lifetime 选择 ResolveSingleton/ResolveScope/ResolveTransient
```

构造策略：

- 若构造函数带 `[Inject]` 则优先。
- 否则使用参数最多的构造函数。
- 参数不存在则尝试默认值，否则抛错。

设计动机：优先显式构造意图，次之使用“最大依赖”构造减少歧义；遇到缺失依赖时立即失败，保证问题早暴露。

### 6.4 释放策略

容器与作用域都会收集 `IDisposable`：

- `DIContainer._disposables`：全局/无作用域实例。
- `Scope.Disposables`：作用域内实例。

Dispose 时统一释放，避免资源泄漏。

## 7. 作用域与子容器

### 7.1 作用域（Scope）

作用域的核心职责是生命周期隔离，而非注册隔离。`Scope` 持有：

- `ScopedInstances`：作用域内实例缓存。
- `Disposables`：作用域内可释放对象。
- `ServiceProvider`：解析入口，内部委托给容器。

### 7.2 子容器（Child Container）

`CreateChildContainer()` 提供注册隔离能力，适用于：

- 场景级需要覆盖全局注册（替换实现）。
- Additive 场景并行时的隔离注册。

子容器的成本是维护父子链解析规则与注册分层策略。当前代码仅实现父指针，不包含注册覆盖规则与冲突解决策略。

## 8. 注入策略

当前代码仅实现构造注入：`[Inject]` 仅用于构造函数选择，并未实现字段/属性注入。

推荐的完整注入策略：

- **构造注入**：用于纯 C# 服务与可控创建对象。
- **字段/属性注入**：用于 Unity 创建的对象（MonoBehaviour、ScriptableObject）。
- **显式 Inject(object)**：作为桥接方法，将容器依赖灌入现有实例。

设计动机：保持依赖声明清晰，同时适配 Unity 的对象生命周期与实例化机制。

## 9. 生命周期接口

项目定义了 `IInitializable` 与 `IStartable`：

- `Initialize()`：内部准备，不对外产生副作用。
- `OnStart()`：开始对外行为，如事件订阅、输入监听。

启动阶段使用：

```
ResolveAll<IInitializable> -> Initialize
ResolveAll<IStartable> -> Start
```

设计动机：将副作用延后至 Start，保证所有依赖注册完毕后再开启外部交互。

## 10. 现有实现中的已知问题

以下问题是“当前代码与设计目标的偏差”，适合作为面试改进点展示：

- `ProjectContext` 使用 `Unity.VisualScripting.IInitializable` 别名，与自定义接口冲突，需统一到 `Core.Architecture.IInitializable`。
- `Resources.Load("InstallerConfig")` 与实际资源路径 `Assets/Resources/Configs/BootConfig.asset` 不匹配，导致加载失败。
- `SceneScopeRunner` 未在 `ProjectContext` 中挂载，场景作用域流程未被触发。
- `SceneScopeRunner` 中 `_globalContainer` 标记了 `[Inject]`，但容器未实现字段注入且未主动注入。
- `SceneScopeRunner` 中 `installer.Register((DIContainer)_scope.ServiceProvider)` 的强转在运行时会失败。
- `InstallerAsset` 处于嵌套命名空间 `Core.Architecture.Core.Architecture`，可读性与引用成本较高。
- `ResolveAll` 与多实现选择策略尚不完善，`Order` 未被使用。

## 11. 改进路线（建议优先级）

1. **启动链闭环**  
   - 统一 `InstallerConfig` 资源路径与加载逻辑。  
   - 在 `ProjectContext.Boot()` 中挂载 `SceneScopeRunner`。  

2. **注入能力完善**  
   - 实现 `Inject(object, IScope?)`，支持字段/属性注入。  
   - 让 `SceneScopeRunner` 等 Unity 对象通过容器注入依赖。  

3. **多实现与顺序控制**  
   - `ResolveAll` 按 `Order` + 注册顺序排序。  
   - 支持 `GetService`/`GetRequiredService` 明确语义区分。  

4. **命名空间与结构整理**  
   - 修正 `InstallerAsset` 命名空间，减少双重命名。  
   - 清理重复/拼写错误的 `SceneSccopeRunner`。  

5. **一致的生命周期接口**  
   - 全项目统一使用 `Core.Architecture.IInitializable` / `IStartable`。  

## 12. 面试展示要点

- 强调 Composition Root 与分阶段启动设计，体现对依赖顺序与副作用控制的认识。
- 展示 “资源配置 + Installer” 的可视化装配方式，体现可维护性。
- 说明作用域设计如何解决场景级对象的生命周期与资源释放问题。
- 主动指出当前缺口并给出改进路线，体现工程判断与演进能力。
# DI 与启动系统设计文档

本文档用于把当前项目的 DI、Installer、启动流程和场景作用域设计系统化记录下来，作为求职展示用的工程设计说明。内容基于现有代码与当前资源布局，并在“已知问题与改进计划”中明确指出下一步可优化点，强调工程可演进性。

## 1. 设计目标

- 明确 Composition Root，集中注册与初始化，避免隐式全局状态蔓延。
- 支持可重复、可预测的启动流程：注册、初始化、启动分离。
- 生命周期清晰：Singleton / Scoped / Transient，避免跨生命周期持有。
- 与 Unity 生命周期兼容：场景加载、卸载时自动建立/释放作用域。
- 强调可解释性与调试性：依赖图可追踪，注册顺序可控。

## 2. 核心构件与角色

- `DIContainer`：核心容器，实现注册与解析。
- `ServiceDescriptor`：服务描述符，记录 ServiceType / Implementation / Lifetime / Order / Id。
- `IScope`：作用域抽象，负责 Scoped 生命周期隔离。
- `IInstaller`：注册单元，聚合一组服务注册逻辑。
- `InstallerAsset`：基于 `ScriptableObject` 的 Installer，允许用资源资产配置注册链。
- `InstallerConfig`：注册清单，区分 Global 与 Scene 两类 Installer。
- `ProjectContext`：全局 Composition Root，启动入口与全局容器持有者。
- `SceneScopeRunner`：场景级生命周期入口，负责创建/释放作用域。
- `IInitializable / IStartable`：可控生命周期接口，用于初始化与启动分阶段。

## 3. 代码与资源结构

- `Assets/Core/DI/DIContainer.cs`：DI 容器与作用域实现。
- `Assets/Core/Architecture/IInstaller.cs`：Installer 接口。
- `Assets/Core/Architecture/InstallerAsset.cs`：基于 SO 的 Installer。
- `Assets/Core/Boot/InstallerConfig.cs`：Installer 清单。
- `Assets/Core/Boot/ProjectContext.cs`：全局启动与容器持有者。
- `Assets/Core/Boot/SceneScopeRunner.cs`：场景作用域运行器。
- `Assets/Core/Boot/ProjectBootstrap.cs`：运行时启动入口。
- `Assets/Resources/Configs/BootConfig.asset`：当前实际资源路径中的 InstallerConfig 资产。

## 4. 启动流程设计

### 4.1 全局启动（ProjectContext）

核心流程是“注册 -> 初始化 -> 启动”，由 `ProjectContext.Boot()` 调用：

```
Boot():
  _globalContainer = new DIContainer()
  config = Resources.Load<InstallerConfig>(...)
  InstallGlobal(config, _globalContainer)
  Initialize(_globalContainer)
  StartAll(_globalContainer)
```

设计动机：将注册阶段与运行阶段分离，避免注册时产生副作用，降低启动顺序不确定性。初始化与启动分离方便控制事件订阅、输入监听等外部副作用发生的时机。

### 4.2 场景启动（SceneScopeRunner）

场景生命周期通过 `SceneScopeRunner` 实现作用域隔离：

```
OnSceneLoaded:
  scope = globalContainer.CreateScope()
  InstallScene(config, scope)
  InjectSceneObjects(scope)
  Initialize(scope)
  Start(scope)

OnSceneUnloaded:
  scope.Dispose()
```

设计动机：场景级对象天然是 Scoped 生命周期，加载即创建、卸载即释放；将其与全局单例隔离，避免场景对象泄漏到全局。

### 4.3 SceneScopeRunner 挂载与依赖来源

`SceneScopeRunner` 作为场景启动的入口，必须由全局启动链挂载（通常在 `ProjectContext.Boot()` 中创建并传入 `InstallerConfig`）。同时它依赖全局容器，因此需要一种“非构造注入”的手段提供 `_globalContainer` 引用。推荐方案如下：

- 由 `ProjectContext` 显式创建 `SceneScopeRunner` 后，调用容器的 `Inject(runner)`。
- 或者由 `ProjectContext` 直接赋值 `_globalContainer`（最简单但侵入）。

设计动机：Unity 创建的对象无法走构造注入，只能通过字段注入或手工赋值；显式 `Inject` 能维持依赖声明的语义一致性。

## 5. Installer 设计

### 5.1 IInstaller 与 InstallerAsset

- `IInstaller`：纯代码注册器，适合依赖复杂逻辑、构造参数或运行时对象。
- `InstallerAsset`：`ScriptableObject` 资产，可在编辑器内组合、排序注册链，便于可视化配置。

`InstallerAsset` 提供 `Order`，通过 `InstallerConfig` 排序实现可控注册顺序。设计动机是对复杂依赖图进行有序装配，降低 “先后顺序” 变成隐式约束的风险。

### 5.2 为什么使用 ScriptableObject

优点：

- 可在编辑器中集中配置，非程序员也可维护。
- 资产可复用（多场景、不同 Boot 方案）。
- 便于打包、版本控制与模块化拆分。

代价：

- 需要资源加载入口，运行时引用由资源系统驱动。
- 需要统一命名与路径，避免运行时加载失败。

### 5.3 资源加载策略

当前实现使用 `Resources.Load<InstallerConfig>("InstallerConfig")`，设计动机是“最简单可用”的启动加载方式，不依赖 Addressables 初始化，适合求职 Demo 与小型项目。

改进方向：

- 保留 `Resources` 作为早期引导配置。
- 中大型项目可迁移到 Addressables + Boot strap 两阶段启动。

## 6. DI 容器设计

### 6.1 ServiceDescriptor

当前描述符字段：

- `ServiceType`：服务接口或抽象类型。
- `ImplementationType` / `ImplementationInstance` / `ImplementationFactory`：三种实现来源。
- `Lifetime`：`Singleton` / `Scoped` / `Transient`。
- `Id`：全局唯一 ID，用于实例缓存键。
- `Order`：顺序字段，当前未被用于解析选择，预留用于多实现选择策略。

### 6.2 生命周期语义

- `Singleton`：全局唯一，存在于 `DIContainer._singletonInstances`。
- `Scoped`：每个作用域唯一，存在于 `Scope.ScopedInstances`。
- `Transient`：每次解析新实例，可进入作用域或全局释放队列。

规则约束：

- Singleton 不应依赖 Scoped。
- Scoped 可依赖 Singleton / Scoped。
- Transient 可依赖任何生命周期，但需注意反向持有导致的生命周期延长。

### 6.3 解析流程

解析核心逻辑：

```
Resolve(serviceType, scope):
  检查循环依赖
  若本容器注册 -> ResolveDescriptor
  否则 -> 递归父容器

ResolveDescriptor:
  取出 ServiceDescriptor
  根据 Lifetime 选择 ResolveSingleton/ResolveScope/ResolveTransient
```

构造策略：

- 若构造函数带 `[Inject]` 则优先。
- 否则使用参数最多的构造函数。
- 参数不存在则尝试默认值，否则抛错。

设计动机：优先显式构造意图，次之使用“最大依赖”构造减少歧义；遇到缺失依赖时立即失败，保证问题早暴露。

### 6.4 释放策略

容器与作用域都会收集 `IDisposable`：

- `DIContainer._disposables`：全局/无作用域实例。
- `Scope.Disposables`：作用域内实例。

Dispose 时统一释放，避免资源泄漏。

## 7. 作用域与子容器

### 7.1 作用域（Scope）

作用域的核心职责是生命周期隔离，而非注册隔离。`Scope` 持有：

- `ScopedInstances`：作用域内实例缓存。
- `Disposables`：作用域内可释放对象。
- `ServiceProvider`：解析入口，内部委托给容器。

### 7.2 子容器（Child Container）

`CreateChildContainer()` 提供注册隔离能力，适用于：

- 场景级需要覆盖全局注册（替换实现）。
- Additive 场景并行时的隔离注册。

子容器的成本是维护父子链解析规则与注册分层策略。当前代码仅实现父指针，不包含注册覆盖规则与冲突解决策略。

## 8. 注入策略

当前代码仅实现构造注入：`[Inject]` 仅用于构造函数选择，并未实现字段/属性注入。

推荐的完整注入策略：

- 构造注入：用于纯 C# 服务与可控创建对象。
- 字段/属性注入：用于 Unity 创建的对象（MonoBehaviour、ScriptableObject）。
- 显式 Inject(object)：作为桥接方法，将容器依赖灌入现有实例。

设计动机：保持依赖声明清晰，同时适配 Unity 的对象生命周期与实例化机制。

### 8.1 字段注入的规则与时机

字段/属性注入建议采用以下规则，避免 Unity 生命周期冲突：

- 仅注入标记 `[Inject]` 的字段/属性，不做全字段扫描。
- 注入发生在 Awake 之后、业务逻辑使用之前。
- 若字段依赖是 Scoped，则必须通过 `scope.ServiceProvider` 解析。

推荐的注入时机：

```
OnSceneLoaded:
  scope = globalContainer.CreateScope()
  RegisterSceneServices(scope)
  InjectSceneObjects(scope)
  Initialize(scope)
  Start(scope)
```

### 8.2 Inject(object) 的设计摘要

`Inject(object, IScope?)` 负责为已存在实例填充依赖，核心步骤：

1. 反射获取字段/属性上标记的 `[Inject]`。
2. 通过 `scope.ServiceProvider` 或全局容器解析依赖。
3. 若依赖缺失且字段非可选，抛出异常。
4. 将解析结果写回字段/属性。

设计动机：把“依赖提供”集中到容器层，避免业务层手工 `GetService` 造成服务定位器滥用。

### 8.3 场景实例注入示例（概念流程）

```
// 1) 查找/缓存场景对象
var targets = FindObjectsOfType<MonoBehaviour>(includeInactive: true);

// 2) 批量注入
foreach (var t in targets)
  container.Inject(t, scope);
```

这样 `UIController` 中的 `[Inject] private PlayerController player;` 会在场景加载后自动被填充，业务代码只需直接使用引用，无需显式 `GetService`。

## 9. 生命周期接口

项目定义了 `IInitializable` 与 `IStartable`：

- `Initialize()`：内部准备，不对外产生副作用。
- `OnStart()`：开始对外行为，如事件订阅、输入监听。

启动阶段使用：

```
ResolveAll<IInitializable> -> Initialize
ResolveAll<IStartable> -> Start
```

设计动机：将副作用延后至 Start，保证所有依赖注册完毕后再开启外部交互。

## 10. 现有实现中的已知问题

以下问题是“当前代码与设计目标的偏差”，适合作为面试改进点展示：

- `ProjectContext` 使用 `Unity.VisualScripting.IInitializable` 别名，与自定义接口冲突，需统一到 `Core.Architecture.IInitializable`。
- `Resources.Load("InstallerConfig")` 与实际资源路径 `Assets/Resources/Configs/BootConfig.asset` 不匹配，导致加载失败。
- `SceneScopeRunner` 未在 `ProjectContext` 中挂载，场景作用域流程未被触发。
- `SceneScopeRunner` 中 `_globalContainer` 标记了 `[Inject]`，但容器未实现字段注入且未主动注入。
- `SceneScopeRunner` 中 `installer.Register((DIContainer)_scope.ServiceProvider)` 的强转在运行时会失败。
- `InstallerAsset` 处于嵌套命名空间 `Core.Architecture.Core.Architecture`，可读性与引用成本较高。
- `ResolveAll` 与多实现选择策略尚不完善，`Order` 未被使用。

## 11. 改进路线（建议优先级）

1. 启动链闭环  
   统一 `InstallerConfig` 资源路径与加载逻辑。  
   在 `ProjectContext.Boot()` 中挂载 `SceneScopeRunner`。  

2. 注入能力完善  
   实现 `Inject(object, IScope?)`，支持字段/属性注入。  
   让 `SceneScopeRunner` 等 Unity 对象通过容器注入依赖。  

3. 多实现与顺序控制  
   `ResolveAll` 按 `Order` + 注册顺序排序。  
   支持 `GetService`/`GetRequiredService` 明确语义区分。  

4. 命名空间与结构整理  
   修正 `InstallerAsset` 命名空间，减少双重命名。  
   清理重复/拼写错误的 `SceneSccopeRunner`。  

5. 一致的生命周期接口  
   全项目统一使用 `Core.Architecture.IInitializable` / `IStartable`。  

## 12. 面试展示要点

- 强调 Composition Root 与分阶段启动设计，体现对依赖顺序与副作用控制的认识。
- 展示 “资源配置 + Installer” 的可视化装配方式，体现可维护性。
- 说明作用域设计如何解决场景级对象的生命周期与资源释放问题。
- 主动指出当前缺口并给出改进路线，体现工程判断与演进能力。
