using System.Collections.Generic;
using Common.Enums;
using UnityEngine;

namespace Configs
{
    
    [CreateAssetMenu(fileName = "BuffConfig", menuName = "Buff配置")]
    public class BuffConfig :ScriptableObject
    {
        [Header("基础参数")]
        public BuffType Type;
        public float Duration;
        public int MaxStacks;
        public bool IsRefreshDuration; 
        [Header("效果配置（原子效果组合）")]
        public List<BuffEffectConfig> Effects;
    }
    [System.Serializable]
    public class BuffEffectConfig
    {
        public BuffEffectType EffectType;  // 效果类型
        public float Value;                // 效果数值（如每秒伤害2 → Value=2）
        public float TriggerInterval;      // 触发间隔（如每1秒掉一次血）
    }
}