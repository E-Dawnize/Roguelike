using System.Collections.Generic;
using Core.Architecture.Interfaces;
using UnityEngine;

namespace Core.Boot
{
    public class UpdateRunner:MonoBehaviour
    {
        private static UpdateRunner _instance;
        private readonly List<ITickable> _tickables = new List<ITickable>();
        private readonly object _lock = new object();

        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (_instance != null) return;
            
            var go = new GameObject("[UpdateRunner]");
            _instance = go.AddComponent<UpdateRunner>();
            DontDestroyOnLoad(go);
            
            Debug.Log("UpdateRunner initialized");
        }
        
        public void Register(ITickable tickable)
        {
            lock (_lock)
            {
                if (!_tickables.Contains(tickable))
                    _tickables.Add(tickable);
            }
        }

        public void Unregister(ITickable tickable)
        {
            lock (_lock)
            {
                _tickables.Remove(tickable);
            }
        }
        private void Update()
        {
            float deltaTime = Time.deltaTime;

            lock (_lock)
            {
                foreach (var tickable in _tickables.ToArray()) // 复制避免迭代修改
                {
                    try
                    {
                        tickable.Tick(deltaTime);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Tick error in {tickable.GetType().Name}: {ex.Message}");
                    }
                }
            }
        }

        private void OnDestroy()
        {
            lock (_lock)
            {
                _tickables.Clear();
            }
        }

        public int GetTickableCount()
        {
            lock (_lock) return _tickables.Count;
        }
    }
}