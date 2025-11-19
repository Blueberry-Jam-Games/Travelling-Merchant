using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class CharacterControllerAuthoring : MonoBehaviour
{
    public bool isPlayer = false;
    public float2 cameraRotation = new float2(0f, 0f);
    public float3 cameraOffset = new float3(0f, 0f, 0f);
}

class CharacterControllerAuthoringBaker : Baker<CharacterControllerAuthoring>
{
    public override void Bake(CharacterControllerAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent<CharacterController>(entity, new CharacterController
        {
            isPlayer = authoring.isPlayer,
            cameraRotation = authoring.cameraRotation,
            cameraOffset = authoring.cameraOffset
        });
        AddComponent<ItemControl>(entity, new ItemControl
        {
            
        });
    }
}

public struct CharacterController : IComponentData
{
    public bool isPlayer;

    // Angular representation of the camera. Used for processing of camera rotation.
    public float2 cameraRotation;

    // Rotation vector for the camera. Used primarily for raycasts, stored for multiple systems to interact with.
    public float3 cameraVector;

    // Cameras offset from the player transform
    public float3 cameraOffset;

    // Stores the world position for the head of the entity. Used for the camera alignment, and interaction raycasts.
    public float3 headPosition;

    // Distance which an item should be held at away from the player.
    public float heldItemDistance;
}
