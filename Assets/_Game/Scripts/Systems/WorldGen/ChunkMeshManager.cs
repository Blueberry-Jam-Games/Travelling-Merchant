using UnityEngine;
using System.Collections.Generic;

public class ChunkMeshManager
{
    private List<Mesh> meshes = new List<Mesh>();

    public int CreateEmptyMesh()
    {
        Mesh mesh = new Mesh();

        meshes.Add(mesh);

        return meshes.Count - 1;
    }

    public int SetMesh(Mesh mesh, int index)
    {
        if (index >= meshes.Count)
        {
            meshes.Add(mesh);
            return meshes.Count - 1;
        }

        meshes[index] = mesh;

        return -1;
    }
    public Mesh GetMesh(int id) => meshes[id];
}
