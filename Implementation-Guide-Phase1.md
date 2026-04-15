# 阶段一实施指导：DI系统与基础绑定框架

## 概述

本指南基于《阶段一任务规划：DI系统与基础绑定框架》（`Task-Phase1-DI-Binding.md`）提供具体的实施步骤和代码示例。阶段一的目标是完成DI容器集成和基础MVVM绑定框架搭建，为后续ECS集成奠定基础。

## 实施前准备

### 1. 项目现状分析

检查现有代码状态：
- **DI系统**：`Assets/Core/DI/` 目录已包含基础DIContainer
- **MVVM基础**：`Assets/MVVM/` 目录包含ViewModelBase和绑定组件
- **绑定管理器**：`BindingManager.cs` 有基本框架但功能不完整
- **安装器系统**：`InstallerConfig` 等配置系统需要完善

### 2. 实施原则

根据项目工作模式：
- **不直接修改代码**：提供设计思路和代码示例
- **保持向后兼容**：新功能不破坏现有代码
- **渐进式实施**：按任务顺序逐步完善
- **充分测试**：每个功能完成后进行验证

## 任务1.1：DI容器完善与测试

### 子任务1：验证现有功能

#### 测试套件创建

创建 `DIContainerTestSuite.cs`：

```csharp
using System;
using Core.DI;
using NUnit.Framework;

namespace Tests.DI
{
    [TestFixture]
    public class DIContainerTestSuite
    {
        private DIContainer _container;
        
        [SetUp]
        public void Setup()
        {
            _container = new DIContainer();
        }
        
        [Test]
        public void Test_SingletonLifetime()
        {
            // 注册Singleton服务
            _container.Register<IService, ServiceImpl>(ServiceLifetime.Singleton);
            
            // 两次解析应该是同一个实例
            var instance1 = _container.Resolve<IService>();
            var instance2 = _container.Resolve<IService>();
            
            Assert.IsNotNull(instance1);
            Assert.AreSame(instance1, instance2);
        }
        
        [Test]
        public void Test_TransientLifetime()
        {
            // 注册Transient服务
            _container.Register<IService, ServiceImpl>(ServiceLifetime.Transient);
            
            // 两次解析应该是不同实例
            var instance1 = _container.Resolve<IService>();
            var instance2 = _container.Resolve<IService>();
            
            Assert.IsNotNull(instance1);
            Assert.IsNotNull(instance2);
            Assert.AreNotSame(instance1, instance2);
        }
        
        [Test]
        public void Test_ScopedLifetime()
        {
            // 注册Scoped服务
            _container.Register<IService, ServiceImpl>(ServiceLifetime.Scoped);
            
            // 在同一Scope内是相同实例
            using (var scope = _container.CreateScope())
            {
                var instance1 = scope.ServiceProvider.GetService<IService>();
                var instance2 = scope.ServiceProvider.GetService<IService>();
                Assert.AreSame(instance1, instance2);
            }
            
            // 不同Scope是不同实例
            using (var scope1 = _container.CreateScope())
            using (var scope2 = _container.CreateScope())
            {
                var instance1 = scope1.ServiceProvider.GetService<IService>();
                var instance2 = scope2.ServiceProvider.GetService<IService>();
                Assert.AreNotSame(instance1, instance2);
            }
        }
        
        [Test]
        public void Test_ConstructorInjection()
        {
            // 注册依赖服务
            _container.Register<IDependency, DependencyImpl>(ServiceLifetime.Transient);
            _container.Register<IServiceWithDependency, ServiceWithDependencyImpl>(ServiceLifetime.Transient);
            
            // 应该正确注入依赖
            var service = _container.Resolve<IServiceWithDependency>();
            Assert.IsNotNull(service);
            Assert.IsNotNull(service.Dependency);
        }
        
        [Test]
        public void Test_FieldInjection()
        {
            // 注册服务
            _container.Register<IDependency, DependencyImpl>(ServiceLifetime.Transient);
            
            // 创建需要字段注入的对象
            var target = new ClassWithFieldInjection();
            
            // 执行字段注入
            _container.Inject(target);
            
            // 验证字段被正确注入
            Assert.IsNotNull(target.Dependency);
        }
        
        [Test]
        public void Test_FactoryMethodRegistration()
        {
            // 使用工厂方法注册
            _container.Register<IService>(provider => new ServiceImpl(), ServiceLifetime.Singleton);
            
            var service = _container.Resolve<IService>();
            Assert.IsNotNull(service);
            Assert.IsInstanceOf<ServiceImpl>(service);
        }
        
        [Test]
        public void Test_CircularDependencyDetection()
        {
            // 注册循环依赖
            _container.Register<ICircularA, CircularA>(ServiceLifetime.Transient);
            _container.Register<ICircularB, CircularB>(ServiceLifetime.Transient);
            
            // 应该检测到循环依赖并抛出异常
            Assert.Throws<InvalidOperationException>(() => _container.Resolve<ICircularA>());
        }
    }
    
    // 测试接口和实现类
    public interface IService { }
    public class ServiceImpl : IService { }
    
    public interface IDependency { }
    public class DependencyImpl : IDependency { }
    
    public interface IServiceWithDependency
    {
        IDependency Dependency { get; }
    }
    
    public class ServiceWithDependencyImpl : IServiceWithDependency
    {
        public IDependency Dependency { get; }
        
        public ServiceWithDependencyImpl(IDependency dependency)
        {
            Dependency = dependency;
        }
    }
    
    public class ClassWithFieldInjection
    {
        [Inject]
        public IDependency Dependency;
    }
    
    public interface ICircularA { }
    public interface ICircularB { }
    
    public class CircularA : ICircularA
    {
        public CircularA(ICircularB b) { }
    }
    
    public class CircularB : ICircularB
    {
        public CircularB(ICircularA a) { }
    }
}
```

