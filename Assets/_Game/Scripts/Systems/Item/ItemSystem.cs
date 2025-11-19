using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SocialPlatforms;

[UpdateAfter(typeof(CharacterControllerSystem))]
partial struct ItemSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        ComponentLookup<ItemControl> itemControlLookup = SystemAPI.GetComponentLookup<ItemControl>(true);

        foreach ((
            RefRO<Interactable> interactable,
            RefRO<LocalTransform> localTransform,
            RefRW<PhysicsGravityFactor> physicsGravityFactor,
            RefRW<PhysicsVelocity> physicsVelocity,
            RefRW<HoldableData> holdableData
            ) in SystemAPI.Query<
            RefRO<Interactable>,
            RefRO<LocalTransform>,
            RefRW<PhysicsGravityFactor>,
            RefRW<PhysicsVelocity>,
            RefRW<HoldableData>
        >())
        {
            HoldableData tempHoldableData = holdableData.ValueRO;

            if (tempHoldableData.ControlingEntity == Entity.Null && tempHoldableData.held == true)
            {
                // Just Dropped
                tempHoldableData.held = false;
                physicsGravityFactor.ValueRW.Value = 1;
            }
            else if (tempHoldableData.ControlingEntity != Entity.Null && tempHoldableData.held == false)
            {
                // Just picked up
                tempHoldableData.held = true;
                physicsGravityFactor.ValueRW.Value = 0;

                ItemControl itemControl = itemControlLookup[tempHoldableData.ControlingEntity];
                float3 playerRotation = itemControl.controllerRotation * math.TORADIANS;

                float3 rotationToTarget = math.Euler(localTransform.ValueRO.Rotation) - playerRotation;
                rotationToTarget *= math.TODEGREES;
                rotationToTarget = RoundRotationValue(rotationToTarget, 45, 45, 45);

                tempHoldableData.zeroedRotation = rotationToTarget;
            }

            // Item Movement
            if (tempHoldableData.ControlingEntity != Entity.Null)
            {
                ItemControl tempItemControl = itemControlLookup[tempHoldableData.ControlingEntity];
                // float3 targetPosition = tempCharacterController.headPosition + tempCharacterController.heldItemDistance * tempCharacterController.cameraVector;

                // float3 itemCurrentPosition = transformLookup[tempItemControl.heldItem].Position;

                float3 targetPosition = tempItemControl.itemTargetPosition;
                float3 itemCurrentPosition = localTransform.ValueRO.Position;

                float3 velocityVector = targetPosition - itemCurrentPosition;

                if (math.length(velocityVector) < 0.05)
                {
                    velocityVector = float3.zero;
                }
                else
                {
                    velocityVector = math.normalize(velocityVector) * math.clamp(math.length(velocityVector), 0.05f, 2) * 8;
                }

                // TODO: seperate out each factor on the rotation velocity and tune the players rotation to have a faster impact on the rotation.
                // This will smooth it out when correcting due to collision, but make it snappy as the player turns
                float3 rotationVector = math.Euler(localTransform.ValueRO.Rotation) - tempHoldableData.zeroedRotation * math.TORADIANS;
                ItemControl itemControl = itemControlLookup[tempHoldableData.ControlingEntity];
                float3 playerRotation = itemControl.controllerRotation;
                rotationVector -= playerRotation * math.TORADIANS;
                rotationVector -= itemControl.itemRotationOffset * math.TORADIANS;

                rotationVector = WrapRotationValue(rotationVector);

                rotationVector *= -5;

                physicsVelocity.ValueRW.Angular = rotationVector;
                physicsVelocity.ValueRW.Linear = velocityVector;
            }

            holdableData.ValueRW = tempHoldableData;
        }
    }

    float3 RoundRotationValue(float3 input, float xInterval, float yInterval, float zInterval)
    {
        float x = input.x;
        float y = input.y;
        float z = input.z;

        x = math.round(x /= xInterval) * xInterval;
        y = math.round(y /= xInterval) * yInterval;
        z = math.round(z /= xInterval) * zInterval;

        return new float3(x, y, z);
    }

    float3 WrapRotationValue(float3 input)
    {
        input = math.fmod(input + math.PI, math.PI * 2); // fmod behaves correctly with floats
        input += math.select(0f, 2 * math.PI, input < 0);
        return input - math.PI;
    }
}
