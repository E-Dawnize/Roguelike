using ECS.Component;
using ECS.Component.SingletonComponent;
using ECS.Component.Tags;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Bridge
{
    public interface IEcsInputBridge
    {
        void SetMove(Vector2 dir,bool isActive);
    }
    public sealed class EcsInputBridge : IEcsInputBridge
    {
        private readonly EntityManager _em;
        private readonly Entity _inputEntity;

        public EcsInputBridge(EntityManager em)
        {
            _em = em;
            var query=em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerInputState>(),
                ComponentType.ReadOnly<PlayerInputSingletonTag>());
            if (!query.TryGetSingletonEntity<PlayerInputState>(out _inputEntity))
            {
                throw new System.Exception("Entity not found");
            }
        }

        public void SetMove(Vector2 dir, bool isActive)
        {
            _em.SetComponentData(_inputEntity, new PlayerInputState
            {
                dir = new float2(dir.x, dir.y),
                isValid = isActive
            });
        }
    }
}