﻿using System;
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

    [Header("Map Size Data")]
    [Range(0, 6)]
    public int editorPreviewLOD;
    public TerrainData terrainData;
    public NoiseData noiseData;

    [Header("Color Data")]
    // public TerrainType[] regions;

    [Header("Debug")]
    public bool autoUpdate;

    private Queue<MapThreadInfo<MapData>> _mapDataThreadInfoQueue;
    private Queue<MapThreadInfo<MeshData>> _meshDataThreadInfoQueue;

    private static MapGenerator _instance;

    private float[,] _falloffMap;

    public static int mapChunkSize
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<MapGenerator>();

            if (_instance.terrainData.useFlatShading)
                return 95;
            else
                return 239;
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

        _falloffMap = FalloffGenerator.GenerateFalloffMap(mapChunkSize);
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

    public void DrawMapInEditor()
    {
        MapData mapData = GenerateMapData(Vector2.zero);
        MapDisplay mapDisplay = FindObjectOfType<MapDisplay>();

        if (drawMode == DrawMode.NoiseMap)
            mapDisplay.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        // else if (drawMode == DrawMode.ColorMap)
        //     mapDisplay.DrawTexture(TextureGenerator.TextureFromColorMap(mapData.colorMap,
        //         mapChunkSize, mapChunkSize));
        else if (drawMode == DrawMode.Mesh)
            mapDisplay.DrawMesh(MeshGenerator.GenerateTerrainMesh(
                    mapData.heightMap,
                    terrainData.heightMultiplier,
                    terrainData.heightCurve,
                    editorPreviewLOD,
                    terrainData.useFlatShading
                )
            // ,
            // TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
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

        // Color[] colorMap = new Color[mapChunkSize * mapChunkSize];

        // for (int x = 0; x < mapChunkSize; x++)
        // {
        //     for (int y = 0; y < mapChunkSize; y++)
        //     {
        //         if (terrainData.useFalloff)
        //             noiseMap[x, y] = Mathf.Clamp01(noiseMap[x, y] - _falloffMap[x, y]);

        //         float currentHeight = noiseMap[x, y];
        //         for (int i = 0; i < regions.Length; i++)
        //         {
        //             if (currentHeight >= regions[i].height)
        //                 colorMap[y * mapChunkSize + x] = regions[i].color;
        //             else
        //                 break;
        //         }
        //     }
        // }

        // return new MapData(noiseMap, colorMap);



        for (int x = 0; x < mapChunkSize; x++)
        {
            for (int y = 0; y < mapChunkSize; y++)
            {
                if (terrainData.useFalloff)
                    noiseMap[x, y] = Mathf.Clamp01(noiseMap[x, y] - _falloffMap[x, y]);


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

        _falloffMap = FalloffGenerator.GenerateFalloffMap(mapChunkSize);
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

// [System.Serializable]
// public struct TerrainType
// {
//     public string name;
//     public float height;
//     public Color color;
// }

public struct MapData
{
    public readonly float[,] heightMap;
    // public readonly Color[] colorMap;

    public MapData(float[,] heightMap)
    {
        this.heightMap = heightMap;
        // this.colorMap = colorMap;
    }
}