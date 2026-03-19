namespace Common.Enums
{
    public enum BuffType
    {
        None,
        Poison,    // 中毒：持续伤害
        Burn,      // 灼烧：持续伤害+减速
        Bleed,     // 流血：持续伤害+攻击降低
        Shield     // 护盾：吸收伤害  
    }
    public enum BuffEffectType
    {
        PeriodicDamage, // 持续伤害
        Slow,           // 减速
        ReduceAttack,   // 降低攻击
        AbsorbDamage    // 吸收伤害
    }
}