using ECS.Component;
using ECS.Component.SingletonComponent;
using ECS.Component.Tags;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using NotImplementedException = System.NotImplementedException;

namespace ECS.System
{
    public partial struct MovementSystem:ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = Time.deltaTime;
            foreach (var (transform,speed) in 
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<MoveSpeed>>().WithAll<PlayerTag>())
            {
                var input = SystemAPI.GetSingleton<PlayerInputState>();
                if(!input.isValid)continue;
                var dir = new float3(input.dir.x, 0, input.dir.y);
                transform.ValueRW.Position+=dir*speed.ValueRO.Speed*dt;
            }
        }
    }
}