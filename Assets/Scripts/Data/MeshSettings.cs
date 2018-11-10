﻿using UnityEngine;

[CreateAssetMenu(fileName = "MeshSettings", menuName = "Terrain/MeshSettings")]
public class MeshSettings : UpdatebleData
{
    public const int numSupportedLODs = 5;
    public const int numSupportedChunkSizes = 9;
    public const int numSupportedFlatshadedChunkSizes = 3;
    public static readonly int[] supportedChunkSizes = { 48, 72, 96, 120, 144, 168, 192, 216, 240 };

    public float meshScale = 2;
    public bool useFlatShading;

    [Header("Chunk Data")]
    [Range(0, numSupportedChunkSizes - 1)]
    public int chunkSizeIndex;
    [Range(0, numSupportedFlatshadedChunkSizes - 1)]
    public int flatshadedChunkSizeIndex;


    // Number of vertices per line for mesh rendered at LOD = 0
    // Includes to 2 extra vertices that are excluded from final mesh but used for calculating normals
    public int numVerticesPerLine
    {
        get
        {
            return supportedChunkSizes[(useFlatShading) ? flatshadedChunkSizeIndex : chunkSizeIndex]
                - 1 + 2;
        }
    }

    public float meshWorldSize
    {
        get
        {
            return (numVerticesPerLine - 1 - 2) * meshScale;
        }
    }
}
