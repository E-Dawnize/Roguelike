using System;
using Core.Architecture.Interfaces;
using Core.DI;
using UnityEngine;

namespace Core.Architecture
{
    /// <summary>
    /// 严格的生命周期MonoBehaviour基类
    /// 强制使用DI生命周期接口，封存Unity原生Awake/Start方法
    /// </summary>
    public class StrictLifecycleMonoBehaviour : MonoBehaviour, IInitializable, IStartable
    {
        #region Unity原生方法封存
        /// <summary>
        /// 密封Unity Awake方法，防止子类误用
        /// 使用private确保子类无法override
        /// </summary>
        private void Awake()
        {
            // 1. 自动注册到生命周期系统
            LifecycleRegistry.Register(this);

            // 2. 自动尝试依赖注入
            //    - 如果DI容器已就绪，立即注入
            //    - 否则标记为延迟注入，在InitializeAll时处理

            // 3. 根据当前生命周期阶段决定是否立即执行
            //    - 如果系统已过Initialize阶段，立即调用Initialize()
            //    - 如果系统已过OnStart阶段，立即调用OnStart()
            HandleDynamicRegistration();
        }

        /// <summary>
        /// 密封Unity Start方法，强制使用OnStartExternal
        /// </summary>
        private void Start()
        {
            // 空实现，防止子类使用
            // 所有逻辑应迁移到OnStartExternal()
        }
        #endregion

        #region DI生命周期接口实现
        /// <summary>
        /// IInitializable.Initialize实现
        /// 调用受保护的OnInitialize()供子类重写
        /// </summary>
        void IInitializable.Initialize()
        {
            // 保证依赖已注入（通过LifecycleRegistry）
            OnInitialize();
        }

        /// <summary>
        /// IStartable.OnStart实现
        /// 调用受保护的OnStartExternal()供子类重写
        /// </summary>
        void IStartable.OnStart()
        {
            // 保证所有组件已Initialize（通过LifecycleRegistry）
            OnStartExternal();
        }
        #endregion

        #region 受保护的生命周期方法（供子类重写）
        /// <summary>
        /// 内部初始化阶段 - 组件自身状态准备
        /// 保证：依赖已注入完成
        /// 用途：初始化内部状态，设置默认值，验证配置
        /// </summary>
        protected virtual void OnInitialize()
        {
            // 子类可重写此方法实现内部初始化逻辑
        }

        /// <summary>
        /// 外部初始化阶段 - 开始与其他组件交互
        /// 保证：所有组件已完成Initialize
        /// 用途：注册事件监听，建立组件间连接，开始运行时逻辑
        /// </summary>
        protected virtual void OnStartExternal()
        {
            // 子类可重写此方法实现外部初始化逻辑
        }

        /// <summary>
        /// 组件销毁时的清理
        /// 注意：与Unity OnDestroy不同，此方法在生命周期系统注销前调用
        /// </summary>
        protected virtual void OnShutdown()
        {
            // 子类可重写此方法实现清理逻辑
        }
        #endregion

        #region 私有辅助方法
        /// <summary>
        /// 处理动态组件注册逻辑
        /// 根据当前系统状态决定是否立即执行生命周期方法
        /// </summary>
        private void HandleDynamicRegistration()
        {
            // 如果系统已经过了Initialize阶段，立即调用Initialize()
            if (LifecycleRegistry.IsInitializationComplete && !LifecycleRegistry.IsInitializing)
            {
                try
                {
                    // 直接调用接口实现，这会触发OnInitialize()
                    ((IInitializable)this).Initialize();
                    Debug.Log($"[Lifecycle] Dynamic component initialized immediately: {GetType().Name}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Lifecycle] Immediate Initialize failed for dynamic component {GetType().Name}: {ex.Message}");
                }
            }

            // 如果系统已经过了OnStart阶段，立即调用OnStart()
            if (LifecycleRegistry.IsStartComplete && !LifecycleRegistry.IsStarting && LifecycleRegistry.IsInitializationComplete)
            {
                try
                {
                    // 直接调用接口实现，这会触发OnStartExternal()
                    ((IStartable)this).OnStart();
                    Debug.Log($"[Lifecycle] Dynamic component started immediately: {GetType().Name}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Lifecycle] Immediate OnStart failed for dynamic component {GetType().Name}: {ex.Message}");
                }
            }
        }
        #endregion

        #region Unity生命周期
        /// <summary>
        /// Unity OnDestroy - 自动注销和清理
        /// </summary>
        private void OnDestroy()
        {
            // 1. 调用子类的清理逻辑
            OnShutdown();

            // 2. 从生命周期系统注销
            LifecycleRegistry.Unregister(this);
        }
        #endregion
        
    }
}