#### 性能测试示例

创建 `DIPerformanceTests.cs`：

```csharp
using System.Diagnostics;
using Core.DI;
using NUnit.Framework;

namespace Tests.DI
{
    [TestFixture]
    public class DIPerformanceTests
    {
        private DIContainer _container;
        private const int Iterations = 10000;
        
        [SetUp]
        public void Setup()
        {
            _container = new DIContainer();
            
            // 注册测试服务
            for (int i = 0; i < 100; i++)
            {
                var serviceType = typeof(IService) + i.ToString();
                var implType = typeof(ServiceImpl) + i.ToString();
                
                // 使用反射动态注册（实际项目应使用接口）
                _container.Register(
                    typeof(IService), 
                    typeof(ServiceImpl), 
                    ServiceLifetime.Transient);
            }
        }
        
        [Test]
        public void Test_ResolvePerformance()
        {
            var stopwatch = Stopwatch.StartNew();
            
            for (int i = 0; i < Iterations; i++)
            {
                var service = _container.Resolve<IService>();
            }
            
            stopwatch.Stop();
            var averageTime = stopwatch.ElapsedMilliseconds / (double)Iterations;
            
            Assert.Less(averageTime, 1.0, 
                $"平均解析时间 {averageTime:F3}ms 超过 1ms 限制");
            
            Debug.Log($"DI解析性能：{Iterations}次调用，平均 {averageTime:F3}ms/次");
        }
    }
}
```

### 子任务2：添加缺失功能

#### Scope嵌套支持

扩展 `DIContainer.cs` 中的Scope实现：

