using Assets.Game.Systems.TerrainSystem.Biomes;
using Assets.Game.Systems.TerrainSystem.Generators;
using System.Collections;
using UnityEngine;

namespace Assets.Game.Systems.TerrainSystem.TerrainChunk
{
    public class TerrainChunk : MonoBehaviour
    {
        public Vector2Int chunkCoord;
        public Terrain terrain;
        public TerrainData terrainData;

        private TerrainGenerator terrainGenerator;
        private BiomeConfig biomeConfig;

        [SerializeField] private Material terrainMaterial; // Assign a valid material in the Inspector

        /// <summary>
        /// Initializes this chunk with its coordinate, size, terrain generator, and biome.
        /// The chunk’s world position is set based on its grid coordinate.
        /// </summary>
        public void Initialize(Vector2Int coord, int chunkSize, TerrainGenerator generator, BiomeConfig biome)
        {
            chunkCoord = coord;
            terrainGenerator = generator;
            biomeConfig = biome;

            // Set the world position based on chunk coordinate and size
            transform.position = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);

            // Create or add a Terrain component if not already added
            if (terrain == null)
            {
                terrain = gameObject.AddComponent<Terrain>();

                // Create a dummy TerrainData with a minimal resolution
                TerrainData dummyData = new TerrainData();
                dummyData.heightmapResolution = 33; // Minimal placeholder resolution
                dummyData.size = new Vector3(chunkSize, biome.maxHeight, chunkSize);
                terrain.terrainData = dummyData;

                // Add a TerrainCollider and assign the dummy data
                TerrainCollider terrainCollider = gameObject.AddComponent<TerrainCollider>();
                terrainCollider.terrainData = dummyData;

                // Use a custom material if one is assigned; otherwise, fallback to a built-in type
                if (terrainMaterial != null)
                {
                    terrain.materialType = Terrain.MaterialType.Custom;
                    terrain.materialTemplate = terrainMaterial;
                }
                else
                {
                    terrain.materialType = Terrain.MaterialType.BuiltInStandard;
                }
            }

            // Start asynchronous terrain generation to avoid freezing the main thread
            StartCoroutine(GenerateTerrainAsync());
        }

        /// <summary>
        /// Generates terrain asynchronously.
        /// </summary>
        private IEnumerator GenerateTerrainAsync()
        {
            Debug.Log($"Starting async terrain generation for Chunk_{chunkCoord.x}_{chunkCoord.y}");
            yield return null; // Spread out generation over frames

            try
            {
                // Generate the terrain data with the generator using the chunk coordinate offset
                TerrainData newTerrainData = terrainGenerator.GenerateTerrain(biomeConfig, chunkCoord);
                if (newTerrainData != null)
                {
                    terrainData = newTerrainData;
                    terrain.terrainData = terrainData;

                    TerrainCollider collider = GetComponent<TerrainCollider>();
                    if (collider != null)
                    {
                        collider.terrainData = terrainData;
                    }
                    Debug.Log($"Async terrain generation complete for Chunk_{chunkCoord.x}_{chunkCoord.y}");
                }
                else
                {
                    Debug.LogError($"TerrainGenerator returned null terrainData for chunk {chunkCoord}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in async terrain generation for chunk {chunkCoord}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Regenerates the terrain synchronously (e.g. when a biome change occurs).
        /// </summary>
        public void GenerateTerrain()
        {
            terrainData = terrainGenerator.GenerateTerrain(biomeConfig, chunkCoord);
            terrain.terrainData = terrainData;

            TerrainCollider collider = GetComponent<TerrainCollider>();
            if (collider != null)
            {
                collider.terrainData = terrainData;
            }
        }

        /// <summary>
        /// Sets neighboring terrains to avoid visible seams.
        /// </summary>
        public void SetNeighbors(TerrainChunk left, TerrainChunk top, TerrainChunk right, TerrainChunk bottom)
        {
            Terrain leftTerrain = left?.terrain;
            Terrain topTerrain = top?.terrain;
            Terrain rightTerrain = right?.terrain;
            Terrain bottomTerrain = bottom?.terrain;

            terrain.SetNeighbors(leftTerrain, topTerrain, rightTerrain, bottomTerrain);
        }
    }
}
