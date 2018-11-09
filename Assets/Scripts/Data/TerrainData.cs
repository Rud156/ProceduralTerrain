using UnityEngine;

[CreateAssetMenu(fileName = "TerrainData", menuName = "Terrain/Terrain")]
public class TerrainData : UpdatebleData
{
    public float uniformScale = 2;

    [Header("Mesh Data")]
    public float heightMultiplier;
    public AnimationCurve heightCurve;

    [Header("Color Data")]
    public bool useFlatShading;
    public bool useFalloff;
}