```csharp
public class Scope : IScope
{
    private readonly DIContainer _container;
    private readonly Scope _parentScope;
    private readonly Dictionary<Type, object> _scopedInstances = new();
    public readonly List<IDisposable> Disposables = new();
    
    public IServiceProvider ServiceProvider => this;
    
    // 支持嵌套Scope
    public Scope(DIContainer container, Scope parentScope = null)
    {
        _container = container;
        _parentScope = parentScope;
    }
    
    public object GetService(Type serviceType)
    {
        // 1. 检查当前Scope
        if (_scopedInstances.TryGetValue(serviceType, out var instance))
            return instance;
        
        // 2. 检查父级Scope（嵌套Scope支持）
        if (_parentScope != null)
        {
            var parentInstance = _parentScope.GetService(serviceType);
            if (parentInstance != null)
                return parentInstance;
        }
        
        // 3. 从容器解析新实例
        var descriptor = _container.GetDescriptor(serviceType);
        if (descriptor == null)
            return null;
        
        if (descriptor.Lifetime == ServiceLifetime.Scoped)
        {
            instance = _container.CreateInstance(descriptor, this);
            _scopedInstances[serviceType] = instance;
            return instance;
        }
        
        // 4. Singleton或Transient委托给容器
        return _container.ResolveService(serviceType, this);
    }
    
    public void Dispose()
    {
        // 先释放子级Scope
        // ...（如果有子Scope管理的逻辑）
        
        // 释放当前Scope的资源
        foreach (var disposable in Disposables)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogError($"释放资源时出错: {ex.Message}");
            }
        }
        
        _scopedInstances.Clear();
        Disposables.Clear();
    }
}
```

#### 循环依赖检测

在 `DIContainer.cs` 中添加检测逻辑：

```csharp
public class DIContainer
{
    private readonly HashSet<Type> _resolvingTypes = new();
    
    private object ResolveService(Type serviceType, Scope scope)
    {
        // 循环依赖检测
        if (_resolvingTypes.Contains(serviceType))
        {
            throw new InvalidOperationException(
                $"检测到循环依赖: {string.Join(" -> ", _resolvingTypes)} -> {serviceType}");
        }
        
        try
        {
            _resolvingTypes.Add(serviceType);
            
            var descriptor = GetDescriptor(serviceType);
            if (descriptor == null)
                return null;
                
            return CreateInstance(descriptor, scope);
        }
        finally
        {
            _resolvingTypes.Remove(serviceType);
        }
    }
    
    // 可视化循环依赖的工具方法
    public string GetDependencyGraph(Type serviceType)
    {
        var visited = new HashSet<Type>();
        var stack = new Stack<Type>();
        var graph = new StringBuilder();
        
        BuildDependencyGraph(serviceType, visited, stack, graph, 0);
        
        return graph.ToString();
    }
    
    private void BuildDependencyGraph(Type type, HashSet<Type> visited, 
        Stack<Type> stack, StringBuilder graph, int depth)
    {
        if (visited.Contains(type))
        {
            // 发现循环
            graph.AppendLine($"{new string(' ', depth * 2)}↳ [循环] {type.Name}");
            return;
        }
        
        visited.Add(type);
        stack.Push(type);
        
        graph.AppendLine($"{new string(' ', depth * 2)}{type.Name}");
        
        var descriptor = GetDescriptor(type);
        if (descriptor?.ImplementationType != null)
        {
            var constructor = GetConstructor(descriptor.ImplementationType);
            foreach (var param in constructor.GetParameters())
            {
                BuildDependencyGraph(param.ParameterType, visited, stack, graph, depth + 1);
            }
        }
        
        stack.Pop();
        visited.Remove(type);
    }
}
```

### 子任务3：性能优化

#### 表达式树编译优化

在 `DIContainer.cs` 中优化构造函数调用：

