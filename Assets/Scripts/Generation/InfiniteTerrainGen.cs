using System.Collections.Generic;
using UnityEngine;

public class InfiniteTerrainGen : MonoBehaviour
{
	const float viewerMoveThresholdForChunkUpdate = 25f;
	const float sqrViewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;

	public LODInfo[] detailLevels;
	public static float maxViewDst;

	public Transform viewer;
	public Material mapMaterial;

	public static Vector2 viewerPosition;
	Vector2 viewerPositionOld;
	static MapGenerator mapGenerator;
	int chunkSize;
	int chunksVisibleInViewDist;

	Dictionary<Vector2, TerrainChunk> chunkDictionary = new Dictionary<Vector2, TerrainChunk>();
	List<TerrainChunk> chunksVisibleLastUpdate    = new List<TerrainChunk>();

	void Start()
	{
		mapGenerator = FindObjectOfType<MapGenerator>();

		maxViewDst = detailLevels[detailLevels.Length - 1].visibleDstThreshold;
		chunkSize = mapGenerator.mapSizeWithBorder - 1;
		chunksVisibleInViewDist = Mathf.RoundToInt(maxViewDst / chunkSize);

		UpdateVisibleChunks();
	}

	void Update()
	{
		viewerPosition = new Vector2(viewer.position.x, viewer.position.z);

		if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrViewerMoveThresholdForChunkUpdate)
		{
			viewerPositionOld = viewerPosition;
			UpdateVisibleChunks();
		}
	}

	void UpdateVisibleChunks()
	{

		for (int i = 0; i < chunksVisibleLastUpdate.Count; i++)
		{
			chunksVisibleLastUpdate[i].SetVisible(false);
		}
		chunksVisibleLastUpdate.Clear();

		int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
		int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

		for (int yOffset = -chunksVisibleInViewDist; yOffset <= chunksVisibleInViewDist; yOffset++)
		{
			for (int xOffset = -chunksVisibleInViewDist; xOffset <= chunksVisibleInViewDist; xOffset++)
			{
				Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

				if (chunkDictionary.ContainsKey(viewedChunkCoord))
				{
					chunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
					if (chunkDictionary[viewedChunkCoord].IsVisible())
					{
						chunksVisibleLastUpdate.Add(chunkDictionary[viewedChunkCoord]);
					}
				}
				else
				{
					chunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, chunkSize, detailLevels, transform, mapMaterial));
				}

			}
		}
	}

	public class TerrainChunk
	{

		GameObject meshObject;
		Vector2 position;
		Bounds bounds;

		MeshRenderer meshRenderer;
		MeshFilter meshFilter;

		LODInfo[] detailLevels;
		LODMesh[] lodMeshes;

		MapData mapData;
		bool mapDataReceived;
		int previousLODIndex = -1;

		public TerrainChunk(Vector2 coord, int size, LODInfo[] detailLevels, Transform parent, Material material)
		{
			this.detailLevels = detailLevels;

			position = coord * size;
			bounds = new Bounds(position, Vector2.one * size);
			Vector3 positionV3 = new Vector3(position.x, 0, position.y);

			meshObject = new GameObject("Terrain Chunk");
			meshRenderer = meshObject.AddComponent<MeshRenderer>();
			meshFilter = meshObject.AddComponent<MeshFilter>();
			meshRenderer.material = material;

			meshObject.transform.position = positionV3;
			meshObject.transform.parent = parent;
			SetVisible(false);

			lodMeshes = new LODMesh[detailLevels.Length];
			for (int i = 0; i < detailLevels.Length; i++)
			{
				lodMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateTerrainChunk);
			}

			mapGenerator.RequestMapData(position, OnMapDataReceived);
		}

		void OnMapDataReceived(MapData mapData)
		{
			this.mapData = mapData;
			mapDataReceived = true;

			Texture2D texture = TextureGenerator.TextureFromColorMap(mapData.colorMap, mapGenerator.mapSizeWithBorder, mapGenerator.mapSizeWithBorder);
			meshRenderer.material.mainTexture = texture;

			UpdateTerrainChunk();
		}



		public void UpdateTerrainChunk()
		{
			if (mapDataReceived)
			{
				float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
				bool visible = viewerDstFromNearestEdge <= maxViewDst;

				if (visible)
				{
					int lodIndex = 0;

					for (int i = 0; i < detailLevels.Length - 1; i++)
					{
						if (viewerDstFromNearestEdge > detailLevels[i].visibleDstThreshold)
						{
							lodIndex = i + 1;
						}
						else
						{
							break;
						}
					}

					if (lodIndex != previousLODIndex)
					{
						LODMesh lodMesh = lodMeshes[lodIndex];
						if (lodMesh.hasMesh)
						{
							previousLODIndex = lodIndex;
							meshFilter.mesh = lodMesh.mesh;
							meshFilter.sharedMesh = lodMesh.mesh;
						}
						else if (!lodMesh.hasRequestedMesh)
						{
							lodMesh.RequestMesh(mapData);
						}
					}
				}

				SetVisible(visible);
			}
		}

		public void SetVisible(bool visible)
		{
			meshObject.SetActive(visible);
		}

		public bool IsVisible()
		{
			return meshObject.activeSelf;
		}

	}

	class LODMesh
	{

		public Mesh mesh;
		public bool hasRequestedMesh;
		public bool hasMesh;
		int lod;
		System.Action updateCallback;

		public LODMesh(int lod, System.Action updateCallback)
		{
			this.lod = lod;
			this.updateCallback = updateCallback;
		}

		void OnMeshDataReceived(MeshData meshData)
		{
			mesh = meshData.CreateMesh();
			hasMesh = true;

			updateCallback();
		}

		public void RequestMesh(MapData mapData)
		{
			hasRequestedMesh = true;
			mapGenerator.RequestMeshData(mapData, lod, OnMeshDataReceived);
		}

	}

	[System.Serializable]
	public struct LODInfo
	{
		public int lod;
		public float visibleDstThreshold;
	}
}
