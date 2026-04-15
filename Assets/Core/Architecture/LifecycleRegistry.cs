using System;
using System.Collections.Generic;
using System.Linq;
using Core.Architecture.Interfaces;
using Core.DI;
using UnityEngine;

namespace Core.Architecture
{
    /// <summary>
    /// 生命周期注册表 - 统一管理所有组件的生命周期
    /// 核心保证：所有Initialize完成 → 所有OnStart开始
    /// </summary>
    public static class LifecycleRegistry
    {
        #region 内部状态
        private static readonly List<IInitializable> _initializables = new();
        private static readonly List<IStartable> _startables = new();
        private static readonly List<object> _pendingInjection = new();

        private static bool _isInitializing = false;
        private static bool _isStarting = false;
        private static bool _initializationComplete = false;
        private static bool _startComplete = false;

        // 用于动态组件注册的队列
        private static readonly Queue<IInitializable> _pendingInitializables = new();
        private static readonly Queue<IStartable> _pendingStartables = new();

        private static DIContainer _container;
        private static IScope _projectScope;
        #endregion

        #region 公共API - 状态查询
        /// <summary>
        /// 是否正在执行Initialize阶段
        /// </summary>
        public static bool IsInitializing => _isInitializing;

        /// <summary>
        /// 是否正在执行OnStart阶段
        /// </summary>
        public static bool IsStarting => _isStarting;

        /// <summary>
        /// Initialize阶段是否已完成
        /// </summary>
        public static bool IsInitializationComplete => _initializationComplete;

        /// <summary>
        /// OnStart阶段是否已完成
        /// </summary>
        public static bool IsStartComplete => _startComplete;

        /// <summary>
        /// 获取当前注册的Initializable组件数量
        /// </summary>
        public static int InitializableCount => _initializables.Count;

        /// <summary>
        /// 获取当前注册的Startable组件数量
        /// </summary>
        public static int StartableCount => _startables.Count;

        /// <summary>
        /// 获取当前DI容器
        /// </summary>
        public static DIContainer GetContainer() => _container;
        #endregion

        #region 公共API - 核心功能
        /// <summary>
        /// 设置DI容器（由ProjectContext调用）
        /// </summary>
        public static void SetContainer(DIContainer container, IScope projectScope)
        {
            _container = container;
            _projectScope = projectScope;
        }

        /// <summary>
        /// 注册组件到生命周期系统
        /// </summary>
        public static void Register(object component)
        {
            if (component == null) return;

            lock (_initializables)
            {
                // 先尝试依赖注入
                TryInjectDependencies(component);

                // 注册 IInitializable
                if (component is IInitializable initializable && !_initializables.Contains(initializable))
                {
                    if (_isInitializing || _initializationComplete)
                    {
                        // 正在或已经完成初始化阶段，加入待处理队列
                        _pendingInitializables.Enqueue(initializable);
                        Debug.Log($"[Lifecycle] Component queued for delayed initialization: {GetComponentName(component)}");
                    }
                    else
                    {
                        _initializables.Add(initializable);
                    }
                }

                // 注册 IStartable
                if (component is IStartable startable && !_startables.Contains(startable))
                {
                    if (_isStarting || _startComplete)
                    {
                        // 正在或已经完成启动阶段，加入待处理队列
                        _pendingStartables.Enqueue(startable);
                        Debug.Log($"[Lifecycle] Component queued for delayed start: {GetComponentName(component)}");
                    }
                    else
                    {
                        _startables.Add(startable);
                    }
                }

                // 标记非MonoBehaviour、非生命周期接口的组件需要依赖注入
                if (!(component is MonoBehaviour) && component is not IInitializable and not IStartable)
                {
                    if (!_pendingInjection.Contains(component))
                        _pendingInjection.Add(component);
                }

                // 处理动态组件的立即执行
                HandleDynamicComponent(component);
            }
        }

        /// <summary>
        /// 注销组件
        /// </summary>
        public static void Unregister(object component)
        {
            if (component == null) return;

            lock (_initializables)
            {
                if (component is IInitializable initializable)
                    _initializables.Remove(initializable);

                if (component is IStartable startable)
                    _startables.Remove(startable);

                _pendingInjection.Remove(component);
            }
        }

