using UnityEngine;

public class MediumChunkMirror : MonoBehaviour
{
    public Mesh TerrainLOD0;
    public Mesh TerrainLOD1;
    public Mesh TerrainLOD2;
    public Mesh TerrainLOD3;

    public Texture2D renderInformation;

    // public bool newMesh = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        TerrainLOD0 = new Mesh
        {
            name = "LOD-0"
        };
        TerrainLOD1 = new Mesh
        {
            name = "LOD-1"
        };
        TerrainLOD2 = new Mesh
        {
            name = "LOD-2"
        };
        TerrainLOD3 = new Mesh
        {
            name = "LOD-3"
        };

        MeshFilter[] LODFilters = transform.GetComponentsInChildren<MeshFilter>();

        LODFilters[0].mesh = TerrainLOD0;
        LODFilters[1].mesh = TerrainLOD1;
        LODFilters[2].mesh = TerrainLOD2;
        LODFilters[3].mesh = TerrainLOD3;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // if(newMesh)
        // {
        //     MeshFilter[] LODFilters = transform.GetComponentsInChildren<MeshFilter>();

        //     LODFilters[0].mesh = TerrainLOD0;
        //     LODFilters[1].mesh = TerrainLOD0;
        //     LODFilters[2].mesh = TerrainLOD2;
        //     LODFilters[3].mesh = TerrainLOD3;

        //     LODGroup lod = transform.gameObject.GetComponent<LODGroup>();
        //     lod.RecalculateBounds();

        //     newMesh = false;
        // }
    }

    public void UpdatedMesh()
    {
        MeshFilter[] LODFilters = transform.GetComponentsInChildren<MeshFilter>();

        LODFilters[0].mesh = TerrainLOD0;
        LODFilters[1].mesh = TerrainLOD0;
        LODFilters[2].mesh = TerrainLOD2;
        LODFilters[3].mesh = TerrainLOD3;

        LODGroup lod = transform.gameObject.GetComponent<LODGroup>();
        lod.RecalculateBounds();

        // newMesh = false;
    }
}
