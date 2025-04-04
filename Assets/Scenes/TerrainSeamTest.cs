using UnityEngine;

public class TerrainSeamTest : MonoBehaviour
{
    public int chunkSize = 256;
    public int resolution = 257; // Always use vertices = chunks + 1
    public float maxHeight = 50f;
    public int seed = 12345;
    public bool useGlobalCoordinates = true;

    // Add material field
    public Material terrainMaterial;

    void Start()
    {
        // Create default material if none is assigned
        if (terrainMaterial == null)
        {
            // Create a basic material with a simple color
            terrainMaterial = new Material(Shader.Find("Nature/Terrain/Standard"));
            terrainMaterial.color = new Color(0.4f, 0.6f, 0.2f); // Green color for grass
        }

        // Create a 2x2 grid of terrain chunks
        for (int x = 0; x < 2; x++)
        {
            for (int z = 0; z < 2; z++)
            {
                CreateTerrainChunk(new Vector2Int(x, z));
            }
        }
    }

    void CreateTerrainChunk(Vector2Int coord)
    {
        GameObject chunkObj = new GameObject($"Chunk_{coord.x}_{coord.y}");
        Terrain terrain = chunkObj.AddComponent<Terrain>();
        TerrainCollider collider = chunkObj.AddComponent<TerrainCollider>();

        TerrainData terrainData = new TerrainData();
        terrainData.heightmapResolution = resolution;
        terrainData.size = new Vector3(chunkSize, maxHeight, chunkSize);

        // Set terrain layers to avoid purple color
        TerrainLayer terrainLayer = new TerrainLayer();
        terrainLayer.diffuseTexture = Texture2D.whiteTexture; // Use white texture as a base
        terrainLayer.tileSize = new Vector2(10, 10);
        terrainData.terrainLayers = new TerrainLayer[] { terrainLayer };

        // Generate heightmap
        float[,] heights = new float[resolution, resolution];

        for (int x = 0; x < resolution; x++)
        {
            for (int z = 0; z < resolution; z++)
            {
                float xCoord, zCoord;

                if (useGlobalCoordinates)
                {
                    // Global coordinates - ensures matching edges
                    xCoord = (coord.x * (resolution - 1) + x) * 0.01f;
                    zCoord = (coord.y * (resolution - 1) + z) * 0.01f;
                }
                else
                {
                    // Local coordinates - will cause seams
                    xCoord = x * 0.01f + coord.x;
                    zCoord = z * 0.01f + coord.y;
                }

                heights[z, x] = Mathf.PerlinNoise(xCoord + seed, zCoord + seed);
            }
        }

        terrainData.SetHeights(0, 0, heights);

        // Assign the terrain data and material
        terrain.terrainData = terrainData;
        terrain.materialTemplate = terrainMaterial;
        collider.terrainData = terrainData;

        // Enable auto-connect for neighbors
        terrain.allowAutoConnect = true;

        // Position the chunk
        chunkObj.transform.position = new Vector3(
            coord.x * chunkSize,
            0,
            coord.y * chunkSize
        );

        // Set this terrain's neighbors
        Terrain leftTerrain = null, topTerrain = null, rightTerrain = null, bottomTerrain = null;

        // Find neighbors (in a real system you'd store these references)
        if (coord.x > 0)
            leftTerrain = GameObject.Find($"Chunk_{coord.x - 1}_{coord.y}")?.GetComponent<Terrain>();
        if (coord.y > 0)
            bottomTerrain = GameObject.Find($"Chunk_{coord.x}_{coord.y - 1}")?.GetComponent<Terrain>();

        // Connect existing neighbors 
        terrain.SetNeighbors(leftTerrain, topTerrain, rightTerrain, bottomTerrain);

        // Update neighbors to connect to this terrain
        if (leftTerrain)
            leftTerrain.SetNeighbors(null, null, terrain, null);
        if (bottomTerrain)
            bottomTerrain.SetNeighbors(null, terrain, null, null);
    }
}
