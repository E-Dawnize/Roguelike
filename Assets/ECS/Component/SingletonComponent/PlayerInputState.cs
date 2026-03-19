using Unity.Entities;
using Unity.Mathematics;

namespace ECS.Component.SingletonComponent
{
    public struct PlayerInputState:IComponentData
    {
        public float2 dir;
        public bool isValid;
    }
}