using UnityEngine;

public static class MeshGenerator
{

	public static MeshData GenerateTerrainMesh(float[] heightMap, float heightMultiplier, AnimationCurve heightCurve, int levelOfDetail, int mapChunkSize, int mapSizeWithBorder, int erosionBrushRadius, float noiseScale)
	{
		AnimationCurve localHeightCurve = new AnimationCurve(heightCurve.keys);

		int width = mapSizeWithBorder;
		int height = mapSizeWithBorder;
		float topLeftX = (width - 1) / -2f;
		float topLeftZ = (height - 1) / 2f;

		int meshSimplificationIncrement = (levelOfDetail == 0) ? 1 : levelOfDetail * 2;
		int verticesPerLine = (width - 1) / meshSimplificationIncrement + 1;

		MeshData meshData = new MeshData(verticesPerLine, verticesPerLine);
		int vertexIndex = 0;

		for (int y = 0; y < height; y += meshSimplificationIncrement)
		{
			for (int x = 0; x < width; x += meshSimplificationIncrement)
			{
				meshData.vertices[vertexIndex] = new Vector3(topLeftX + x, localHeightCurve.Evaluate(heightMap[x * height + y]) * heightMultiplier, topLeftZ - y);
				meshData.uvs[vertexIndex] = new Vector2(x / (float)width, y / (float)height);

				if (x < width - 1 && y < height - 1)
				{
					meshData.AddTriangle(vertexIndex, (vertexIndex + verticesPerLine + 1), vertexIndex + verticesPerLine);
					meshData.AddTriangle(vertexIndex + verticesPerLine + 1, vertexIndex, vertexIndex + 1);
				}

				vertexIndex++;
			}
		}

        #region old code not work so well
        /*Vector3[] verts = new Vector3[mapChunkSize * mapChunkSize];
		int[] triangles = new int[(mapChunkSize - 1) * (mapChunkSize - 1) * 6];
		Vector2[] uvs = new Vector2[mapChunkSize * mapChunkSize];
		int t = 0;

		for (int i = 0; i < mapChunkSize * mapChunkSize; i++)
		{
			int x = i % mapChunkSize;
			int y = i / mapChunkSize;
			int borderedMapIndex = (y + erosionBrushRadius) * mapSizeWithBorder + x + erosionBrushRadius;
			int meshMapIndex = y * mapChunkSize + x;

			Vector2 percent = new Vector2(x / (mapChunkSize - 1f), y / (mapChunkSize - 1f));
			Vector3 pos = new Vector3(percent.x * 2 - 1, 0, percent.y * 2 - 1) * noiseScale;

			float normalizedHeight = heightMap[borderedMapIndex];
			pos += Vector3.up * normalizedHeight * heightMultiplier;
			verts[meshMapIndex] = pos;
			uvs[meshMapIndex] = new Vector2(x / (float)mapChunkSize, y / (float)mapChunkSize);

			// Construct triangles
			if (x != mapChunkSize - 1 && y != mapChunkSize - 1)
			{
				t = (y * (mapChunkSize - 1) + x) * 3 * 2;

				triangles[t + 0] = meshMapIndex + mapChunkSize;
				triangles[t + 1] = meshMapIndex + mapChunkSize + 1;
				triangles[t + 2] = meshMapIndex;

				triangles[t + 3] = meshMapIndex + mapChunkSize + 1;
				triangles[t + 4] = meshMapIndex + 1;
				triangles[t + 5] = meshMapIndex;
				t += 6;
			}
		}

		MeshData meshData = new MeshData();
		meshData.triangles = triangles;
		meshData.vertices = verts;*/
        #endregion

        return meshData;
	}
}

public class MeshData
{
	public Vector3[] vertices;
	public int[] triangles;
	public Vector2[] uvs;

	int triangleIndex;

	public MeshData(int meshWidth, int meshHeight)
	{
		vertices = new Vector3[meshWidth * meshHeight];
		uvs = new Vector2[meshWidth * meshHeight];
		triangles = new int[(meshWidth - 1) * (meshHeight - 1) * 6];
	}

	public void AddTriangle(int a, int b, int c)
	{
		triangles[triangleIndex] = a;
		triangles[triangleIndex + 1] = b;
		triangles[triangleIndex + 2] = c;
		triangleIndex += 3;
	}

	public Mesh CreateMesh()
	{
		Mesh mesh = new Mesh();
		mesh.vertices = vertices;
		mesh.triangles = triangles;
		mesh.uv = uvs;
		mesh.RecalculateNormals();
		return mesh;
	}

}