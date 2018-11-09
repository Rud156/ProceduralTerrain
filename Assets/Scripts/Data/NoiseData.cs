using UnityEngine;

[CreateAssetMenu(fileName = "NoiseData", menuName = "Terrain/Noise")]
public class NoiseData : UpdatebleData
{
    public Noise.NormalizedMode normalizedMode;
    public float noiseScale;

    [Header("Map Noise Data")]
    public int octaves;
    [Range(0, 1)]
    public float persistance;
    public float lacunarity;

    [Header("Randomness")]
    public int seed;
    public Vector2 offset;

    /// <summary>
    /// Called when the script is loaded or a value is changed in the
    /// inspector (Called in the editor only).
    /// </summary>
    protected override void OnValidate()
    {
        if (lacunarity < 1)
            lacunarity = 1;

        if (octaves < 0)
            octaves = 0;

        base.OnValidate();
    }
}
