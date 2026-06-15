using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public static class MediumChunkMirrorRegistry
{
    public static Dictionary<Entity, MediumChunkMirror> Mirrors = new Dictionary<Entity, MediumChunkMirror>();

    public static Queue<MediumChunkMirror> pendingMirrors = new Queue<MediumChunkMirror>();
}
