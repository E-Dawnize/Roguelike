using System.Linq;
using Core.Architecture;
using Core.Architecture.Interfaces;
using Core.DI;
using UnityEngine;

namespace Core.Boot
{
    /// <summary>
    /// 项目启动入口 - 集成新的生命周期管理系统
    /// 核心职责：
    /// 1. 初始化DI容器
    /// 2. 设置LifecycleRegistry
    /// 3. 协调全局生命周期执行顺序
    /// 4. 管理项目级Scope
    /// </summary>
    public class ProjectContext : MonoBehaviour
    {
        private static ProjectContext _instance;
        private DIContainer _globalContainer;
        private IScope _projectScope;

        /// <summary>
        /// 确保ProjectContext存在并已启动
        /// </summary>
        public static void Ensure()
        {
            if (_instance != null) return;

            var go = new GameObject("ProjectContext");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ProjectContext>();
            _instance.Boot();
        }

        /// <summary>
        /// 获取当前DI容器（供其他系统使用）
        /// </summary>
        public static DIContainer GetContainer() => _instance?._globalContainer;

        /// <summary>
        /// 获取项目级Scope
        /// </summary>
        public static IScope GetProjectScope() => _instance?._projectScope;

        private void Boot()
        {
            Debug.Log("[ProjectContext] Starting boot sequence...");

            // 阶段1: 创建DI容器和Scope
            CreateDIContainer();

            // 阶段2: 注册全局安装器
            RegisterInstallers();

            // 阶段3: 设置LifecycleRegistry
            SetupLifecycleRegistry();

            // 阶段4: 执行生命周期初始化
            ExecuteLifecycle();

            // 阶段5: 启动游戏循环
            StartGameLoop();

            Debug.Log("[ProjectContext] Boot sequence completed");
        }

        #region 启动阶段实现
        private void CreateDIContainer()
        {
            _globalContainer = new DIContainer();
            _projectScope = _globalContainer.CreateScope();
            Debug.Log("[ProjectContext] DI container and project scope created");
        }

        private void RegisterInstallers()
        {
            var config = LoadInstallerConfig();
            if (config != null)
            {
                foreach (var installer in config.GlobalInstallersSorted)
                {
                    installer.Register(_globalContainer);
                }
                Debug.Log($"[ProjectContext] Registered {config.GlobalInstallersSorted.Count()} global installers");
            }
            else
            {
                Debug.LogWarning("[ProjectContext] No installer config found");
            }
        }

        private void SetupLifecycleRegistry()
        {
            // 设置LifecycleRegistry使用我们的DI容器
            LifecycleRegistry.SetContainer(_globalContainer, _projectScope);
            Debug.Log("[ProjectContext] LifecycleRegistry configured");
        }

        private void ExecuteLifecycle()
        {
            // 关键保证：所有Initialize完成 → 所有OnStart开始

            // 1. 初始化阶段
            Debug.Log("[ProjectContext] Starting Initialize phase...");
            LifecycleRegistry.InitializeAll();

            // 2. 启动阶段
            Debug.Log("[ProjectContext] Starting OnStart phase...");
            LifecycleRegistry.StartAll();
        }

        private void StartGameLoop()
        {
            // 注册ITickable组件到UpdateRunner
            var updateRunner = GetComponent<UpdateRunner>();
            if (updateRunner == null)
            {
                updateRunner = gameObject.AddComponent<UpdateRunner>();
            }

            // 获取所有ITickable服务并注册
            var tickables = _globalContainer.ResolveAll<ITickable>(_projectScope);
            foreach (var tickable in tickables)
            {
                updateRunner.Register(tickable);
            }

            Debug.Log($"[ProjectContext] Game loop started with {tickables.Count()} tickable components");
        }
        #endregion

        #region 辅助方法
        private InstallerConfig LoadInstallerConfig()
        {
            return Resources.Load<InstallerConfig>("Configs/BootConfig");
        }
        #endregion

        #region 清理
        private void OnDestroy()
        {
            // 清理LifecycleRegistry
            LifecycleRegistry.Clear();

            // 释放DI容器
            _projectScope?.Dispose();
            _globalContainer?.Dispose();

            Debug.Log("[ProjectContext] Cleanup completed");
        }
        #endregion
    }
}