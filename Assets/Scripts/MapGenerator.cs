using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode
    {
        NoiseMap,
        // ColorMap,
        Mesh,
        FalloffMap
    }
    public DrawMode drawMode;

    [Header("Map Data")]
    [Range(0, MeshGenerator.numSupportedLODs - 1)]
    public int editorPreviewLOD;
    public TerrainData terrainData;
    public NoiseData noiseData;
    public TextureData textureData;
    public Material terrainMaterial;

    [Header("Chunk Data")]
    [Range(0, MeshGenerator.numSupportedChunkSizes - 1)]
    public int chunkSizeIndex;
    [Range(0, MeshGenerator.numSupportedFlatshadedChunkSizes - 1)]
    public int flatshadedChunkSizeIndex;

    [Header("Debug")]
    public bool autoUpdate;

    private Queue<MapThreadInfo<MapData>> _mapDataThreadInfoQueue;
    private Queue<MapThreadInfo<MeshData>> _meshDataThreadInfoQueue;

    private float[,] _falloffMap;

    public int mapChunkSize
    {
        get
        {
            if (terrainData.useFlatShading)
                return MeshGenerator.supportedFlatshadedChunkSizes[flatshadedChunkSizeIndex] - 1;
            else
                return MeshGenerator.supportedChunkSizes[chunkSizeIndex] - 1;
        }
    }

    /// <summary>
    /// Start is called on the frame when a script is enabled just before
    /// any of the Update methods is called the first time.
    /// </summary>
    void Start()
    {
        _mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
        _meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

        textureData.ApplyToMaterial(terrainMaterial);
        textureData.UpdateMeshHeights(terrainMaterial, terrainData.minHeight, terrainData.maxHeight);
    }

    /// <summary>
    /// Update is called every frame, if the MonoBehaviour is enabled.
    /// </summary>
    void Update()
    {
        if (_mapDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < _mapDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MapData> threadInfo = _mapDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }

        if (_meshDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < _meshDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MeshData> threadInfo = _meshDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }

    private void OnValuesUpdated()
    {
        if (!Application.isPlaying)
            DrawMapInEditor();
    }

    private void OnTextureValuesUpdated() => textureData.ApplyToMaterial(terrainMaterial);

    public void DrawMapInEditor()
    {
        textureData.UpdateMeshHeights(terrainMaterial, terrainData.minHeight, terrainData.maxHeight);

        MapData mapData = GenerateMapData(Vector2.zero);
        MapDisplay mapDisplay = FindObjectOfType<MapDisplay>();

        if (drawMode == DrawMode.NoiseMap)
            mapDisplay.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        else if (drawMode == DrawMode.Mesh)
            mapDisplay.DrawMesh(MeshGenerator.GenerateTerrainMesh(
                    mapData.heightMap,
                    terrainData.heightMultiplier,
                    terrainData.heightCurve,
                    editorPreviewLOD,
                    terrainData.useFlatShading
                )
            );
        else if (drawMode == DrawMode.FalloffMap)
            mapDisplay.DrawTexture(TextureGenerator.TextureFromHeightMap(
                FalloffGenerator.GenerateFalloffMap(mapChunkSize)
            ));
    }

    public void RequestMapData(Vector2 center, Action<MapData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MapDataThread(center, callback);
        };

        new Thread(threadStart).Start();

    }

    private void MapDataThread(Vector2 center, Action<MapData> callback)
    {
        MapData mapData = GenerateMapData(center);
        lock (_mapDataThreadInfoQueue)
        {
            _mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }

    public void RequestMeshData(MapData mapData, int lod, Action<MeshData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MeshDataThread(mapData, lod, callback);
        };

        new Thread(threadStart).Start();
    }

    private void MeshDataThread(MapData mapData, int lod, Action<MeshData> callback)
    {
        MeshData meshData =
            MeshGenerator.GenerateTerrainMesh(
                    mapData.heightMap,
                    terrainData.heightMultiplier,
                    terrainData.heightCurve,
                    lod,
                    terrainData.useFlatShading
                );
        lock (_meshDataThreadInfoQueue)
        {
            _meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }

    private MapData GenerateMapData(Vector2 center)
    {
        float[,] noiseMap = Noise.GenerateNoiseMap(
            mapChunkSize + 2,
            mapChunkSize + 2,
            noiseData.seed,
            noiseData.noiseScale,
            noiseData.octaves,
            noiseData.persistance,
            noiseData.lacunarity,
            center + noiseData.offset,
            noiseData.normalizedMode
        );

        if (terrainData.useFalloff)
        {
            if (_falloffMap == null)
                _falloffMap = FalloffGenerator.GenerateFalloffMap(mapChunkSize + 2);

            for (int x = 0; x < mapChunkSize + 2; x++)
            {
                for (int y = 0; y < mapChunkSize + 2; y++)
                {
                    if (terrainData.useFalloff)
                        noiseMap[x, y] = Mathf.Clamp01(noiseMap[x, y] - _falloffMap[x, y]);


                }
            }
        }

        return new MapData(noiseMap);
    }

    /// <summary>
    /// Called when the script is loaded or a value is changed in the
    /// inspector (Called in the editor only).
    /// </summary>
    void OnValidate()
    {
        if (terrainData != null)
        {
            terrainData.OnValuesUpdated -= OnValuesUpdated;
            terrainData.OnValuesUpdated += OnValuesUpdated;
        }

        if (noiseData != null)
        {
            noiseData.OnValuesUpdated -= OnValuesUpdated;
            noiseData.OnValuesUpdated += OnValuesUpdated;
        }

        if (textureData != null)
        {
            textureData.OnValuesUpdated -= OnTextureValuesUpdated;
            textureData.OnValuesUpdated += OnTextureValuesUpdated;
        }
    }

    struct MapThreadInfo<T>
    {
        public readonly Action<T> callback;
        public readonly T parameter;

        public MapThreadInfo(Action<T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
}
public struct MapData
{
    public readonly float[,] heightMap;

    public MapData(float[,] heightMap)
    {
        this.heightMap = heightMap;
    }
}