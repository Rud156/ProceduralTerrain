using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Noise
{
    public enum NormalizedMode
    {
        Local, // Single Chunk Mode
        Global // Large Terrain System using multiple chunks Mode
    }

    public static float[,] GenerateNoiseMap(int mapWidth, int mapHeight, int seed, float scale,
        int octaves, float persistance, float lacunarity,
        Vector2 offset, NormalizedMode normalizedMode)
    {
        float[,] noiseMap = new float[mapWidth, mapHeight];

        System.Random rnd = new System.Random(seed);
        Vector2[] octaveOffset = new Vector2[octaves];

        float maxPossibleHeight = 0;
        float amplitude = 1;
        float frequency = 1;

        for (int i = 0; i < octaves; i++)
        {
            float offsetX = rnd.Next(-100000, 100000) + offset.x;
            float offsetY = rnd.Next(-100000, 100000) - offset.y;

            octaveOffset[i] = new Vector2(offsetX, offsetY);

            maxPossibleHeight += amplitude;
            amplitude *= persistance;
        }

        if (scale <= 0)
            scale = 0.0001f;

        float maxLocalNoiseHeight = float.MinValue;
        float minLocalNoiseHeight = float.MaxValue;

        float halfWidth = mapWidth / 2f;
        float halfHeight = mapHeight / 2f;

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                amplitude = 1;
                frequency = 1;
                float noiseHeight = 0;

                for (int i = 0; i < octaves; i++)
                {
                    float sampleX = (x - halfWidth + octaveOffset[i].x) / scale * frequency;
                    float sampleY = (y - halfHeight + octaveOffset[i].y) / scale * frequency;

                    // * 2 - 1 Added to make the noise values go between -1 to 1
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistance;
                    frequency *= lacunarity;

                    if (noiseHeight > maxLocalNoiseHeight)
                        maxLocalNoiseHeight = noiseHeight;
                    else if (noiseHeight < minLocalNoiseHeight)
                        minLocalNoiseHeight = noiseHeight;
                }

                noiseMap[x, y] = noiseHeight;
            }
        }

        for (int x = 0; x < mapWidth; x++)
            for (int y = 0; y < mapHeight; y++)
            {
                // Use this in case only 1 chunk is to be generated.
                // Then the full range of the noise map is used
                if (normalizedMode == NormalizedMode.Local)
                    noiseMap[x, y] = Mathf.InverseLerp(minLocalNoiseHeight,
                        maxLocalNoiseHeight, noiseMap[x, y]);
                else
                {
                    float normalizedHeight = (noiseMap[x, y] + 1) / (maxPossibleHeight);
                    noiseMap[x, y] = Mathf.Clamp(normalizedHeight, 0, int.MaxValue);
                }
            }

        return noiseMap;
    }
}
