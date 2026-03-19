using Unity.Entities;
using Unity.Mathematics;

namespace ECS.Component
{
    public struct MoveSpeed : IComponentData
    {
        public float Speed;
    }
}