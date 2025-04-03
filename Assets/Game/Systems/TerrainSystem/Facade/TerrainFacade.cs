using Assets.Game.Systems.TerrainSystem.Biomes;
using Assets.Game.Systems.TerrainSystem.Generators;
using System;
using UnityEngine;

namespace Assets.Game.Systems.TerrainSystem.Facade
{
    public class TerrainFacade : MonoBehaviour
    {
        [Header("Terrain Settings")]
        [SerializeField] private int terrainWidth = 256;
        [SerializeField] private int terrainHeight = 256;
        [SerializeField] private int seed = 0;
        [SerializeField] private bool randomizeSeedOnGenerate = true;

        [Header("References")]
        [SerializeField] private Terrain terrain;
        [SerializeField] private BiomeManager biomeManager;

        // Dependencies
        private TerrainGenerator terrainGenerator;
        private CombinedNoiseGenerator noiseGenerator;

        // Events (Observer Pattern)
        public event Action OnTerrainGenerationStarted;
        public event Action<Terrain> OnTerrainGenerationCompleted;

        private void Awake()
        {
            // Find or create required components
            if (terrain == null)
            {
                terrain = FindObjectOfType<Terrain>();
                if (terrain == null)
                {
                    GameObject terrainObject = new GameObject("GeneratedTerrain");
                    terrain = terrainObject.AddComponent<Terrain>();
                    terrainObject.AddComponent<TerrainCollider>();
                }
            }

            if (biomeManager == null)
            {
                biomeManager = FindObjectOfType<BiomeManager>();
                if (biomeManager == null)
                {
                    GameObject biomeManagerObject = new GameObject("BiomeManager");
                    biomeManager = biomeManagerObject.AddComponent<BiomeManager>();
                }
            }

            // Subscribe to biome change events
            biomeManager.OnBiomeChanged += OnBiomeChanged;
        }

        private void Start()
        {
            // Initialize with current biome
            InitializeGenerators(biomeManager.GetCurrentBiome());
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (biomeManager != null)
            {
                biomeManager.OnBiomeChanged -= OnBiomeChanged;
            }
        }

        /// <summary>
        /// Initializes or updates the noise and terrain generators
        /// </summary>
        private void InitializeGenerators(BiomeConfig biomeConfig)
        {
            if (biomeConfig == null)
            {
                Debug.LogError("Cannot initialize generators: Biome config is null");
                return;
            }

            if (noiseGenerator == null)
            {
                // Create the noise generator with current biome config
                noiseGenerator = new CombinedNoiseGenerator(biomeConfig, seed);
            }
            else
            {
                // Update existing noise generator with new biome config
                noiseGenerator.UpdateBiomeConfig(biomeConfig);
            }

            if (terrainGenerator == null)
            {
                // Create the terrain generator
                terrainGenerator = new TerrainGenerator(terrainWidth, terrainHeight, noiseGenerator);

                // Subscribe to terrain generation events
                terrainGenerator.OnTerrainGenerated += OnTerrainDataGenerated;
            }
            else
            {
                // Update terrain generator parameters if needed
                terrainGenerator.UpdateParameters(terrainWidth, terrainHeight);
            }
        }

        /// <summary>
        /// Generates terrain using current settings
        /// </summary>
        public void GenerateTerrain()
        {
            if (randomizeSeedOnGenerate)
            {
                seed = UnityEngine.Random.Range(0, 100000);
            }

            // Notify that generation has started
            OnTerrainGenerationStarted?.Invoke();

            // Get current biome config
            BiomeConfig currentBiome = biomeManager.GetCurrentBiome();
            if (currentBiome == null)
            {
                Debug.LogError("Cannot generate terrain: No active biome");
                return;
            }

            // Initialize or update generators
            InitializeGenerators(currentBiome);

            // Generate terrain
            TerrainData terrainData = terrainGenerator.GenerateTerrain(currentBiome);

            // Apply to terrain
            terrain.terrainData = terrainData;

            // Ensure terrain collider uses the same data
            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (terrainCollider != null)
            {
                terrainCollider.terrainData = terrainData;
            }
        }

        /// <summary>
        /// Called when biome changes
        /// </summary>
        private void OnBiomeChanged(BiomeConfig newBiome)
        {
            // Update generators with new biome
            InitializeGenerators(newBiome);

            // Regenerate terrain with new biome
            Debug.Log($"Biome changed to {newBiome.biomeName}, regenerating terrain");
            GenerateTerrain();
        }

        /// <summary>
        /// Called when terrain data is generated
        /// </summary>
        private void OnTerrainDataGenerated(TerrainData terrainData)
        {
            // Notify that generation is complete
            OnTerrainGenerationCompleted?.Invoke(terrain);
        }

        /// <summary>
        /// Updates terrain generation parameters
        /// </summary>
        public void UpdateTerrainParameters(int width, int height, int newSeed, bool randomize)
        {
            terrainWidth = width;
            terrainHeight = height;
            seed = newSeed;
            randomizeSeedOnGenerate = randomize;

            // Update terrain generator if exists
            if (terrainGenerator != null)
            {
                terrainGenerator.UpdateParameters(width, height);
            }
        }
    }
}
