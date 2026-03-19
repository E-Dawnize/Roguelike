using ECS.Component.SingletonComponent;
using ECS.Component.Tags;
using Unity.Entities;
using UnityEngine;
using NotImplementedException = System.NotImplementedException;

namespace ECS.Input
{
    public class PlayerInputStateAuthoring:MonoBehaviour {}

    public class PlayerInputStateBaker : Baker<PlayerInputStateAuthoring>
    {
        public override void Bake(PlayerInputStateAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new PlayerInputState());
            AddComponent<PlayerInputSingletonTag>(entity);
        }
    }
}