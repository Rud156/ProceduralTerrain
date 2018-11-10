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
    [Range(0, MeshSettings.numSupportedLODs - 1)]
    public int editorPreviewLOD;
    public MeshSettings meshSettings;
    public HeightMapSettings heightMapSettings;
    public TextureData textureData;
    public Material terrainMaterial;

    [Header("Debug")]
    public bool autoUpdate;

    private Queue<MapThreadInfo<HeightMap>> _mapDataThreadInfoQueue;
    private Queue<MapThreadInfo<MeshData>> _meshDataThreadInfoQueue;

    private float[,] _falloffMap;

    /// <summary>
    /// Start is called on the frame when a script is enabled just before
    /// any of the Update methods is called the first time.
    /// </summary>
    void Start()
    {
        _mapDataThreadInfoQueue = new Queue<MapThreadInfo<HeightMap>>();
        _meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

        textureData.ApplyToMaterial(terrainMaterial);
        textureData.UpdateMeshHeights(terrainMaterial,
            heightMapSettings.minHeight, heightMapSettings.maxHeight);
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
                MapThreadInfo<HeightMap> threadInfo = _mapDataThreadInfoQueue.Dequeue();
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
        textureData.UpdateMeshHeights(terrainMaterial,
            heightMapSettings.minHeight, heightMapSettings.maxHeight);

        HeightMap heightMap = HeightMapGenerator.GenerateHeightMap(
            meshSettings.numVerticesPerLine,
            meshSettings.numVerticesPerLine,
            heightMapSettings,
            Vector2.zero
        );
        MapDisplay mapDisplay = FindObjectOfType<MapDisplay>();

        if (drawMode == DrawMode.NoiseMap)
            mapDisplay.DrawTexture(TextureGenerator.TextureFromHeightMap(heightMap.values));
        else if (drawMode == DrawMode.Mesh)
            mapDisplay.DrawMesh(MeshGenerator.GenerateTerrainMesh(
                    heightMap.values,
                    editorPreviewLOD,
                    meshSettings
                )
            );
        else if (drawMode == DrawMode.FalloffMap)
            mapDisplay.DrawTexture(TextureGenerator.TextureFromHeightMap(
                FalloffGenerator.GenerateFalloffMap(meshSettings.numVerticesPerLine)
            ));
    }

    public void RequestHeightMap(Vector2 center, Action<HeightMap> callback)
    {
        ThreadStart threadStart = delegate
        {
            HeightMapThread(center, callback);
        };

        new Thread(threadStart).Start();

    }

    private void HeightMapThread(Vector2 center, Action<HeightMap> callback)
    {
        HeightMap heightMap = HeightMapGenerator.GenerateHeightMap(
            meshSettings.numVerticesPerLine,
            meshSettings.numVerticesPerLine,
            heightMapSettings,
            center
        );
        lock (_mapDataThreadInfoQueue)
        {
            _mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<HeightMap>(callback, heightMap));
        }
    }

    public void RequestMeshData(HeightMap heightMap, int lod, Action<MeshData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MeshDataThread(heightMap, lod, callback);
        };

        new Thread(threadStart).Start();
    }

    private void MeshDataThread(HeightMap heightMap, int lod, Action<MeshData> callback)
    {
        MeshData meshData =
            MeshGenerator.GenerateTerrainMesh(
                    heightMap.values,
                    lod,
                    meshSettings
                );
        lock (_meshDataThreadInfoQueue)
        {
            _meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }

    /// <summary>
    /// Called when the script is loaded or a value is changed in the
    /// inspector (Called in the editor only).
    /// </summary>
    void OnValidate()
    {
        if (meshSettings != null)
        {
            meshSettings.OnValuesUpdated -= OnValuesUpdated;
            meshSettings.OnValuesUpdated += OnValuesUpdated;
        }

        if (heightMapSettings != null)
        {
            heightMapSettings.OnValuesUpdated -= OnValuesUpdated;
            heightMapSettings.OnValuesUpdated += OnValuesUpdated;
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