using Unity.Entities;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class ChunkMeshManagerSystem : SystemBase
{
    public static ChunkMeshManager Instance;

    protected override void OnCreate()
    {
        Instance = new ChunkMeshManager();
    }

    protected override void OnUpdate() { }
}