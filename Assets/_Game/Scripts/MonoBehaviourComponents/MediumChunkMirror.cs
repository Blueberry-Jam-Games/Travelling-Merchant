using UnityEngine;

public class MediumChunkMirror : MonoBehaviour
{
    public Mesh TerrainLOD0;
    public Mesh TerrainLOD1;
    public Mesh TerrainLOD2;
    public Mesh TerrainLOD3;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        TerrainLOD0 = new Mesh();
        TerrainLOD0.name = "LOD-0";
        TerrainLOD1 = new Mesh();
        TerrainLOD1.name = "LOD-1";
        TerrainLOD2 = new Mesh();
        TerrainLOD2.name = "LOD-2";
        TerrainLOD3 = new Mesh();
        TerrainLOD3.name = "LOD-3";

        MeshFilter[] LODFilters = transform.GetComponentsInChildren<MeshFilter>();

        LODFilters[0].mesh = TerrainLOD0;
        LODFilters[1].mesh = TerrainLOD1;
        LODFilters[2].mesh = TerrainLOD2;
        LODFilters[3].mesh = TerrainLOD3;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
