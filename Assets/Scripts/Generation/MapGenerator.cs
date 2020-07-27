using System.Collections.Generic;
using System;
using System.Threading;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{

	public enum DrawMode { NoiseMap, ColourMap, Mesh };
	public DrawMode drawMode;

	public const int mapChunkSize = 241;
	[HideInInspector]
	public int mapSizeWithBorder;

	[Range(0, 6)]
	public int editorPreviewLOD;
	public float noiseScale;

	public int octaves;
	[Range(0, 1)]
	public float persistance;
	public float lacunarity;

	public int seed;
	public Vector2 offset;

	public float meshHeightMultiplier;
	public AnimationCurve meshHeightCurve;

	public bool autoUpdate;

	public TerrainType[] regions;

	public ComputeShader heightMapComputeShader;

	//private
	bool inEditor = false;

	Erosion erosion;

	Queue<float[]> noiseMapQueue = new Queue<float[]>();
	Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
	Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

    private void Start()
    {
		GameObject editorPreviewMesh = GameObject.Find("Mesh");
		editorPreviewMesh.SetActive(false);
		erosion = GetComponent<Erosion>();
		mapSizeWithBorder = mapChunkSize + erosion.erosionBrushRadius * 2;
    }

	private void Update()
	{
		if (noiseMapQueue.Count > 0)
		{
			for (int i = 0; i < noiseMapQueue.Count; i++)
			{
				float[] map = noiseMapQueue.Dequeue();
				erosion.Erode(mapSizeWithBorder, map);
			}
		}

		if (mapDataThreadInfoQueue.Count > 0)
		{
			for (int i = 0; i < mapDataThreadInfoQueue.Count; i++)
			{
				MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
				threadInfo.callback(threadInfo.parameter);
			}
		}

		if (meshDataThreadInfoQueue.Count > 0)
		{
			for (int i = 0; i < meshDataThreadInfoQueue.Count; i++)
			{
				MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
				threadInfo.callback(threadInfo.parameter);
			}
		}
	}

	public void DrawMapInEditor()
	{
		inEditor = true;
		erosion = GetComponent<Erosion>();
		MapData mapData = GenerateMapData(Vector2.zero);
		mapSizeWithBorder = mapChunkSize + erosion.erosionBrushRadius * 2;

		MapDisplay display = FindObjectOfType<MapDisplay>();
		if (drawMode == DrawMode.NoiseMap)
		{
			display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
		}
		else if (drawMode == DrawMode.ColourMap)
		{
			display.DrawTexture(TextureGenerator.TextureFromColorMap(mapData.colorMap, mapSizeWithBorder, mapSizeWithBorder));
		}
		else if (drawMode == DrawMode.Mesh)
		{
			display.DrawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap, mapSizeWithBorder, mapSizeWithBorder, meshHeightMultiplier, meshHeightCurve, editorPreviewLOD), TextureGenerator.TextureFromColorMap(mapData.colorMap, mapSizeWithBorder, mapSizeWithBorder));
		}
	}

    public void RequestMapData(Vector2 centre, Action<MapData> callback)
	{
        ThreadStart threadStart = delegate
        {
            MapDataThread(centre, callback);
        };

        new Thread(threadStart).Start();
    }

    private void MapDataThread(Vector2 centre, Action<MapData> callback)
    {
        MapData mapData = GenerateMapData(centre);

        lock (mapDataThreadInfoQueue)
        {
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
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

	void MeshDataThread(MapData mapData, int lod, Action<MeshData> callback)
	{
		MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap, mapSizeWithBorder, mapSizeWithBorder, meshHeightMultiplier, meshHeightCurve, lod);
		lock (meshDataThreadInfoQueue)
		{
			meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
		}
	}

	private float[] GenerateNoiseMap(Vector2 centre)
    {
        mapSizeWithBorder = mapChunkSize + erosion.erosionBrushRadius * 2;
        float[] noiseMap = Noise.GenerateNoiseMap(mapSizeWithBorder, mapSizeWithBorder, seed, noiseScale, octaves, persistance, lacunarity, centre + offset);
		noiseMapQueue.Enqueue(noiseMap);

		if (inEditor)
		{
			erosion.Erode(mapSizeWithBorder, noiseMap);
		}
		else
		{
			Thread.Sleep(600);
		}

		return noiseMap;
    }
    private MapData GenerateMapData(Vector2 centre)
    {
		float[] noiseMap = GenerateNoiseMap(centre);

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
}

public struct MapData
{
	public readonly float[] heightMap;
	public readonly Color[] colorMap;
	public bool generatingErosion;

	public MapData(float[] heightMap, Color[] colourMap)
	{
		this.heightMap = heightMap;
		this.colorMap = colourMap;
		this.generatingErosion = false;
	}
}