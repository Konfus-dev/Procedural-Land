using UnityEngine;

public static class Noise
{

	public static float[] GenerateNoiseMap(int mapWidth, int mapHeight, int seed, float initScale, int octaves, float persistance, float lacunarity, Vector2 offset)
	{
        var map = new float[mapWidth * mapHeight];
        var prng = new System.Random(seed);

        Vector2[] offsets = new Vector2[octaves];
        for (int i = 0; i < octaves; i++)
        {
            offsets[i] = new Vector2(prng.Next(-1000, 1000), prng.Next(-1000, 1000));
        }

        float minValue = float.MaxValue;
        float maxValue = float.MinValue;

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                float noiseValue = 0;
                float scale = initScale;
                float weight = 1;
                for (int i = 0; i < octaves; i++)
                {
                    Vector2 p = offsets[i] + new Vector2(x / (float)mapWidth, y / (float)mapWidth) * scale;
                    noiseValue += Mathf.PerlinNoise(p.x, p.y) * weight;
                    weight *= persistance;
                    scale *= lacunarity;
                }
                map[y * mapWidth + x] = noiseValue;
                minValue = Mathf.Min(noiseValue, minValue);
                maxValue = Mathf.Max(noiseValue, maxValue);
            }
        }

        // Normalize
        if (maxValue != minValue)
        {
            for (int i = 0; i < map.Length; i++)
            {
                map[i] = (map[i] - minValue) / (maxValue - minValue);
            }
        }

        return map;
        
    }

}