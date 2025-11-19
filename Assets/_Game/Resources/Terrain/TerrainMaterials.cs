using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "TerrainMaterials", menuName = "Scriptable Objects/TerrainMaterials")]
public class TerrainMaterials : ScriptableObject
{
    List<MaterialDefinition> materials;
}

struct MaterialDefinition
{
    Material mat;
    FixedString128Bytes description;
}