```csharp
public class DIContainer
{
    private readonly ConcurrentDictionary<Type, Func<object[], object>> _constructorCache = new();
    
    private object CreateInstance(ServiceDescriptor descriptor, Scope scope)
    {
        if (descriptor.ImplementationFactory != null)
            return descriptor.ImplementationFactory(this);
            
        if (descriptor.ImplementationInstance != null)
            return descriptor.ImplementationInstance;
            
        var implementationType = descriptor.ImplementationType;
        var constructor = GetConstructor(implementationType);
        
        // 使用表达式树编译构造函数调用
        var constructorInvoker = _constructorCache.GetOrAdd(
            implementationType, 
            type => CompileConstructorInvoker(constructor));
        
        var parameters = constructor.GetParameters();
        var arguments = new object[parameters.Length];
        
        for (int i = 0; i < parameters.Length; i++)
        {
            arguments[i] = ResolveService(parameters[i].ParameterType, scope);
        }
        
        var instance = constructorInvoker(arguments);
        
        // 执行字段注入
        if (descriptor.Lifetime != ServiceLifetime.Transient || scope == null)
        {
            Inject(instance, scope);
        }
        
        return instance;
    }
    
    private static Func<object[], object> CompileConstructorInvoker(ConstructorInfo constructor)
    {
        var parameters = constructor.GetParameters();
        var argsParam = Expression.Parameter(typeof(object[]), "args");
        
        var argsExpressions = new Expression[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var index = Expression.Constant(i);
            var parameterType = parameters[i].ParameterType;
            var argAccess = Expression.ArrayIndex(argsParam, index);
            argsExpressions[i] = Expression.Convert(argAccess, parameterType);
        }
        
        var newExpression = Expression.New(constructor, argsExpressions);
        var lambda = Expression.Lambda<Func<object[], object>>(
            Expression.Convert(newExpression, typeof(object)), 
            argsParam);
        
        return lambda.Compile();
    }
}
```

## 任务1.2：安装器系统重构

### 子任务1：InstallerConfig完善

