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

    private float[,] _falloffMap;

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
}