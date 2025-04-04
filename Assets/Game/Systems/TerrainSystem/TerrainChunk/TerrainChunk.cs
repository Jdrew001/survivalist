using Assets.Game.Systems.TerrainSystem.Biomes;
using Assets.Game.Systems.TerrainSystem.Generators;
using System.Collections;
using UnityEngine;

namespace Assets.Game.Systems.TerrainSystem.TerrainChunk
{
    public class TerrainChunk : MonoBehaviour
    {
        public Vector2Int chunkCoord { get; private set; }
        public Terrain terrain { get; private set; }
        public TerrainData terrainData { get; private set; }
        public int CurrentLOD { get; private set; } = 0;

        private TerrainGenerator terrainGenerator;
        private BiomeConfig biomeConfig;
        private int chunkSize;
        private int resolution;
        private bool isGenerating = false;
        private Coroutine generationCoroutine;

        [SerializeField] private Material terrainMaterial;

        // Public setter for terrain material
        public Material TerrainMaterial
        {
            get { return terrainMaterial; }
            set { terrainMaterial = value; }
        }

        /// <summary>
        /// Initializes or resets this chunk with the specified parameters
        /// </summary>
        public void Initialize(Vector2Int coord, int size, TerrainGenerator generator, BiomeConfig biome, int lodLevel = 0, int terrainResolution = 255)
        {
            // Check if any required parameters are null
            if (generator == null)
            {
                Debug.LogError($"Cannot initialize chunk: TerrainGenerator is null");
                return;
            }

            if (biome == null)
            {
                Debug.LogError($"Cannot initialize chunk: BiomeConfig is null");
                return;
            }

            chunkCoord = coord;
            chunkSize = size;
            terrainGenerator = generator;
            biomeConfig = biome;
            CurrentLOD = lodLevel;
            resolution = terrainResolution;

            // Set the world position based on chunk coordinate
            transform.position = new Vector3(coord.x * size, 0, coord.y * size);

            // Create or initialize the terrain component
            SetupTerrainComponent();

            // Generate terrain data
            if (generationCoroutine != null)
            {
                StopCoroutine(generationCoroutine);
                generationCoroutine = null;
            }

            if (gameObject.activeInHierarchy)
            {
                try
                {
                    generationCoroutine = StartCoroutine(GenerateTerrainAsync());
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Error starting GenerateTerrainAsync coroutine: {ex.Message}");
                    isGenerating = false;
                    generationCoroutine = null;
                }
            }
        }