扩展 `InstallerConfig.cs`：

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Architecture
{
    public enum EnvironmentType
    {
        Development,
        Testing,
        Production
    }
    
    [CreateAssetMenu(fileName = "InstallerConfig", menuName = "DI/Installer Config")]
    public class InstallerConfig : ScriptableObject
    {
        [Header("环境配置")]
        public EnvironmentType CurrentEnvironment = EnvironmentType.Development;
        
        [Header("安装器列表")]
        public List<InstallerEntry> Installers = new();
        
        [Header("环境特定配置")]
        public EnvironmentConfig DevelopmentConfig;
        public EnvironmentConfig TestingConfig;
        public EnvironmentConfig ProductionConfig;
        
        public EnvironmentConfig GetCurrentEnvironmentConfig()
        {
            return CurrentEnvironment switch
            {
                EnvironmentType.Development => DevelopmentConfig,
                EnvironmentType.Testing => TestingConfig,
                EnvironmentType.Production => ProductionConfig,
                _ => DevelopmentConfig
            };
        }
        
        public List<InstallerAsset> GetInstallersForCurrentEnvironment()
        {
            var result = new List<InstallerAsset>();
            
            foreach (var entry in Installers)
            {
                // 检查环境条件
                if (!IsEntryEnabledForEnvironment(entry))
                    continue;
                    
                // 检查其他条件
                if (!IsEntryEnabled(entry))
                    continue;
                    
                result.Add(entry.Installer);
            }
            
            // 按Order排序
            result.Sort((a, b) => a.Order.CompareTo(b.Order));
            
            return result;
        }
        
        private bool IsEntryEnabledForEnvironment(InstallerEntry entry)
        {
            return entry.EnabledEnvironments.HasFlag(CurrentEnvironment);
        }
        
        private bool IsEntryEnabled(InstallerEntry entry)
        {
            // 可以扩展其他条件检查
            // 如：平台条件、功能开关等
            return entry.Enabled;
        }
    }
    
    [Serializable]
    public class InstallerEntry
    {
        public InstallerAsset Installer;
        public int Order = 0;
        public bool Enabled = true;
        
        [EnumFlags]
        public EnvironmentType EnabledEnvironments = 
            EnvironmentType.Development | EnvironmentType.Testing | EnvironmentType.Production;
        
        // 可以添加更多条件字段
        public RuntimePlatform[] EnabledPlatforms;
        public string RequiredFeatureFlag;
    }
    
    [Serializable]
    public class EnvironmentConfig
    {
        public bool EnableDebugTools = true;
        public bool EnableProfiling = false;
        public LogLevel LogLevel = LogLevel.Info;
        public List<ServiceOverride> ServiceOverrides = new();
    }
    
    [Serializable]
    public class ServiceOverride
    {
        public TypeReference ServiceType;
        public TypeReference ImplementationType;
        public ServiceLifetime Lifetime = ServiceLifetime.Singleton;
    }
    
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}
```

### 子任务2：编辑器扩展

创建 `InstallerConfigEditor.cs`：

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace Core.Architecture.Editor
{
    [CustomEditor(typeof(InstallerConfig))]
    public class InstallerConfigEditor : UnityEditor.Editor
    {
        private InstallerConfig _config;
        private Vector2 _scrollPosition;
        private bool _showEnvironmentSettings = true;
        private bool _showInstallerList = true;
        private bool _showAdvancedSettings = false;
        
        public override void OnInspectorGUI()
        {
            _config = (InstallerConfig)target;
            
            EditorGUILayout.Space(10);
            
            // 环境选择
            EditorGUILayout.LabelField("环境配置", EditorStyles.boldLabel);
            _config.CurrentEnvironment = (EnvironmentType)EditorGUILayout.EnumPopup(
                "当前环境", _config.CurrentEnvironment);
            
            EditorGUILayout.Space(10);
            
            // 安装器列表
            _showInstallerList = EditorGUILayout.Foldout(_showInstallerList, "安装器列表", true);
            if (_showInstallerList)
            {
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(300));
                
                for (int i = 0; i < _config.Installers.Count; i++)
                {
                    DrawInstallerEntry(i);
                }
                
                EditorGUILayout.EndScrollView();
                
                // 添加/删除按钮
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("添加安装器"))
                {
                    _config.Installers.Add(new InstallerEntry());
                }
                
                if (GUILayout.Button("清理空项"))
                {
                    _config.Installers.RemoveAll(e => e.Installer == null);
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.Space(10);
            
            // 环境特定配置
            _showEnvironmentSettings = EditorGUILayout.Foldout(_showEnvironmentSettings, "环境配置", true);
            if (_showEnvironmentSettings)
            {
                DrawEnvironmentConfig("开发环境", ref _config.DevelopmentConfig);
                DrawEnvironmentConfig("测试环境", ref _config.TestingConfig);
                DrawEnvironmentConfig("生产环境", ref _config.ProductionConfig);
            }
            
            EditorGUILayout.Space(10);
            
            // 依赖关系图按钮
            if (GUILayout.Button("生成依赖关系图"))
            {
                GenerateDependencyGraph();
            }
            
            // 验证配置按钮
            if (GUILayout.Button("验证配置"))
            {
                ValidateConfiguration();
            }
            
            if (GUI.changed)
            {
                EditorUtility.SetDirty(_config);
            }
        }
        
        private void DrawInstallerEntry(int index)
        {
            var entry = _config.Installers[index];
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            
            // 折叠头
            entry.Installer = (InstallerAsset)EditorGUILayout.ObjectField(
                entry.Installer, typeof(InstallerAsset), false);
            
            // 删除按钮
            if (GUILayout.Button("×", GUILayout.Width(20)))
            {
                _config.Installers.RemoveAt(index);
                return;
            }
            
            EditorGUILayout.EndHorizontal();
            
            if (entry.Installer != null)
            {
                // 详细设置
                entry.Order = EditorGUILayout.IntField("执行顺序", entry.Order);
                entry.Enabled = EditorGUILayout.Toggle("启用", entry.Enabled);
                
                // 环境条件
                EditorGUILayout.LabelField("启用环境:");
                EditorGUILayout.BeginHorizontal();
                
                var envs = System.Enum.GetValues(typeof(EnvironmentType)).Cast<EnvironmentType>();
                foreach (var env in envs)
                {
                    bool isEnabled = entry.EnabledEnvironments.HasFlag(env);
                    bool newEnabled = EditorGUILayout.ToggleLeft(env.ToString(), isEnabled);
                    
                    if (isEnabled != newEnabled)
                    {
                        if (newEnabled)
                            entry.EnabledEnvironments |= env;
                        else
                            entry.EnabledEnvironments &= ~env;
                    }
                }
                
                EditorGUILayout.EndHorizontal();
                
                // 显示安装器信息
                EditorGUILayout.LabelField($"服务数: {entry.Installer.Services.Count}", 
                    EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawEnvironmentConfig(string label, ref EnvironmentConfig config)
        {
            if (config == null)
                config = new EnvironmentConfig();
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            
            config.EnableDebugTools = EditorGUILayout.Toggle("启用调试工具", config.EnableDebugTools);
            config.EnableProfiling = EditorGUILayout.Toggle("启用性能分析", config.EnableProfiling);
            config.LogLevel = (LogLevel)EditorGUILayout.EnumPopup("日志级别", config.LogLevel);
            
            EditorGUILayout.EndVertical();
        }
        
        private void GenerateDependencyGraph()
        {
            // 生成依赖关系图逻辑
            Debug.Log("生成依赖关系图...");
            // 实际实现会使用GraphView API或生成图片
        }
        
        private void ValidateConfiguration()
        {
            // 验证配置逻辑
            var issues = new List<string>();
            
            // 检查循环依赖
            // 检查服务冲突
            // 检查环境配置
            
            if (issues.Count == 0)
            {
                Debug.Log("配置验证通过");
            }
            else
            {
                Debug.LogWarning($"发现 {issues.Count} 个问题:");
                foreach (var issue in issues)
                {
                    Debug.LogWarning($"  - {issue}");
                }
            }
        }
    }
}
#endif
```

