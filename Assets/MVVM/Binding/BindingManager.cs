using System;
using System.Collections.Generic;
using System.Linq;
using Core.Boot;
using Core.Events.EventInterfaces;
using MVVM.Binding.Interfaces;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MVVM.Binding
{
    public class BindingManager:IBindingManager
    {
        private List<IBinding> Bindings { get; } = new List<IBinding>();
        private IEventCenter _eventCenter;
        private Dictionary<object, List<IBinding>> _bindingsByContext = new();
        private Dictionary<GameObject, List<IBinding>> _bindingsByGameObject = new();
        private readonly object _lock = new object();
        private string _debugInfoCache;
        private bool _debugInfoDirty = true;

        public BindingManager(IEventCenter eventCenter)
        {
            _eventCenter = eventCenter;

            // 可选：监听场景卸载事件
            // SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        #region 注册绑定
        public void RegisterBinding(IBinding binding, object context = null)
        {
            lock (_lock)
            {
                Bindings.Add(binding);
                if (context != null)
                {
                    if (!_bindingsByContext.TryGetValue(context, out var list))
                    {
                        list = new List<IBinding>();
                        _bindingsByContext[context] = list;
                    }
                    list.Add(binding);
                }
                if (binding is MonoBehaviour monoBinding)
                {
                    var gameObject = monoBinding.gameObject;
                    if (!_bindingsByGameObject.TryGetValue(gameObject, out var goList))
                    {
                        goList = new List<IBinding>();
                        _bindingsByGameObject[gameObject] = goList;
                    }
                    goList.Add(binding);
                }

                _debugInfoDirty = true;
            }
        }

        public void UnregisterBinding(IBinding binding, object context = null)
        {
            lock (_lock)
            {
                Bindings.Remove(binding);
                foreach (var kvp in _bindingsByContext)
                {
                    kvp.Value.Remove(binding);
                }

                // 从GameObject字典中移除
                foreach (var kvp in _bindingsByGameObject)
                {
                    kvp.Value.Remove(binding);
                }

                // 清理空列表
                CleanupEmptyCollections();

                _debugInfoDirty = true;
            }
        }
        #endregion

        #region 绑定
        
        public void BindAllInContext(object context)
        {
            if (context == null || !_bindingsByContext.TryGetValue(context, out var bindings))
                return;

            foreach (var binding in bindings)
            {
                try
                {
                    binding.Bind();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"绑定失败: {binding.GetType().Name} - {ex.Message}");
                }
            }
        }
        public void UnbindAllInContext(object context)
        {
            if (context == null || !_bindingsByContext.TryGetValue(context, out var bindings))
                return;

            foreach (var binding in bindings)
            {
                try
                {
                    binding.UnBind();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"解绑失败: {binding.GetType().Name} - {ex.Message}");
                }
            }
        }

        public void RebindAllInContext(object context)
        {
            UnbindAllInContext(context);
            BindAllInContext(context);
        }

        public void BindAll()
        {
            lock (_lock)
            {
                foreach (var binding in Bindings.ToArray())
                {
                    try
                    {
                        binding.Bind();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"全局绑定失败: {binding.GetType().Name} - {ex.Message}");
                    }
                }
            }
        }
        public void UnbindAll()
        {
            lock (_lock)
            {
                foreach (var binding in Bindings.ToArray())
                {
                    try
                    {
                        binding.UnBind();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"全局解绑失败: {binding.GetType().Name} - {ex.Message}");
                    }
                }
            }
        }
        public void RebindAll()
        {
            UnbindAll();
            BindAll();
        }
        #endregion
        public int GetBindingCount()
        {
            lock (_lock) return Bindings.Count;
        }
        public List<IBinding> GetBindingsInContext(object context)
        {
            lock (_lock)
            {
                if (context == null || !_bindingsByContext.TryGetValue(context, out var bindings))
                    return new List<IBinding>();

                return new List<IBinding>(bindings);
            }
        }
        
        private void OnSceneUnloaded(Scene scene)
        {
            // 清理属于已卸载场景的GameObject的绑定
            var gameObjectsToRemove = _bindingsByGameObject
                .Where(kvp => kvp.Key == null || kvp.Key.scene != scene) // GameObject已销毁或不属于当前场景
                .Select(kvp => kvp.Key)
                .ToList();

            lock (_lock)
            {
                foreach (var gameObject in gameObjectsToRemove)
                {
                    if (_bindingsByGameObject.TryGetValue(gameObject, out var bindings))
                    {
                        foreach (var binding in bindings)
                        {
                            Bindings.Remove(binding);
                        }
                        _bindingsByGameObject.Remove(gameObject);
                    }
                }

                _debugInfoDirty = true;
            }
        }
        public void CleanupDestroyedGameObjects()
        {
            lock (_lock)
            {
                var destroyedGameObjects = _bindingsByGameObject
                    .Where(kvp => kvp.Key == null)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var gameObject in destroyedGameObjects)
                {
                    _bindingsByGameObject.Remove(gameObject);
                }

                CleanupEmptyCollections();
                _debugInfoDirty = true;
            }
        }
        private void CleanupEmptyCollections()
        {
            // 清理空的上下文列表
            var emptyContexts = _bindingsByContext
                .Where(kvp => kvp.Value.Count == 0)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in emptyContexts)
                _bindingsByContext.Remove(key);

            // 清理空的GameObject列表
            var emptyGameObjects = _bindingsByGameObject
                .Where(kvp => kvp.Value.Count == 0)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in emptyGameObjects)
                _bindingsByGameObject.Remove(key);
        }
        public string GetDebugInfo()
        {
            if (!_debugInfoDirty && !string.IsNullOrEmpty(_debugInfoCache))
                return _debugInfoCache;

            lock (_lock)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"=== 绑定管理器状态 ===");
                sb.AppendLine($"总绑定数: {Bindings.Count}");
                sb.AppendLine($"上下文数量: {_bindingsByContext.Count}");
                sb.AppendLine($"GameObject数量: {_bindingsByGameObject.Count}");

                sb.AppendLine("\n--- 按类型统计 ---");
                var byType = Bindings.GroupBy(b => b.GetType().Name);
                foreach (var group in byType)
                {
                    sb.AppendLine($"  {group.Key}: {group.Count()}个");
                }

                sb.AppendLine("\n--- 活动绑定 ---");
                foreach (var binding in Bindings.Take(20)) // 限制显示数量
                {
                    if (binding is MonoBehaviour mono)
                        sb.AppendLine($"  {binding.GetType().Name} on {mono.gameObject.name}");
                    else
                        sb.AppendLine($"  {binding.GetType().Name}");
                }

                if (Bindings.Count > 20)
                    sb.AppendLine($"  ... 还有 {Bindings.Count - 20} 个绑定未显示");

                _debugInfoCache = sb.ToString();
                _debugInfoDirty = false;
                return _debugInfoCache;
            }
        }
    }
    
}