        /// <summary>
        /// Sets up or resets the terrain component
        /// </summary>
        private void SetupTerrainComponent()
        {
            try
            {
                // Get or add a Terrain component
                terrain = GetComponent<Terrain>();
                if (terrain == null)
                {
                    terrain = gameObject.AddComponent<Terrain>();
                    if (terrain == null)
                    {
                        Debug.LogError($"Failed to add Terrain component to chunk {chunkCoord}");
                        return;
                    }
                }

                // Create TerrainData if needed
                if (terrainData == null)
                {
                    terrainData = new TerrainData();
                    if (terrainData == null)
                    {
                        Debug.LogError($"Failed to create TerrainData for chunk {chunkCoord}");
                        return;
                    }
                    terrainData.name = $"TerrainData_{chunkCoord.x}_{chunkCoord.y}";
                }

                // Verify that biomeConfig is not null before using it
                if (biomeConfig == null)
                {
                    Debug.LogError($"BiomeConfig is null during SetupTerrainComponent for chunk {chunkCoord}");
                    return;
                }

                // Configure basic terrain properties (actual heightmap will be set during generation)
                terrainData.heightmapResolution = resolution + 1; // +1 because Unity needs n+1 vertices for n cells
                terrainData.size = new Vector3(chunkSize, biomeConfig.maxHeight, chunkSize);
                terrain.terrainData = terrainData;

                // Setup material
                if (terrainMaterial != null)
                {
                    terrain.materialTemplate = terrainMaterial;
                }

                // Set terrain properties for proper neighbor connection
                terrain.allowAutoConnect = true;
                terrain.drawInstanced = true; // Better performance with instanced rendering
                terrain.groupingID = CurrentLOD; // Use LOD level as grouping ID to match LOD levels

                // Add or update the TerrainCollider
                TerrainCollider terrainCollider = GetComponent<TerrainCollider>();
                if (terrainCollider == null)
                {
                    terrainCollider = gameObject.AddComponent<TerrainCollider>();
                    if (terrainCollider == null)
                    {
                        Debug.LogError($"Failed to add TerrainCollider component to chunk {chunkCoord}");
                        return;
                    }
                }

                if (terrainCollider != null && terrainData != null)
                {
                    terrainCollider.terrainData = terrainData;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in SetupTerrainComponent for chunk {chunkCoord}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Asynchronously generates terrain data
        /// </summary>
        private IEnumerator GenerateTerrainAsync()
        {
            if (isGenerating) yield break; // Prevent multiple generations at once
            isGenerating = true;

            yield return null; // Wait a frame to avoid hiccups

            try
            {
                // Check if GameObject is still active
                if (!gameObject.activeInHierarchy)
                {
                    isGenerating = false;
                    generationCoroutine = null;
                    yield break;
                }

                // Check for null references before proceeding
                if (terrainGenerator == null)
                {
                    Debug.LogError($"Error in chunk {chunkCoord}: terrainGenerator is null");
                    isGenerating = false;
                    generationCoroutine = null;
                    yield break;
                }

                if (biomeConfig == null)
                {
                    Debug.LogError($"Error in chunk {chunkCoord}: biomeConfig is null");
                    isGenerating = false;
                    generationCoroutine = null;
                    yield break;
                }

                // Update generator resolution if needed
                terrainGenerator.UpdateParameters(resolution, resolution);

                // Generate terrain data with extra safety
                TerrainData newTerrainData = null;
                try
                {
                    newTerrainData = terrainGenerator.GenerateTerrain(biomeConfig, chunkCoord);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Exception during terrainGenerator.GenerateTerrain for chunk {chunkCoord}: {e.Message}\n{e.StackTrace}");
                    isGenerating = false;
                    generationCoroutine = null;
                    yield break;
                }

                // Check again if GameObject is still active
                if (!gameObject.activeInHierarchy)
                {
                    isGenerating = false;
                    generationCoroutine = null;
                    yield break;
                }

                if (newTerrainData != null)
                {
                    // Make sure terrainData exists
                    if (terrainData == null)
                    {
                        terrainData = new TerrainData();
                        terrainData.name = $"TerrainData_{chunkCoord.x}_{chunkCoord.y}";
                    }

                    // Apply new settings to existing terrainData to maintain references
                    terrainData.heightmapResolution = newTerrainData.heightmapResolution;

                    float[,] heights = newTerrainData.GetHeights(0, 0, newTerrainData.heightmapResolution, newTerrainData.heightmapResolution);
                    if (heights != null)
                    {
                        terrainData.SetHeights(0, 0, heights);
                    }
                    else
                    {
                        Debug.LogError($"Generated heights array is null for chunk {chunkCoord}");
                    }

                    if (newTerrainData.terrainLayers != null && newTerrainData.terrainLayers.Length > 0)
                    {
                        terrainData.terrainLayers = newTerrainData.terrainLayers;
                    }

                    // Make sure terrain exists
                    if (terrain != null)
                    {
                        // Apply terrain material if available
                        if (terrainMaterial != null)
                        {
                            terrain.materialTemplate = terrainMaterial;
                        }

                        // Make sure terrain has the correct terrainData
                        terrain.terrainData = terrainData;

                        // Ensure the TerrainCollider is up to date
                        TerrainCollider collider = GetComponent<TerrainCollider>();
                        if (collider != null)
                        {
                            collider.terrainData = terrainData;
                        }

                        // Ensure proper connection parameters
                        terrain.allowAutoConnect = true;
                    }
                    else
                    {
                        Debug.LogError($"Error in chunk {chunkCoord}: terrain component is null");
                    }
                }
                else
                {
                    Debug.LogError($"TerrainGenerator returned null terrainData for chunk {chunkCoord}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error generating terrain for chunk {chunkCoord}: {ex.Message}\n{ex.StackTrace}");
            }

            isGenerating = false;
            generationCoroutine = null;
        }

        /// <summary>
        /// Updates the LOD (Level of Detail) for this chunk
        /// </summary>
        public void UpdateLOD(int newLODLevel)
        {
            if (CurrentLOD == newLODLevel) return;

            CurrentLOD = newLODLevel;

            if (terrain != null)
            {
                terrain.groupingID = newLODLevel;
            }

            // Queue terrain regeneration with new resolution
            if (generationCoroutine != null)
            {
                StopCoroutine(generationCoroutine);
                generationCoroutine = null;
            }

            if (gameObject.activeInHierarchy && terrainGenerator != null && biomeConfig != null)
            {
                try
                {
                    generationCoroutine = StartCoroutine(GenerateTerrainAsync());
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Error starting GenerateTerrainAsync in UpdateLOD: {ex.Message}");
                    isGenerating = false;
                    generationCoroutine = null;
                }
            }
        }

        /// <summary>
        /// Regenerates the terrain synchronously (used when biome changes)
        /// </summary>
        public void GenerateTerrain()
        {
            if (isGenerating) return;

            // Stop any running generation
            if (generationCoroutine != null)
            {
                StopCoroutine(generationCoroutine);
                generationCoroutine = null;
            }

            // Only start if active and dependencies exist
            if (gameObject.activeInHierarchy && terrainGenerator != null && biomeConfig != null)
            {
                try
                {
                    generationCoroutine = StartCoroutine(GenerateTerrainAsync());
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Error starting GenerateTerrainAsync in GenerateTerrain: {ex.Message}");
                    isGenerating = false;
                    generationCoroutine = null;
                }
            }
        }

        /// <summary>
        /// Sets neighboring terrains to enable seamless connections
        /// </summary>
        public void SetNeighbors(TerrainChunk left, TerrainChunk top, TerrainChunk right, TerrainChunk bottom)
        {
            if (terrain == null) return;

            // Get the terrain component from each neighbor, being careful to check for null
            Terrain leftTerrain = left?.terrain;
            Terrain topTerrain = top?.terrain;
            Terrain rightTerrain = right?.terrain;
            Terrain bottomTerrain = bottom?.terrain;

            try
            {
                terrain.SetNeighbors(leftTerrain, topTerrain, rightTerrain, bottomTerrain);

                // Log successful neighbor connection for debugging
                Debug.Log($"Set neighbors for chunk {chunkCoord}: Left: {leftTerrain != null}, Top: {topTerrain != null}, Right: {rightTerrain != null}, Bottom: {bottomTerrain != null}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error setting neighbors for chunk {chunkCoord}: {ex.Message}");
            }
        }

        /// <summary>
        /// Debug method to check neighbor connections
        /// </summary>
        //public void DebugNeighborStatus()
        //{
        //    if (terrain == null) return;

        //    Terrain left = null, top = null, right = null, bottom = null;
        //    terrain.GetNeighbors(ref left, ref top, ref right, ref bottom);

        //    Debug.Log($"Chunk {chunkCoord} neighbors: Left: {left != null}, Top: {top != null}, Right: {right != null}, Bottom: {bottom != null}");
        //    Debug.Log($"Chunk {chunkCoord} allowAutoConnect: {terrain.allowAutoConnect}");

        //    // Also check heightmap resolution and size
        //    if (terrainData != null)
        //    {
        //        Debug.Log($"Chunk {chunkCoord} heightmap resolution: {terrainData.heightmapResolution}, size: {terrainData.size}");
        //    }
        //}

        /// <summary>
        /// Prepares this chunk for deactivation by stopping coroutines
        /// </summary>
        public void PrepareForDeactivation()
        {
            try
            {
                // Stop any running coroutines
                if (generationCoroutine != null)
                {
                    StopCoroutine(generationCoroutine);
                    generationCoroutine = null;
                }
                isGenerating = false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in PrepareForDeactivation for chunk {chunkCoord}: {ex.Message}");
            }
        }

        /// <summary>
        /// Prepares this chunk for recycling into the object pool
        /// </summary>
        public void ResetForPooling()
        {
            try
            {
                // Stop any active coroutines first
                PrepareForDeactivation();

                // Reset state but keep components for reuse
                chunkCoord = new Vector2Int(0, 0);
                CurrentLOD = 0;

                // Don't destroy terrainData to avoid garbage collection, just reset it
                if (terrainData != null)
                {
                    // Create a minimal heightmap to release memory
                    terrainData.heightmapResolution = 33; // Minimum resolution
                    terrainData.SetHeights(0, 0, new float[33, 33]);
                }

                gameObject.SetActive(false);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in ResetForPooling for chunk {chunkCoord}: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when the chunk is disabled
        /// </summary>
        private void OnDisable()
        {
            try
            {
                // Stop any running coroutines when the chunk is disabled
                if (generationCoroutine != null)
                {
                    StopCoroutine(generationCoroutine);
                    generationCoroutine = null;
                }
                isGenerating = false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in OnDisable for chunk {chunkCoord}: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when the chunk is destroyed
        /// </summary>
        private void OnDestroy()
        {
            try
            {
                // Clean up resources if needed
                PrepareForDeactivation();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in OnDestroy for chunk {chunkCoord}: {ex.Message}");
            }
        }
    }
}