由于文档长度限制，这里只提供了部分实施指导。完整的实施指南应包含：

## 后续内容概览

### 任务1.3：ViewModelBase完善
- 属性通知系统增强
- 命令系统实现（AsyncCommand、RelayCommand）
- ViewModel生命周期管理
- 验证系统实现（IDataErrorInfo）

### 任务1.4：基础绑定组件开发
- PropertyBinding双向绑定实现
- CommandBinding命令绑定
- 值转换器系统
- 编辑器集成工具

### 任务1.5：绑定管理器原型
- BindingManager完整实现
- BindingRegistry注册表
- 上下文分组管理
- 生命周期自动清理

## 实施建议

### 1. 实施顺序
1. 先完成DI系统完善（任务1.1）
2. 再实现ViewModelBase增强（任务1.3）
3. 然后完善绑定组件（任务1.4）
4. 最后实现绑定管理器（任务1.5）

### 2. 测试策略
- 每个功能点编写单元测试
- 重要集成点进行集成测试
- 使用性能测试确保性能达标
- 定期运行自动化测试

### 3. 代码质量
- 遵循现有代码风格
- 添加必要的注释和文档
- 保持向后兼容性
- 进行代码审查

## 验收检查点

完成阶段一后，检查以下验收标准：

### DI系统
- [ ] 所有生命周期模式测试通过
- [ ] Scope嵌套正常工作
- [ ] 循环依赖检测正确报告错误
- [ ] 服务解析性能达标（<1ms/次）

### MVVM框架
- [ ] 属性变更正确触发UI更新
- [ ] 命令可绑定到UI控件
- [ ] ViewModel可正确清理资源
- [ ] 验证系统正常工作

### 绑定系统
- [ ] 属性绑定正确同步数据
- [ ] 命令绑定正确触发ViewModel逻辑
- [ ] 绑定管理器可管理所有绑定
- [ ] 编辑器工具可配置绑定

## 下一步准备

完成阶段一后，可以开始阶段二：ECS系统集成。参考 `Task-Phase2-ECS-Integration.md` 和 `DOTS-Integration-Design.md` 进行准备。

---
*文档版本：1.0*
*最后更新：2026-04-12*
*相关文档：Task-Phase1-DI-Binding.md, DI-System-Design.md, MVVM-Framework-Design.md*
