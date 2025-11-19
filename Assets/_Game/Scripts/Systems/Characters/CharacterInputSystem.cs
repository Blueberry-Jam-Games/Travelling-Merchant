using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SocialPlatforms;

[UpdateBefore(typeof(CharacterControllerSystem))]
partial struct CharacterInputSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        UserInput input = SystemAPI.GetSingleton<UserInput>();
        foreach ((
            RefRO<CharacterController> characterController,
            RefRW<LocalInput> localInput
            ) in SystemAPI.Query<
            RefRO<CharacterController>,
            RefRW<LocalInput>
        >().WithPresent<Player>())
        {
            LocalInput newInput = localInput.ValueRO;
            newInput.mouseDelta = input.mouseDelta;
            newInput.moveDirection = input.moveDirection;

            newInput.interact = input.interact;
            newInput.rotateLeft = input.rotateLeft;
            newInput.rotateRight = input.rotateRight;

            localInput.ValueRW = newInput;
        }
    }
}