        /// <summary>
        /// 执行所有组件的 Initialize 阶段
        /// </summary>
        public static void InitializeAll()
        {
            if (_isInitializing) return;

            _isInitializing = true;
            _initializationComplete = false;
            Debug.Log($"[Lifecycle] Starting Initialize phase ({_initializables.Count} components)");

            try
            {
                // 1. 处理延迟依赖注入
                ProcessDelayedInjection();

                // 2. 执行 Initialize
                var componentsToInitialize = new List<IInitializable>(_initializables);

                foreach (var component in componentsToInitialize)
                {
                    try
                    {
                        if (IsComponentReady(component))
                        {
                            component.Initialize();
                            Debug.Log($"[Lifecycle] Initialized: {GetComponentName(component)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Lifecycle] Initialize failed for {GetComponentName(component)}: {ex.Message}");
                    }
                }

                // 3. 处理动态注册的组件
                ProcessPendingRegistrations();

                _initializationComplete = true;
            }
            finally
            {
                _isInitializing = false;
                Debug.Log("[Lifecycle] Initialize phase completed");
            }
        }

        /// <summary>
        /// 执行所有组件的 OnStart 阶段
        /// 保证：所有Initialize已完成
        /// </summary>
        public static void StartAll()
        {
            if (_isStarting) return;
            if (_isInitializing)
                throw new InvalidOperationException("Cannot start while still initializing");

            if (!_initializationComplete)
            {
                Debug.LogWarning("[Lifecycle] Initialize not complete, calling InitializeAll first");
                InitializeAll();
            }

            _isStarting = true;
            _startComplete = false;
            Debug.Log($"[Lifecycle] Starting OnStart phase ({_startables.Count} components)");

            try
            {
                var componentsToStart = new List<IStartable>(_startables);

                foreach (var component in componentsToStart)
                {
                    try
                    {
                        component.OnStart();
                        Debug.Log($"[Lifecycle] Started: {GetComponentName(component)}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Lifecycle] OnStart failed for {GetComponentName(component)}: {ex.Message}");
                    }
                }

                // 处理动态注册的Startable组件
                ProcessPendingStartables();

                _startComplete = true;
            }
            finally
            {
                _isStarting = false;
                Debug.Log("[Lifecycle] OnStart phase completed");
            }
        }

        /// <summary>
        /// 清理所有注册的组件（场景卸载时调用）
        /// </summary>
        public static void Clear()
        {
            lock (_initializables)
            {
                _initializables.Clear();
                _startables.Clear();
                _pendingInjection.Clear();
                _pendingInitializables.Clear();
                _pendingStartables.Clear();
                _initializationComplete = false;
                _startComplete = false;
            }
            Debug.Log("[Lifecycle] Registry cleared");
        }
        #endregion

        #region 私有辅助方法
        private static void TryInjectDependencies(object component)
        {
            if (_container == null)
            {
                // 容器未就绪，标记为延迟注入
                lock (_initializables)
                {
                    if (!_pendingInjection.Contains(component))
                        _pendingInjection.Add(component);
                }
                return;
            }

            // 立即注入依赖
            try
            {
                _container.Inject(component, _projectScope);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Lifecycle] Dependency injection failed for {GetComponentName(component)}: {ex.Message}");
            }
        }

        private static void ProcessDelayedInjection()
        {
            if (_container == null) return;

            lock (_initializables)
            {
                foreach (var component in _pendingInjection.ToList())
                {
                    try
                    {
                        _container.Inject(component, _projectScope);
                        _pendingInjection.Remove(component);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Lifecycle] Dependency injection failed for {GetComponentName(component)}: {ex.Message}");
                    }
                }
            }
        }

        private static bool IsComponentReady(object component)
        {
            // 检查组件是否已准备好初始化
            // 这里可以扩展为检查依赖是否满足等逻辑
            return true;
        }

        private static string GetComponentName(object component)
        {
            return component?.GetType().Name ?? "null";
        }

        private static void ProcessPendingRegistrations()
        {
            // 处理动态注册的Initializable组件
            while (_pendingInitializables.Count > 0)
            {
                var component = _pendingInitializables.Dequeue();
                if (!_initializables.Contains(component))
                {
                    _initializables.Add(component);
                    try
                    {
                        component.Initialize();
                        Debug.Log($"[Lifecycle] Initialized (delayed): {GetComponentName(component)}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Lifecycle] Initialize failed for delayed {GetComponentName(component)}: {ex.Message}");
                    }
                }
            }
        }

        private static void ProcessPendingStartables()
        {
            // 处理动态注册的Startable组件
            while (_pendingStartables.Count > 0)
            {
                var component = _pendingStartables.Dequeue();
                if (!_startables.Contains(component))
                {
                    _startables.Add(component);
                    try
                    {
                        component.OnStart();
                        Debug.Log($"[Lifecycle] Started (delayed): {GetComponentName(component)}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Lifecycle] OnStart failed for delayed {GetComponentName(component)}: {ex.Message}");
                    }
                }
            }
        }

        private static void HandleDynamicComponent(object component)
        {
            // 处理动态组件的立即执行
            if (component is IInitializable initializable)
            {
                if (_initializationComplete && !_isInitializing)
                {
                    // 系统已完成初始化，立即调用Initialize
                    try
                    {
                        initializable.Initialize();
                        Debug.Log($"[Lifecycle] Dynamic component initialized immediately: {GetComponentName(component)}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Lifecycle] Immediate Initialize failed for dynamic component {GetComponentName(component)}: {ex.Message}");
                    }
                }
            }

            if (component is IStartable startable)
            {
                if (_startComplete && !_isStarting && _initializationComplete)
                {
                    // 系统已完成启动，立即调用OnStart
                    try
                    {
                        startable.OnStart();
                        Debug.Log($"[Lifecycle] Dynamic component started immediately: {GetComponentName(component)}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Lifecycle] Immediate OnStart failed for dynamic component {GetComponentName(component)}: {ex.Message}");
                    }
                }
            }
        }
        #endregion
    }
}