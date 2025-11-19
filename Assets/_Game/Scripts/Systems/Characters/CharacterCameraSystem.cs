using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SocialPlatforms;

[UpdateAfter(typeof(CharacterControllerSystem))]
partial struct CharacterCameraSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        ChunkWorldSingleton input = SystemAPI.GetSingleton<ChunkWorldSingleton>();
        foreach ((
            RefRO<CharacterController> characterController,
            RefRO<LocalTransform> transform,
            RefRW<ECSCamera> ecsCamera
            ) in SystemAPI.Query<
            RefRO<CharacterController>,
            RefRO<LocalTransform>,
            RefRW<ECSCamera>
        >())
        {
            float3 newPosition = transform.ValueRO.Position;
            newPosition += characterController.ValueRO.cameraOffset;
            ecsCamera.ValueRW.position = newPosition;
            ecsCamera.ValueRW.rotation = characterController.ValueRO.cameraRotation;
        }
    }
}
