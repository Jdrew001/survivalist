using Assets.Game.Systems.TerrainSystem.Biomes;
using Assets.Game.Systems.TerrainSystem.Generators;
using Assets.Game.Systems.TerrainSystem.TerrainChunk;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Game.Systems.TerrainSystem.TerrainChunk
{
    public class EndlessTerrainManager : MonoBehaviour
    {
        [Header("Terrain Settings")]
        [SerializeField] private int chunkSize = 256;
        [SerializeField] private int viewDistance = 3;
        [SerializeField] private Transform viewer;
        [SerializeField] private float updateVisibilityTime = 0.2f;

        [Header("Terrain Generation")]
        [SerializeField] private BiomeManager biomeManager;
        [SerializeField] private int seed = 0;
        [SerializeField] private bool randomizeSeeds = true;

        [Header("LOD Settings")]
        [SerializeField] private bool useLOD = true;
        [SerializeField] private int[] lodResolutions = new int[] { 255, 129, 65 }; // For close, medium, far

        // Terrain generation dependencies
        private TerrainGenerator terrainGenerator;
        private CombinedNoiseGenerator noiseGenerator;

        // Chunk management
        private Dictionary<Vector2Int, TerrainChunk> terrainChunks = new Dictionary<Vector2Int, TerrainChunk>();
        private List<TerrainChunk> visibleChunks = new List<TerrainChunk>();

        private Vector2Int currentChunkCoord;
        private GameObject chunkParent;

        private Queue<Vector2Int> chunkGenerationQueue = new Queue<Vector2Int>();
        [SerializeField] private int maxChunksPerFrame = 1;

        private Vector3 viewerPositionOld;

        private void Awake()
        {
            // Ensure a viewer is assigned (fallback to main camera if necessary)
            if (viewer == null)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    viewer = mainCamera.transform;
                }
            }

            if (biomeManager == null)
            {
                biomeManager = FindObjectOfType<BiomeManager>();
            }

            // Create a parent GameObject for all chunks
            chunkParent = new GameObject("Terrain Chunks");

            // Initialize noise and terrain generators
            InitializeGenerators();

            // Begin asynchronous chunk generation
            StartCoroutine(GenerateChunksOverTime());
        }

        private void Start()
        {
            if (viewer == null)
            {
                Debug.LogError("No viewer assigned to EndlessTerrainManager!");
                return;
            }

            viewerPositionOld = viewer.position;
            currentChunkCoord = WorldToChunkCoord(viewer.position);
            Debug.Log($"Initial viewer position: {viewer.position}, chunk coord: {currentChunkCoord}");

            // Immediately update the chunks and start updating visible chunks over time
            UpdateChunks();
            StartCoroutine(UpdateVisibleChunks());
        }

        private void Update()
        {
            if (viewer == null) return;

            // Check if the viewer has moved into a new chunk
            Vector2Int newChunkCoord = WorldToChunkCoord(viewer.position);
            if (newChunkCoord != currentChunkCoord)
            {
                currentChunkCoord = newChunkCoord;
                UpdateChunks();
            }
        }

        private void InitializeGenerators()
        {
            BiomeConfig currentBiome = biomeManager.GetCurrentBiome();
            if (currentBiome == null)
            {
                Debug.LogError("No active biome for terrain generation");
                return;
            }

            if (randomizeSeeds)
            {
                seed = UnityEngine.Random.Range(0, 100000);
            }

            // Create the noise generator using the current biome and seed
            noiseGenerator = new CombinedNoiseGenerator(currentBiome, seed);

            // Create the terrain generator; note that each chunk’s TerrainData will be set to (chunkSize x chunkSize)
            terrainGenerator = new TerrainGenerator(chunkSize, chunkSize, noiseGenerator);

            // Subscribe to biome change events (to regenerate chunks when needed)
            biomeManager.OnBiomeChanged += OnBiomeChanged;
        }

        /// <summary>
        /// Converts a world position into a chunk coordinate.
        /// </summary>
        private Vector2Int WorldToChunkCoord(Vector3 worldPosition)
        {
            int x = Mathf.FloorToInt(worldPosition.x / chunkSize);
            int z = Mathf.FloorToInt(worldPosition.z / chunkSize);
            return new Vector2Int(x, z);
        }

        /// <summary>
        /// Enqueue any chunks within view distance that aren’t already loaded.
        /// </summary>
        private void UpdateChunks()
        {
            for (int xOffset = -viewDistance; xOffset <= viewDistance; xOffset++)
            {
                for (int zOffset = -viewDistance; zOffset <= viewDistance; zOffset++)
                {
                    Vector2Int chunkCoord = new Vector2Int(
                        currentChunkCoord.x + xOffset,
                        currentChunkCoord.y + zOffset
                    );

                    if (!terrainChunks.ContainsKey(chunkCoord) && !chunkGenerationQueue.Contains(chunkCoord))
                    {
                        chunkGenerationQueue.Enqueue(chunkCoord);
                    }
                }
            }
        }

        /// <summary>
        /// Coroutine to update the visibility of chunks at fixed intervals.
        /// </summary>
        private IEnumerator UpdateVisibleChunks()
        {
            while (true)
            {
                // Update visibility if the viewer has moved significantly
                if (Vector3.Distance(viewer.position, viewerPositionOld) > 10)
                {
                    viewerPositionOld = viewer.position;
                    UpdateChunkVisibility();
                }

                yield return new WaitForSeconds(updateVisibilityTime);
            }
        }

        /// <summary>
        /// Enables chunks within view and disables those outside view distance.
        /// Optionally, you could also remove chunks that are too far.
        /// </summary>
        private void UpdateChunkVisibility()
        {
            visibleChunks.Clear();

            foreach (var kvp in terrainChunks)
            {
                TerrainChunk chunk = kvp.Value;
                Vector2Int coord = kvp.Key;

                // Using grid-distance; you could also use actual world distance if desired
                int distX = Mathf.Abs(currentChunkCoord.x - coord.x);
                int distZ = Mathf.Abs(currentChunkCoord.y - coord.y);
                int maxDist = Mathf.Max(distX, distZ);

                if (maxDist <= viewDistance)
                {
                    chunk.gameObject.SetActive(true);
                    visibleChunks.Add(chunk);
                }
                else
                {
                    chunk.gameObject.SetActive(false);
                    // Optionally, destroy chunks that are very far away:
                    // Destroy(chunk.gameObject);
                    // terrainChunks.Remove(coord);
                }
            }

            // Update neighbors so that chunk borders connect seamlessly
            UpdateChunkNeighbors();
        }

        /// <summary>
        /// Sets neighboring chunks on each visible chunk to avoid seams.
        /// </summary>
        private void UpdateChunkNeighbors()
        {
            foreach (TerrainChunk chunk in visibleChunks)
            {
                Vector2Int coord = chunk.chunkCoord;

                terrainChunks.TryGetValue(new Vector2Int(coord.x - 1, coord.y), out TerrainChunk leftChunk);
                terrainChunks.TryGetValue(new Vector2Int(coord.x, coord.y + 1), out TerrainChunk topChunk);
                terrainChunks.TryGetValue(new Vector2Int(coord.x + 1, coord.y), out TerrainChunk rightChunk);
                terrainChunks.TryGetValue(new Vector2Int(coord.x, coord.y - 1), out TerrainChunk bottomChunk);

                chunk.SetNeighbors(leftChunk, topChunk, rightChunk, bottomChunk);
            }
        }

        /// <summary>
        /// Creates a new chunk at the given coordinate.
        /// </summary>
        private TerrainChunk CreateChunk(Vector2Int coord)
        {
            Debug.Log($"CreateChunk({coord}) called");

            // Determine LOD level based on grid distance
            int gridDistance = Mathf.Max(Mathf.Abs(currentChunkCoord.x - coord.x), Mathf.Abs(currentChunkCoord.y - coord.y));
            int lodLevel = 0;
            if (useLOD)
            {
                if (gridDistance > 1) lodLevel = 1;
                if (gridDistance > 2 && lodResolutions.Length > 2) lodLevel = 2;
            }

            // Set resolution for this chunk based on LOD
            int resolution = lodResolutions[lodLevel];
            terrainGenerator.UpdateParameters(resolution, resolution);

            // Create a new GameObject for the chunk
            GameObject chunkObject = new GameObject($"Chunk_{coord.x}_{coord.y}");
            chunkObject.transform.parent = chunkParent.transform;

            // Add the TerrainChunk component
            TerrainChunk chunk = chunkObject.AddComponent<TerrainChunk>();
            Debug.Log($"TerrainChunk component added to {chunkObject.name}");

            // Get the current biome (you could extend this to blend biomes based on position)
            BiomeConfig biome = biomeManager.GetCurrentBiome();
            if (biome == null)
            {
                Debug.LogError("No biome available for chunk!");
                return null;
            }

            Debug.Log($"Initializing chunk with biome: {biome.biomeName}");

            // Initialize the chunk (the chunk will position itself using its coord)
            chunk.Initialize(coord, chunkSize, terrainGenerator, biome);
            Debug.Log("Chunk initialized successfully");

            // Add the chunk to our dictionary
            terrainChunks.Add(coord, chunk);
            return chunk;
        }

        /// <summary>
        /// Handles biome changes by updating the noise generator and regenerating visible chunks.
        /// </summary>
        private void OnBiomeChanged(BiomeConfig newBiome)
        {
            noiseGenerator.UpdateBiomeConfig(newBiome);

            foreach (TerrainChunk chunk in visibleChunks)
            {
                chunk.GenerateTerrain();
            }

            UpdateChunkNeighbors();
        }

        /// <summary>
        /// Processes the chunk generation queue over multiple frames.
        /// </summary>
        private IEnumerator GenerateChunksOverTime()
        {
            while (true)
            {
                int chunksThisFrame = 0;
                while (chunkGenerationQueue.Count > 0 && chunksThisFrame < maxChunksPerFrame)
                {
                    Vector2Int coord = chunkGenerationQueue.Dequeue();
                    if (!terrainChunks.ContainsKey(coord))
                    {
                        CreateChunk(coord);
                        chunksThisFrame++;
                    }
                }
                yield return null;
            }
        }

        private void OnDestroy()
        {
            if (biomeManager != null)
            {
                biomeManager.OnBiomeChanged -= OnBiomeChanged;
            }
        }
    }
}
