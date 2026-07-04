using Unity.Entities;

public struct ChunkActive : IComponentData, IEnableableComponent
{
    // Chunk is not sleeping. All tags must be in default state when this is false
}

public struct ChunkPopulated : IComponentData, IEnableableComponent
{
    // Chunk has heightmap, structure or other data generated
    // Default: False
}

public struct ChunkGeneratedPhysicsCollider : IComponentData, IEnableableComponent
{
    // Minor chunk is ready to recieve a new physics component
    // Default: False
}

public struct ChunkGeneratedMesh : IComponentData, IEnableableComponent
{
    // Chunk has had a Mesh generated
    // Default: False
}

public struct ChunkGeneratingMesh : IComponentData, IEnableableComponent
{
    // Chunk is having a mesh generated. Only used for async generation.
    // Default: False
}

public struct EntityHasMirror : IComponentData, IEnableableComponent
{
    // Entity has a monobehaviour mirror set up
    // Default: False
}

public struct ChunkContainsParentLink : IComponentData, IEnableableComponent
{
    // Chunk entity contains the link to a higher level Chunk. Medium -> Major,  Minor -> Medium
    // Default: False
}

//  Major Chunk
//      LocalTransform
//      ChunkActive
//      ChunkRequestPopulation
//      Chunk
//      MajorChunk
//      ChunkGenerationComplete

//  Medium Chunk
//      LocalTransform
//      ChunkActive
//      ChunkRequestPopulation
//      ChunkRequestMesh
//      Chunk
//      MediumChunk
//      ChunkGenerationComplete
//      ChunkRequiresMirror
//      ChunkRequestLink

//  Minor Chunk
//      LocalTransform
//      Chunk
//      ChunkActive
//      ChunkRequestMesh
//      ChunkRequestPhysics
//      PhysicsCollider
//      PhysicsWorldIndex
//      MinorChunk
//      ChunkGenerationComplete

//  Character Chunk Tracking System - ISystem
//
//  Chunk Placment System  - ISystem
//      Major Chunk
//      + ChunkRequestPopulation
//      + ChunkActive
//
//      Medium Chunk
//      + ChunkRequestPopulation
//      + ChunkRequestMesh
//      + ChunkActive
//      + ChunkRequestLink
//      -> majorChunk set
//
//  Chunk Major Population System - SystemBase
//      Major Chunk
//      + ChunkGenerationComplete
//      + ChunkActive
// 
//  Chunk Medium Mirror Linking System - SystemBase
//      Medium Chunk
//      - ChunkRequiresMirror
//
//  Chunk Medium Heightmap Link System - ISystem
//      Medium Chunk
//      - ChunkRequestLink
// 
//  Chunk Medium Population System - ISystem
//      Medium Chunk
//      - ChunkRequestPopulation
//
//      Minor Chunk
//      + ChunkActive
//      + ChunkRequestMesh
//      -> mediumChunk set
// 
//  Chunk Medium Mesh System - ISystem
//      Medium Chunk
//      - ChunkRequestMesh
//
//  Chunk Minor Mesh System - SystemBase
//      Minor Chunk
//      - ChunkRequestUpdate
//      - ChunkRequestMesh
//      + ChunkGenerationComplete
// 
//  Chunk Physics System - ISystem
//      Minor Chunk
//      - ChunkRequestPhysics
// 
//  Medium Chunk Mesh Transfer System
//      Medium Chunk
//      + ChunkGenerationComplete
// 