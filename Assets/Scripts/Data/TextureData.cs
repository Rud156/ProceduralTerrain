using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TextureData", menuName = "Terrain/Texture")]
public class TextureData : UpdatebleData
{
    public Color[] baseColors;
    [Range(0, 1)]
    public float[] baseStartHeights;

    private float _savedMinHeight;
    private float _savedMaxHeight;

    public void ApplyToMaterial(Material material)
    {
        material.SetInt("baseColorCount", baseColors.Length);
        material.SetColorArray("baseColors", baseColors);
        material.SetFloatArray("baseStartHeights", baseStartHeights);

        UpdateMeshHeights(material, _savedMinHeight, _savedMaxHeight);
    }

    public void UpdateMeshHeights(Material material, float minHeight, float maxHeight)
    {
        _savedMinHeight = minHeight;
        _savedMaxHeight = maxHeight;

        material.SetFloat("minHeight", minHeight);
        material.SetFloat("maxHeight", maxHeight);
    }
}
