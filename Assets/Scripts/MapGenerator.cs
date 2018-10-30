using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct TerrainType
{
    public string name;
    public float height;
    public Color color;
}

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode
    {
        NoiseMap,
        ColorMap,
        Mesh
    }

    public DrawMode drawMode;

    [Header("Map Size Data")]
    [Range(0, 6)]
    public int levelOfDetail;
    public float noiseScale;

    [Header("Map Noise Data")]
    public int octaves;
    [Range(0, 1)]
    public float persistance;
    public float lacunarity;

    [Header("Randomness")]
    public int seed;
    public Vector2 offset;

    [Header("Mesh Data")]
    public float heightMultiplier;
    public AnimationCurve heightCurve;

    [Header("Color Data")]
    public TerrainType[] regions;

    [Header("Debug")]
    public bool autoUpdate;

    private const int mapChunkSize = 241;

    public void GenerateMap()
    {
        float[,] noiseMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed, noiseScale,
            octaves, persistance, lacunarity,
            offset);

        Color[] colorMap = new Color[mapChunkSize * mapChunkSize];
        for (int x = 0; x < mapChunkSize; x++)
        {
            for (int y = 0; y < mapChunkSize; y++)
            {
                float currentHeight = noiseMap[x, y];
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight <= regions[i].height)
                    {
                        colorMap[y * mapChunkSize + x] = regions[i].color;
                        break;
                    }
                }
            }
        }

        MapDisplay mapDisplay = FindObjectOfType<MapDisplay>();

        if (drawMode == DrawMode.NoiseMap)
            mapDisplay.DrawTexture(TextureGenerator.TextureFromHeightMap(noiseMap));
        else if (drawMode == DrawMode.ColorMap)
            mapDisplay.DrawTexture(TextureGenerator.TextureFromColorMap(colorMap, mapChunkSize, mapChunkSize));
        else if (drawMode == DrawMode.Mesh)
            mapDisplay.DrawMesh(MeshGenerator.GenerateTerrainMesh(noiseMap, heightMultiplier,
                heightCurve, levelOfDetail),
                TextureGenerator.TextureFromColorMap(colorMap, mapChunkSize, mapChunkSize));
    }

    /// <summary>
    /// Called when the script is loaded or a value is changed in the
    /// inspector (Called in the editor only).
    /// </summary>
    void OnValidate()
    {
        if (lacunarity < 1)
            lacunarity = 1;

        if (octaves < 0)
            octaves = 0;
    }
}
