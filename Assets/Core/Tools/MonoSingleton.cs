﻿using System;
using UnityEngine;

namespace Core.Tools
{
    
    public class MonoSingleton<T>:MonoBehaviour where T:MonoBehaviour
    {
        private readonly static Lazy<T> InstanceLazy = new(() =>
        {
            T instance=FindFirstObjectByType<T>();
            if (instance == null)
            {
                GameObject singletonObj = new GameObject(typeof(T).Name + " (Singleton)");
                instance = singletonObj.AddComponent<T>();
                // 设置为 DontDestroyOnLoad，避免场景切换销毁
                DontDestroyOnLoad(singletonObj);
            }
            return instance;
        });
        public static T Instance => InstanceLazy.Value;
    }
}