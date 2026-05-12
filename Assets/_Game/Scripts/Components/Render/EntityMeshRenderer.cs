using System;
using Unity.Entities;
using UnityEngine;

public class EntityMeshRenderer : IComponentData, IDisposable, IEnableableComponent
{
    public Mesh mesh;
    public int material;

    public void Dispose()
    {

    }
}