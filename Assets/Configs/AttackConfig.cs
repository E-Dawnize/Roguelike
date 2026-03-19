using System.Collections.Generic;
using UnityEngine;

namespace Configs
{
    [CreateAssetMenu(fileName = "AttackConfig", menuName = "深渊回廊/攻击配置")]
    public class AttackConfig : ScriptableObject
    {
        public int BaseDamage;                  // 基础伤害
        public List<BuffConfig> AttachBuffs; // 攻击附加的Buff列表（可配多个）
        public float Cooldown;                  // 冷却时间
    }
}