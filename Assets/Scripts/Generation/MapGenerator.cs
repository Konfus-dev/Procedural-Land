using System.Collections.Generic;
using System;
using System.Threading;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{

    public enum DrawMode { NoiseMap, ColorMap, Mesh};
    public const int mapChunkSize = 241;

    [Header("Mesh Settings")]
    public DrawMode drawMode;
    [Range(0, 6)]
    public int levelOfDetail;
    public float meshHeightMultiplier;
    public int seed;
    public float noiseScale;
    public int octaves;
    public float lacunarity;
    [Range(0, 1)]
    public float persistance;
    public Vector2 offset;
    public AnimationCurve meshHeightCurve;
    public bool autoUpdate;

    [Header("Material Stuff")]
    public TerrainType[] regions;

    [Header("Erosion Settings")]
    public ComputeShader erosionShader;
    public Erosion erosion;
    public int numErosionIterations = 50000;
    public int erosionBrushRadius = 3;

    public int maxLifetime = 30;
    public float sedimentCapacityFactor = 3;
    public float minSedimentCapacity = .01f;
    public float depositSpeed = 0.3f;
    public float erodeSpeed = 0.3f;

    public float evaporateSpeed = .01f;
    public float gravity = 4;
    public float startSpeed = 1;
    public float startWater = 1;
    [Range(0, 1)]
    public float inertia = 0.3f;

    // Internal
    int mapSizeWithBorder;
    Queue<MapThreadInfo<MapData>> mapDataThreadQueue = new Queue<MapThreadInfo<MapData>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadQueue = new Queue<MapThreadInfo<MeshData>>();

    private void Start()
    {
        //DrawMap();
    }

    private void Update()
    {
        if (mapDataThreadQueue.Count > 0)
        {
            for (int i = 0; i < mapDataThreadQueue.Count; i++)
            {
                MapThreadInfo<MapData> threadInfo = mapDataThreadQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
        if (meshDataThreadQueue.Count > 0)
        {
            for (int i = 0; i < meshDataThreadQueue.Count; i++)
            {
                MapThreadInfo<MeshData> threadInfo = meshDataThreadQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }

    public void DrawMap()
    {
        float[] noiseMap = GenerateNoiseMap();
        MapData mapData = GenerateMapData(noiseMap);
        MapDisplay display = FindObjectOfType<MapDisplay>();
        if (drawMode == DrawMode.NoiseMap) display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.noiseMap));
        else if (drawMode == DrawMode.ColorMap) display.DrawTexture(TextureGenerator
            .TextureFromColorMap(mapData.colorMap, mapSizeWithBorder, mapSizeWithBorder));
        else if (drawMode == DrawMode.Mesh) display.DrawMesh(MeshGenerator
            .GenerateTerrainMesh(mapData.noiseMap, meshHeightMultiplier, meshHeightCurve, levelOfDetail, mapChunkSize, mapSizeWithBorder, erosionBrushRadius, noiseScale), TextureGenerator.TextureFromColorMap(mapData.colorMap, mapSizeWithBorder, mapSizeWithBorder));
    }

    public void RequestMapData(Action<MapData> callback)
    {
        float[] noiseMap = GenerateNoiseMap();
        
        ThreadStart threadStart = delegate
        {
            MapDataThread(callback, noiseMap);
        };

        new Thread(threadStart).Start();
    }
    
    private void MapDataThread(Action<MapData> callback, float[] noiseMap)
    {
        MapData mapData = GenerateMapData(noiseMap);
        
        lock(mapDataThreadQueue)
        {
            mapDataThreadQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }

    public void RequestMeshData(MapData mapData, Action<MeshData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MeshDataThread(mapData, callback);
        };

        new Thread(threadStart).Start();
    }

    private void MeshDataThread(MapData mapData, Action<MeshData> callback)
    {
        MeshData meshData = MeshGenerator
            .GenerateTerrainMesh(mapData.noiseMap, meshHeightMultiplier, meshHeightCurve, levelOfDetail, mapChunkSize, mapSizeWithBorder, erosionBrushRadius, noiseScale);
        
        lock (meshDataThreadQueue)
        {
            meshDataThreadQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }

    private float[] GenerateNoiseMap()
    {
        mapSizeWithBorder = mapChunkSize + erosionBrushRadius * 2;
        float[] noiseMap = Noise.GenerateNoiseMap(mapSizeWithBorder, mapSizeWithBorder, seed, noiseScale, octaves, persistance, lacunarity, offset);

        erosion.Erode(erosionShader, numErosionIterations, erosionBrushRadius, mapSizeWithBorder, noiseMap, maxLifetime, inertia, sedimentCapacityFactor, minSedimentCapacity, depositSpeed, erodeSpeed, evaporateSpeed, gravity, startSpeed, startWater);

        return noiseMap;
    }
    private MapData GenerateMapData(float[] noiseMap)
    {
       /* mapSizeWithBorder = mapChunkSize + erosionBrushRadius * 2;
        float[] noiseMap = Noise.GenerateNoiseMap(mapSizeWithBorder, mapSizeWithBorder, seed, noiseScale, octaves, persistance, lacunarity, offset);

        erosion.Erode(erosionShader, numErosionIterations, erosionBrushRadius, mapSizeWithBorder, noiseMap, maxLifetime, inertia, sedimentCapacityFactor, minSedimentCapacity, depositSpeed, erodeSpeed, evaporateSpeed, gravity, startSpeed, startWater);*/

        Color[] colorMap = new Color[mapSizeWithBorder * mapSizeWithBorder];
        for (int y = 0; y < mapSizeWithBorder; y++)
        {
            for (int x = 0; x < mapSizeWithBorder; x++)
            {
                float currentHeight = noiseMap[x * mapSizeWithBorder + y];
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight <= regions[i].height)
                    {
                        colorMap[y * mapSizeWithBorder + x] = regions[i].color;
                        break;
                    }
                }
            }

        }

        return new MapData(noiseMap, colorMap);
    }

    private void OnValidate()
    {
        if (lacunarity < 1) lacunarity = 1;
        if (octaves < 0) octaves = 0;
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

[System.Serializable]
public struct TerrainType
{
    public string name;
    public float height;
    public Color color;
    public Texture2D texture;
}

public struct MapData
{
    public readonly float[] noiseMap;
    public readonly Color[] colorMap;

    public MapData(float[] noiseMap, Color[] colorMap)
    {
        this.noiseMap = noiseMap;
        this.colorMap = colorMap;
    }
}