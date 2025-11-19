using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    private void Start()
    {
        Cursor.visible = true;
    }
    private void LateUpdate()
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery entityQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<ECSCamera>().Build(entityManager);

        NativeArray<ECSCamera> compiledCameraData = entityQuery.ToComponentDataArray<ECSCamera>(Allocator.Temp);

        float3 cameraPosition = float3.zero;
        float2 cameraRotation = float2.zero;

        int inputSize = compiledCameraData.Length;

        foreach (ECSCamera camera in compiledCameraData)
        {
            cameraPosition += camera.position;
            cameraRotation += camera.rotation;
        }

        cameraPosition /= inputSize;
        cameraRotation /= inputSize;

        transform.position = cameraPosition;
        transform.rotation = Quaternion.Euler(cameraRotation.y, cameraRotation.x, 0);

        // transform.rotation = Quaternion.Euler(characterController.cameraRotation.y, characterController.cameraRotation.x, 0);
        // transform.position = characterController.cameraPosition;
    }
}
