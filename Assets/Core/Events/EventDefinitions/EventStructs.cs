using Common.Enums;
using Unity.Entities;

namespace Core.Events.EventDefinitions
{
    public struct EntityHpChangedEvent
    {
        public Entity EntityId;
        public int DeltaHp;
        public int CurrentHp;
        public EntityHpChangedEvent(Entity entity, int deltaHp, int currentHp)
        {
            EntityId = entity;
            DeltaHp = deltaHp;
            CurrentHp = currentHp;
        }
    }

    public struct AttachBuffEvent
    {
        public Entity SourceEntity;
        public Entity TargetEntity;
        public BuffType BuffType;

        public AttachBuffEvent(Entity source, Entity target, BuffType buffType)
        {
            SourceEntity = source;
            TargetEntity = target;
            BuffType = buffType;
        }
    }
    
    public struct AttackEvent
    {
        public Entity SourceEntity;

        public AttackEvent(Entity source)
        {
            SourceEntity = source;
        }
    }
}