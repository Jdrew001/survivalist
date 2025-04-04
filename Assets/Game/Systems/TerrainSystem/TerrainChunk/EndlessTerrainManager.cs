using Assets.Game.Systems.TerrainSystem.Biomes;
using Assets.Game.Systems.TerrainSystem.Generators;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Assets.Game.Systems.TerrainSystem.TerrainChunk
{
    public class EndlessTerrainManager : MonoBehaviour
    {
        [Header("Terrain Settings")]
        [SerializeField] private int chunkSize = 256;
        [SerializeField] private int viewDistance = 3;
        [SerializeField] private float detailFalloffFactor = 1.5f;
        [SerializeField] private Transform viewer;
        [SerializeField] private LayerMask terrainLayer;
        [SerializeField] private float updateVisibilityTime = 0.2f;

        [Header("Terrain Generation")]
        [SerializeField] private BiomeManager biomeManager;
        [SerializeField] private int seed = 0;
        [SerializeField] private bool randomizeSeeds = true;
        [SerializeField] private bool useMultithreading = true;

        [Header("LOD Settings")]
        [SerializeField] private bool useLOD = true;
        [SerializeField] private int[] lodResolutions = new int[] { 255, 129, 65, 33 }; // For close, medium, far, very far
        [SerializeField] private float[] lodDistances = new float[] { 1f, 2f, 3f, 4f }; // In chunk distances

        [Header("Terrain Connection Settings")]
        [SerializeField] private Material sharedTerrainMaterial; // Assign this in the inspector
        [SerializeField] private bool debugChunkConnections = false;

        // World and chunk management
        private Dictionary<Vector2Int, TerrainChunk> terrainChunks = new Dictionary<Vector2Int, TerrainChunk>();
        private List<TerrainChunk> visibleTerrainChunks = new List<TerrainChunk>();
        private Vector2Int currentViewerChunkCoord;
        private GameObject chunkParent;
        private const float MAX_VIEW_DST = 800f;

        // Threading and pooling for optimization
        private Queue<Vector2Int> chunkGenerationQueue = new Queue<Vector2Int>();
        private Queue<TerrainChunk> chunksToUpdate = new Queue<TerrainChunk>();
        private List<TerrainChunk> chunkPool = new List<TerrainChunk>();


        [SerializeField] private int maxChunksPerFrame = 1;
        [SerializeField] private int chunkPoolSize = 10;

        // Dependencies
        private TerrainGenerator terrainGenerator;
        private CombinedNoiseGenerator noiseGenerator;

        private Vector3 viewerPositionOld;
        private bool initialChunksGenerated = false;
        private Coroutine chunkQueueCoroutine;
        private Coroutine visibilityCoroutine;

        private bool initialGenerationComplete = false;
        private List<TerrainChunk> pendingConnectionChunks = new List<TerrainChunk>();

        // Add a public method to force connection updates
        public void ForceUpdateConnections()
        {
            StartCoroutine(UpdateConnectionsDelayed());
        }

        private void Awake()
        {
            // Ensure a viewer is assigned (fallback to main camera)
            if (viewer == null)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    viewer = mainCamera.transform;
                }
                else
                {
                    Debug.LogError("No main camera found for EndlessTerrainManager!");
                }
            }

            // Find dependencies if not assigned
            if (biomeManager == null)
            {
                biomeManager = FindObjectOfType<BiomeManager>();
                if (biomeManager == null)
                {
                    Debug.LogError("No BiomeManager found in scene!");
                }
            }

            // Create parent for chunks
            chunkParent = new GameObject("Terrain Chunks");
            chunkParent.transform.SetParent(transform);

            // Initialize systems
            InitializeGenerators();
            InitializeChunkPool();
        }

        private void Start()
        {
            if (viewer == null) return;

            viewerPositionOld = viewer.position;
            currentViewerChunkCoord = WorldToChunkCoord(viewer.position);

            // Start system coroutines
            visibilityCoroutine = StartCoroutine(UpdateVisibleChunks());
            chunkQueueCoroutine = StartCoroutine(ProcessChunkQueue());

            // Initial chunk generation
            GenerateInitialChunks();
        }

        private void Update()
        {
            if (viewer == null) return;

            // When moving to a new chunk, update visible chunks
            Vector2Int newChunkCoord = WorldToChunkCoord(viewer.position);
            if (newChunkCoord != currentViewerChunkCoord)
            {
                currentViewerChunkCoord = newChunkCoord;
                QueueChunksInViewDistance();
            }
        }

        private void OnDestroy()
        {
            // Stop all coroutines when destroyed
            if (chunkQueueCoroutine != null)
            {
                StopCoroutine(chunkQueueCoroutine);
            }

            if (visibilityCoroutine != null)
            {
                StopCoroutine(visibilityCoroutine);
            }

            StopAllCoroutines();

            // Clean up chunks
            foreach (var chunk in terrainChunks.Values)
            {
                if (chunk != null)
                {
                    chunk.PrepareForDeactivation();
                }
            }

            if (biomeManager != null)
            {
                biomeManager.OnBiomeChanged -= OnBiomeChanged;
            }
        }

        #region Initialization

        private void InitializeGenerators()
        {
            // Make sure BiomeManager exists
            if (biomeManager == null)
            {
                Debug.LogError("BiomeManager is null! Trying to find or create one...");
                biomeManager = FindObjectOfType<BiomeManager>();

                if (biomeManager == null)
                {
                    // Create BiomeManager if it doesn't exist
                    GameObject biomeManagerObj = new GameObject("BiomeManager");
                    biomeManager = biomeManagerObj.AddComponent<BiomeManager>();
                    Debug.Log("Created BiomeManager because none was found");
                }
            }

            // Try to get current biome
            BiomeConfig currentBiome = biomeManager.GetCurrentBiome();

            // If no biome is active, try to find available biomes
            if (currentBiome == null)
            {
                Debug.LogWarning("No active biome found. Attempting to find and set a biome...");

                // Try getting available biomes
                List<BiomeConfig> availableBiomes = biomeManager.GetAvailableBiomes();

                if (availableBiomes != null && availableBiomes.Count > 0)
                {
                    // Use the first available biome
                    currentBiome = availableBiomes[0];
                    biomeManager.SetBiome(currentBiome);
                    Debug.Log($"Set active biome to: {currentBiome.biomeName}");
                }
                else
                {
                    // No biomes found - we need to load biomes or create a default
                    Debug.LogError("No biomes found! Make sure you have biome configurations in Resources/Biomes folder");

                    // Create a default biome as a last resort
                    currentBiome = CreateDefaultBiome();
                    if (currentBiome != null)
                    {
                        Debug.Log("Created a default biome as a fallback");
                        // The biome manager should now have this biome available
                    }
                    else
                    {
                        Debug.LogError("Failed to create default biome. Terrain generation will fail!");
                        return;
                    }
                }
            }

            if (randomizeSeeds)
            {
                seed = UnityEngine.Random.Range(0, 100000);
            }

            // Create the noise generator with current biome
            try
            {
                noiseGenerator = new CombinedNoiseGenerator(currentBiome, seed);

                // Create terrain generator with initial resolution
                terrainGenerator = new TerrainGenerator(lodResolutions[0], lodResolutions[0], noiseGenerator);

                // Subscribe to biome changes
                if (biomeManager != null)
                {
                    biomeManager.OnBiomeChanged += OnBiomeChanged;
                }

                Debug.Log($"Successfully initialized generators with biome: {currentBiome.biomeName}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error creating terrain generators: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // Add this method to create a default biome as a last resort
        private BiomeConfig CreateDefaultBiome()
        {
            try
            {
                // Create a ScriptableObject for the default biome
                BiomeConfig defaultBiome = ScriptableObject.CreateInstance<BiomeConfig>();

                // Set default values
                defaultBiome.biomeName = "Default Biome";
                defaultBiome.biomeType = BiomeConfig.BiomeType.Forest;
                defaultBiome.biomeDescription = "Default biome created as a fallback.";

                // Set noise parameters
                defaultBiome.macroNoiseScale = 50f;
                defaultBiome.detailNoiseScale = 25f;
                defaultBiome.ridgedNoiseScale = 35f;
                defaultBiome.macroNoiseWeight = 0.6f;
                defaultBiome.detailNoiseWeight = 0.3f;
                defaultBiome.ridgedNoiseWeight = 0.1f;
                defaultBiome.heightExponent = 1.5f;
                defaultBiome.maxHeight = 50f;
                defaultBiome.flatnessModifier = 0.2f;
                defaultBiome.ruggednessFactor = 0.5f;
                defaultBiome.warpStrength = 0.3f;
                defaultBiome.erosionIterations = 20;
                defaultBiome.erosionTalus = 0.01f;
                defaultBiome.smoothingFactor = 0.2f;
                defaultBiome.smoothingIterations = 2;

                // Create a basic terrain layer for the default biome
                TerrainLayer defaultLayer = new TerrainLayer();
                defaultLayer.diffuseTexture = Texture2D.grayTexture; // Use a built-in texture
                defaultLayer.tileSize = new Vector2(50, 50);
                defaultBiome.terrainLayers = new TerrainLayer[] { defaultLayer };

                // Assign to biome manager
                if (biomeManager != null)
                {
                    // Add to biome manager's list if possible
                    var method = biomeManager.GetType().GetMethod("AddBiome",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic);

                    if (method != null)
                    {
                        method.Invoke(biomeManager, new object[] { defaultBiome });
                        biomeManager.SetBiome(defaultBiome);
                    }
                    else
                    {
                        // Direct assignment through SetBiome
                        biomeManager.SetBiome(defaultBiome);
                    }
                }

                return defaultBiome;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error creating default biome: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        private void InitializeChunkPool()
        {
            // Preinstantiate inactive chunks for reuse
            for (int i = 0; i < chunkPoolSize; i++)
            {
                GameObject chunkObj = new GameObject("PooledChunk");
                chunkObj.transform.parent = chunkParent.transform;
                TerrainChunk chunk = chunkObj.AddComponent<TerrainChunk>();
                chunk.gameObject.SetActive(false);
                chunkPool.Add(chunk);
            }
        }

        private void GenerateInitialChunks()
        {
            // Generate chunks in immediate vicinity for initial load
            QueueChunksInViewDistance(1); // Start with a smaller radius for faster initial load
            initialChunksGenerated = true;

            // Schedule an update of connections after chunks are generated
            StartCoroutine(UpdateConnectionsDelayed());
        }

        #endregion

        #region Chunk Management

        private void QueueChunksInViewDistance(int customViewDistance = -1)
        {
            int viewDst = customViewDistance > 0 ? customViewDistance : viewDistance;

            // Queue chunks from center outward (prioritizing close chunks)
            for (int dist = 0; dist <= viewDst; dist++)
            {
                for (int xOffset = -dist; xOffset <= dist; xOffset++)
                {
                    // Top and bottom edges of the square at this distance
                    for (int zOffset = -dist; zOffset <= dist; zOffset++)
                    {
                        // Only process chunks at the current distance (square perimeter)
                        if (Mathf.Abs(xOffset) == dist || Mathf.Abs(zOffset) == dist)
                        {
                            Vector2Int chunkCoord = new Vector2Int(
                                currentViewerChunkCoord.x + xOffset,
                                currentViewerChunkCoord.y + zOffset
                            );

                            // Only queue chunks that don't already exist
                            if (!terrainChunks.ContainsKey(chunkCoord) && !ContainsChunkInQueue(chunkCoord))
                            {
                                chunkGenerationQueue.Enqueue(chunkCoord);
                            }
                        }
                    }
                }
            }

            // After initial load, queue all remaining chunks in view distance
            if (initialChunksGenerated && customViewDistance > 0)
            {
                QueueChunksInViewDistance();
            }
        }

        private bool ContainsChunkInQueue(Vector2Int coord)
        {
            // Check if a coordinate is already in the generation queue
            // This is a simple O(n) implementation - could be improved with a HashSet if needed
            foreach (Vector2Int queuedCoord in chunkGenerationQueue)
            {
                if (queuedCoord == coord) return true;
            }
            return false;
        }

        private IEnumerator ProcessChunkQueue()
        {
            WaitForEndOfFrame wait = new WaitForEndOfFrame();
            float connectionUpdateTimer = 0f;
            float connectionUpdateInterval = 2.0f; // Check every 2 seconds

            while (true)
            {
                // Process a limited number of chunks per frame
                int chunksThisFrame = 0;
                while (chunkGenerationQueue.Count > 0 && chunksThisFrame < maxChunksPerFrame)
                {
                    Vector2Int coord = chunkGenerationQueue.Dequeue();

                    // Skip if chunk already exists (could have been created while in queue)
                    if (terrainChunks.ContainsKey(coord)) continue;

                    // Create the chunk
                    CreateChunkAtCoord(coord);
                    chunksThisFrame++;
                }

                // Process any chunks waiting for updates 
                chunksThisFrame = 0;
                while (chunksToUpdate.Count > 0 && chunksThisFrame < maxChunksPerFrame)
                {
                    TerrainChunk chunk = chunksToUpdate.Dequeue();
                    if (chunk != null && chunk.gameObject.activeInHierarchy)
                    {
                        chunk.UpdateLOD(GetChunkLODIndex(chunk));
                        chunksThisFrame++;
                    }
                }

                connectionUpdateTimer += Time.deltaTime;
                if (initialGenerationComplete && connectionUpdateTimer > connectionUpdateInterval)
                {
                    connectionUpdateTimer = 0f;

                    // Only update if there are pending chunks
                    if (pendingConnectionChunks.Count > 0)
                    {
                        StartCoroutine(UpdateNewChunkConnections());
                    }
                }

                yield return wait;
            }
        }

        private IEnumerator UpdateVisibleChunks()
        {
            WaitForSeconds wait = new WaitForSeconds(updateVisibilityTime);

            while (true)
            {
                if (viewer != null)
                {
                    // Update visibility if viewer moved significantly
                    if (Vector3.Distance(viewer.position, viewerPositionOld) > chunkSize / 10f)
                    {
                        viewerPositionOld = viewer.position;
                        UpdateChunkVisibility();
                    }
                }
                yield return wait;
            }
        }

        private void UpdateChunkVisibility()
        {
            visibleTerrainChunks.Clear();
            Vector2 viewerPosition2D = new Vector2(viewer.position.x, viewer.position.z);

            // Update each chunk's visibility based on distance
            List<Vector2Int> chunksToRemove = new List<Vector2Int>();

            foreach (var kvp in terrainChunks)
            {
                TerrainChunk chunk = kvp.Value;
                Vector2Int coord = kvp.Key;

                // Skip null chunks (safety check)
                if (chunk == null)
                {
                    chunksToRemove.Add(coord);
                    continue;
                }

                // Get chunk center in world space
                Vector2 chunkCenter = new Vector2(
                    (coord.x * chunkSize) + (chunkSize / 2f),
                    (coord.y * chunkSize) + (chunkSize / 2f)
                );

                // Calculate distance to chunk (using squared distance for efficiency)
                float sqrDst = (viewerPosition2D - chunkCenter).sqrMagnitude;
                float maxViewDstSqr = MAX_VIEW_DST * MAX_VIEW_DST;

                if (sqrDst <= maxViewDstSqr)
                {
                    // Visible chunk
                    if (!chunk.gameObject.activeSelf)
                    {
                        chunk.gameObject.SetActive(true);
                    }

                    // Update LOD if needed
                    int newLodIndex = GetLODIndex(sqrDst);
                    if (chunk.CurrentLOD != newLodIndex)
                    {
                        // Queue for LOD update rather than updating immediately
                        chunksToUpdate.Enqueue(chunk);
                    }

                    visibleTerrainChunks.Add(chunk);
                }
                else
                {
                    // Not visible - ensure coroutines are stopped before deactivating
                    chunk.PrepareForDeactivation();

                    // Now deactivate
                    if (chunk.gameObject.activeSelf)
                    {
                        chunk.gameObject.SetActive(false);
                    }

                    // Optionally recycle very distant chunks
                    if (sqrDst > maxViewDstSqr * 4)
                    {
                        RecycleChunk(chunk, coord);
                        chunksToRemove.Add(coord);
                    }
                }
            }

            // Remove recycled chunks from dictionary
            foreach (var coord in chunksToRemove)
            {
                terrainChunks.Remove(coord);
            }

            // After updating visibility, set neighbors to avoid seams
            UpdateChunkNeighbors();
        }

        private void UpdateChunkNeighbors()
        {
            foreach (TerrainChunk chunk in visibleTerrainChunks)
            {
                if (chunk == null || !chunk.IsGenerationComplete || !chunk.gameObject.activeInHierarchy)
                    continue;

                Vector2Int coord = chunk.chunkCoord;

                // Try to get neighboring chunks
                terrainChunks.TryGetValue(new Vector2Int(coord.x - 1, coord.y), out TerrainChunk leftChunk);
                terrainChunks.TryGetValue(new Vector2Int(coord.x, coord.y + 1), out TerrainChunk topChunk);
                terrainChunks.TryGetValue(new Vector2Int(coord.x + 1, coord.y), out TerrainChunk rightChunk);
                terrainChunks.TryGetValue(new Vector2Int(coord.x, coord.y - 1), out TerrainChunk bottomChunk);

                // Only use neighbors that are fully generated
                if (leftChunk != null && !leftChunk.IsGenerationComplete) leftChunk = null;
                if (topChunk != null && !topChunk.IsGenerationComplete) topChunk = null;
                if (rightChunk != null && !rightChunk.IsGenerationComplete) rightChunk = null;
                if (bottomChunk != null && !bottomChunk.IsGenerationComplete) bottomChunk = null;

                chunk.SetNeighbors(leftChunk, topChunk, rightChunk, bottomChunk);
            }

            // Force a terrain update to apply changes
            foreach (TerrainChunk chunk in visibleTerrainChunks)
            {
                if (chunk != null && chunk.terrain != null)
                {
                    chunk.terrain.Flush();
                }
            }
        }

        private IEnumerator UpdateConnectionsDelayed()
        {
            Debug.Log("Starting connection update process");

            // First wait until the chunk queue is empty
            while (chunkGenerationQueue.Count > 0)
            {
                yield return null;
            }

            Debug.Log("Generation queue processed, waiting for chunk generation to complete");

            // Now wait until all visible chunks have completed generation
            bool allChunksGenerated = false;
            int maxWaitFrames = 100; // Safety timeout
            int waitedFrames = 0;

            while (!allChunksGenerated && waitedFrames < maxWaitFrames)
            {
                allChunksGenerated = true;

                // Check if any chunks are still generating
                foreach (TerrainChunk chunk in visibleTerrainChunks)
                {
                    if (chunk != null && !chunk.IsGenerationComplete)
                    {
                        allChunksGenerated = false;
                        break;
                    }
                }

                yield return null;
                waitedFrames++;
            }

            // Give it a couple more frames for safety
            yield return new WaitForSeconds(0.1f);

            // Update chunk neighbors
            UpdateChunkNeighbors();

            initialGenerationComplete = true;
            Debug.Log($"Chunk connections updated after waiting {waitedFrames} frames");
        }

        private void RecycleChunk(TerrainChunk chunk, Vector2Int coord)
        {
            // Stop any coroutines on the chunk first
            chunk.PrepareForDeactivation();

            // Reset and add to pool
            chunk.ResetForPooling();
            chunkPool.Add(chunk);
        }

        private TerrainChunk CreateChunkAtCoord(Vector2Int coord)
        {
            // Get LOD based on distance from viewer
            Vector2 chunkCenterPos = new Vector2(
                (coord.x * chunkSize) + (chunkSize / 2f),
                (coord.y * chunkSize) + (chunkSize / 2f)
            );
            Vector2 viewerPos2D = new Vector2(viewer.position.x, viewer.position.z);
            float sqrDst = (viewerPos2D - chunkCenterPos).sqrMagnitude;
            int lodIndex = GetLODIndex(sqrDst);

            // Use chunk from pool if available
            TerrainChunk chunk = null;
            if (chunkPool.Count > 0)
            {
                chunk = chunkPool[chunkPool.Count - 1];
                chunkPool.RemoveAt(chunkPool.Count - 1);
                chunk.gameObject.name = $"Chunk_{coord.x}_{coord.y}";
            }
            else
            {
                // Create new if pool is empty
                GameObject chunkObj = new GameObject($"Chunk_{coord.x}_{coord.y}");
                chunkObj.transform.parent = chunkParent.transform;
                chunk = chunkObj.AddComponent<TerrainChunk>();
            }

            // Get current biome
            BiomeConfig biome = biomeManager.GetCurrentBiome();
            if (biome == null)
            {
                Debug.LogError("No biome available for chunk!");
                return null;
            }

            // Assign the shared terrain material if available
            if (sharedTerrainMaterial != null)
            {
                chunk.TerrainMaterial = sharedTerrainMaterial;
            }

            // Initialize the chunk
            chunk.gameObject.SetActive(true); // Make sure it's active before initialization
            chunk.Initialize(coord, chunkSize, terrainGenerator, biome, lodIndex, lodResolutions[lodIndex]);
            terrainChunks.Add(coord, chunk);

            if (initialGenerationComplete)
            {
                // If this is a chunk created after initial setup, 
                // schedule a connection update
                pendingConnectionChunks.Add(chunk);
                StartCoroutine(UpdateNewChunkConnections());
            }

            return chunk;
        }

        // Add a new method to handle connections for new chunks
        private IEnumerator UpdateNewChunkConnections()
        {
            // Wait a few frames to let chunks initialize
            yield return new WaitForSeconds(0.2f);

            // Make sure all pending chunks are done generating
            bool allDone = false;
            int waitFrames = 0;
            int maxWaitFrames = 30;

            while (!allDone && waitFrames < maxWaitFrames)
            {
                allDone = true;
                foreach (TerrainChunk chunk in pendingConnectionChunks)
                {
                    if (chunk != null && !chunk.IsGenerationComplete)
                    {
                        allDone = false;
                        break;
                    }
                }

                yield return null;
                waitFrames++;
            }

            // Update chunk connections
            UpdateChunkNeighbors();

            // Clear pending list
            pendingConnectionChunks.Clear();

            Debug.Log($"New chunk connections updated");
        }

        private int GetLODIndex(float sqrDst)
        {
            // Determine LOD level based on distance
            float linearDst = Mathf.Sqrt(sqrDst) / chunkSize;

            for (int i = 0; i < lodDistances.Length; i++)
            {
                if (linearDst <= lodDistances[i])
                {
                    return i;
                }
            }

            return lodDistances.Length - 1; // Max LOD if beyond all ranges
        }

        private int GetChunkLODIndex(TerrainChunk chunk)
        {
            Vector2 chunkCenterPos = new Vector2(
                (chunk.chunkCoord.x * chunkSize) + (chunkSize / 2f),
                (chunk.chunkCoord.y * chunkSize) + (chunkSize / 2f)
            );
            Vector2 viewerPos2D = new Vector2(viewer.position.x, viewer.position.z);
            float sqrDst = (viewerPos2D - chunkCenterPos).sqrMagnitude;

            return GetLODIndex(sqrDst);
        }

        private Vector2Int WorldToChunkCoord(Vector3 worldPosition)
        {
            int x = Mathf.FloorToInt(worldPosition.x / chunkSize);
            int z = Mathf.FloorToInt(worldPosition.z / chunkSize);
            return new Vector2Int(x, z);
        }

        #endregion

        #region Event Handlers

        private void OnBiomeChanged(BiomeConfig newBiome)
        {
            // Update noise generator with new biome
            noiseGenerator.UpdateBiomeConfig(newBiome);

            // Regenerate visible chunks with new biome
            foreach (TerrainChunk chunk in visibleTerrainChunks)
            {
                if (chunk != null && chunk.gameObject.activeInHierarchy)
                {
                    chunk.GenerateTerrain();
                }
            }

            // Ensure chunks fit together
            UpdateChunkNeighbors();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Regenerates all visible chunks
        /// </summary>
        public void RegenerateVisibleTerrain()
        {
            foreach (TerrainChunk chunk in visibleTerrainChunks)
            {
                if (chunk != null && chunk.gameObject.activeInHierarchy)
                {
                    chunk.GenerateTerrain();
                }
            }
            UpdateChunkNeighbors();
        }

        /// <summary>
        /// Changes the view distance for terrain loading
        /// </summary>
        public void SetViewDistance(int newViewDistance)
        {
            viewDistance = Mathf.Max(1, newViewDistance);
            QueueChunksInViewDistance();
        }

        /// <summary>
        /// Updates the seed and regenerates terrain
        /// </summary>
        public void UpdateSeed(int newSeed)
        {
            seed = newSeed;
            if (noiseGenerator != null)
            {
                // Recreate generators with new seed
                BiomeConfig currentBiome = biomeManager.GetCurrentBiome();
                noiseGenerator = new CombinedNoiseGenerator(currentBiome, seed);
                terrainGenerator = new TerrainGenerator(lodResolutions[0], lodResolutions[0], noiseGenerator);
            }
            RegenerateVisibleTerrain();
        }

        #endregion
    }
}