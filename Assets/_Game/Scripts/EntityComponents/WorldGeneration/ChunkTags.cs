using Unity.Entities;

public struct ChunkRequestPopulation : IComponentData, IEnableableComponent
{
    // Chunk is ready to be populated
}

public struct ChunkRequestPhysics : IComponentData, IEnableableComponent
{
    // Minor chunk is ready to recieve a new physics component
}

public struct ChunkGenerationComplete : IComponentData, IEnableableComponent
{
    // All faccets of a chunk are generated
}

public struct ChunkPopulationComplete : IComponentData, IEnableableComponent
{
    // All elements required for a lower order chunk to generate are present
}

public struct ChunkRequestMesh : IComponentData, IEnableableComponent
{
    // Chunk is ready to recieve a new mesh
}

public struct ChunkActive : IComponentData, IEnableableComponent
{
    // Chunk is in use by a system and either in creation or in use.
}

public struct ChunkRequiresMirror : IComponentData, IEnableableComponent
{
    // Chunk requires a connection to a chunk mirror monobehaviour. Only occurs once on generation, link remains consistant.
}

public struct ChunkRequestLink : IComponentData, IEnableableComponent
{
    // Chunk requesting a link to a higher order chunk
}

public struct ChunkValidMesh : IComponentData, IEnableableComponent
{
    
}

public struct ChunkMediumLod0Ready : IComponentData, IEnableableComponent
{
    
}

public struct ChunkMediumLod0Transfered : IComponentData, IEnableableComponent
{
    
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