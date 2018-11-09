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

    public float minHeight
    {
        get
        {
            return uniformScale * heightMultiplier * heightCurve.Evaluate(0);
        }
    }
    public float maxHeight
    {
        get
        {
            return uniformScale * heightMultiplier * heightCurve.Evaluate(1);
        }
    }
}
