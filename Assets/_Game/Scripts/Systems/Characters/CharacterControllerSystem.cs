using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SocialPlatforms;

[UpdateBefore(typeof(SimulationSystemGroup))]
partial struct CharacterControllerSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        //Transform lookup used to pull components off of an entity
        ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<PhysicsVelocity> physicsVelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocity>(false);
        ComponentLookup<HoldableData> holdableDataLookup = SystemAPI.GetComponentLookup<HoldableData>(false);
        ComponentLookup<Interactable> interactableLookup = SystemAPI.GetComponentLookup<Interactable>(true);

        PhysicsWorldSingleton physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        CollisionWorld collisionWorld = physicsWorld.CollisionWorld;

        foreach ((
            RefRW<CharacterController> characterController,
            RefRO<LocalTransform> localTransform,
            RefRO<LocalInput> localInput,
            RefRW<PhysicsVelocity> physicsVelocity
            )
            in SystemAPI.Query<
            RefRW<CharacterController>,
            RefRO<LocalTransform>,
            RefRO<LocalInput>,
            RefRW<PhysicsVelocity>
        >())
        {
            // Section:
            // Data Gathering
            // Notes: New structs are created within the for loop as seperate variables will be needed when the system is transitioned to jobs.
            // Gathers copies of all structs to reduce read write calls reducing overhead for when transitioned to jobs.

            CharacterController tempCharacterController = characterController.ValueRO;
            LocalInput tempLocalInput = localInput.ValueRO;
            LocalTransform tempLocalTransform = localTransform.ValueRO;

            // Section:
            // Movement Code
            // TODO: Include jump, sprint, hill climbing, and refine controls
            quaternion cameraDirection = quaternion.Euler(tempCharacterController.cameraRotation.y * math.TORADIANS, tempCharacterController.cameraRotation.x * math.TORADIANS, 0);
            tempCharacterController.cameraVector = math.rotate(cameraDirection, new float3(0, 0, 1));

            tempCharacterController.headPosition = tempCharacterController.cameraOffset + tempLocalTransform.Position;

            float2 headRotationDelta = tempLocalInput.mouseDelta;

            float2 inputMoveDirection = tempLocalInput.moveDirection;

            float scaleFactor = 0.5f; // TODO Link to Mouse Sensativity

            headRotationDelta *= scaleFactor;
            headRotationDelta.y *= -1; // TODO Link to mouse invert

            //Rotation change of character
            tempCharacterController.cameraRotation += headRotationDelta;
            tempCharacterController.cameraRotation.y = math.clamp(tempCharacterController.cameraRotation.y, -90, 90);

            //Normal Raycast
            // RaycastInput normalRaycast = new RaycastInput
            // {
            //     Start = tempLocalTransform.Position,
            //     End = tempLocalTransform.Position - new float3(0, -2f, 0)
            // };

            float3 moveDirection = new float3(inputMoveDirection.x, 0, inputMoveDirection.y);

            float3 playerInfluencedVelocity = math.rotate(quaternion.Euler(0, tempCharacterController.cameraRotation.x * math.TORADIANS, 0), moveDirection);

            //newVelocity = adjustToNormal * moveDirection;

            if (!math.all(playerInfluencedVelocity == float3.zero))
            {
                playerInfluencedVelocity = math.normalize(playerInfluencedVelocity);
            }
            else
            {
                playerInfluencedVelocity = float3.zero;
            }

            playerInfluencedVelocity *= 3; // TODO Adjust sensativity

            float3 environmentInfluencedVelocity = physicsVelocity.ValueRO.Linear;

            if (math.abs(playerInfluencedVelocity.y) < math.abs(environmentInfluencedVelocity.y))
            {
                playerInfluencedVelocity.y = environmentInfluencedVelocity.y;
            }

            physicsVelocity.ValueRW.Linear = playerInfluencedVelocity;
            physicsVelocity.ValueRW.Angular = float3.zero;

            characterController.ValueRW = tempCharacterController;
        }

        foreach ((
            RefRW<CharacterController> characterController,
            RefRO<LocalInput> localInput,
            RefRW<ItemControl> itemControl,
            Entity caster
            )
            in SystemAPI.Query<
            RefRW<CharacterController>,
            RefRO<LocalInput>,
            RefRW<ItemControl>
        >().WithEntityAccess())
        {
            CharacterController tempCharacterController = characterController.ValueRO;
            LocalInput tempLocalInput = localInput.ValueRO;
            ItemControl tempItemControl = itemControl.ValueRO;

            // ----------------------------------------------------------------------------------------------------------------------------------------------------------
            // Item Interaction
            if (tempLocalInput.interact && tempItemControl.heldItem == Entity.Null)
            {
                UnityEngine.Ray playerRay = new UnityEngine.Ray(tempCharacterController.headPosition, tempCharacterController.cameraVector);

                // TODO build monobehaviour data injector to replace this. Assigning layers within each job will become difficult to track.
                int itemLayer = 6;

                RaycastInput raycastInput = new RaycastInput
                {
                    Start = playerRay.GetPoint(0),
                    End = playerRay.GetPoint(2),
                    Filter = new CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = 1u << itemLayer,
                        GroupIndex = 0
                    }
                };

                collisionWorld.CastRay(raycastInput, out Unity.Physics.RaycastHit raycastHit);
                Entity entityReturn = raycastHit.Entity;

                if (entityReturn != Entity.Null && interactableLookup.HasComponent(entityReturn) && interactableLookup.IsComponentEnabled(entityReturn))
                {
                    if (holdableDataLookup.HasComponent(entityReturn))
                    {
                        tempItemControl.heldItem = entityReturn;

                        float3 itemPosition = transformLookup[entityReturn].Position;

                        tempCharacterController.heldItemDistance = math.distance(itemPosition, tempCharacterController.headPosition);

                        tempItemControl.itemRotationOffset = float3.zero;

                        HoldableData holdableData = holdableDataLookup[entityReturn];
                        holdableData.ControlingEntity = caster;
                        holdableDataLookup[entityReturn] = holdableData;
                    }
                    else if (true) // TODO Replace with clickable component later
                    {

                    }
                }
            }
            else if (localInput.ValueRO.interact && tempItemControl.heldItem != Entity.Null)
            {
                HoldableData holdableData = holdableDataLookup[tempItemControl.heldItem];
                holdableData.ControlingEntity = Entity.Null;
                holdableDataLookup[tempItemControl.heldItem] = holdableData;

                tempItemControl.heldItem = Entity.Null;
            }

            tempItemControl.itemTargetPosition = tempCharacterController.headPosition + tempCharacterController.heldItemDistance * tempCharacterController.cameraVector;

            tempItemControl.controllerRotation = new float3(0, tempCharacterController.cameraRotation.x, 0);

            if (tempLocalInput.rotateLeft)
            {
                tempItemControl.itemRotationOffset += new float3(0, -45, 0);
            }
            if (tempLocalInput.rotateRight)
            {
                tempItemControl.itemRotationOffset += new float3(0, 45, 0);
            }

            itemControl.ValueRW = tempItemControl;
            characterController.ValueRW = tempCharacterController;
        }
    }
}
