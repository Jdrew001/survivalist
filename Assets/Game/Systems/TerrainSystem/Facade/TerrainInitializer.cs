using Assets.Game.Systems.TerrainSystem.Biomes;
using Assets.Game.Systems.TerrainSystem.UI;
using UnityEngine;

namespace Assets.Game.Systems.TerrainSystem.Facade
{
    public class TerrainInitializer : MonoBehaviour
    {
        [Header("Auto Generation")]
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private bool useRandomBiome = true;

        [Header("Components")]
        [SerializeField] private TerrainFacade terrainFacade;
        [SerializeField] private BiomeManager biomeManager;

        private void Awake()
        {
            // Find or create required components
            EnsureComponentsExist();
        }

        private void Start()
        {
            // Generate terrain if configured to do so
            if (generateOnStart)
            {
                if (useRandomBiome && biomeManager != null)
                {
                    biomeManager.SelectRandomBiome();
                }
                else if (terrainFacade != null)
                {
                    terrainFacade.GenerateTerrain();
                }
            }
        }

        /// <summary>
        /// Ensures all necessary components exist in the scene
        /// </summary>
        private void EnsureComponentsExist()
        {
            // Find or create BiomeManager
            if (biomeManager == null)
            {
                biomeManager = FindObjectOfType<BiomeManager>();
                if (biomeManager == null)
                {
                    GameObject biomeManagerObject = new GameObject("BiomeManager");
                    biomeManager = biomeManagerObject.AddComponent<BiomeManager>();
                    Debug.Log("Created BiomeManager");
                }
            }

            // Find or create TerrainFacade
            if (terrainFacade == null)
            {
                terrainFacade = FindObjectOfType<TerrainFacade>();
                if (terrainFacade == null)
                {
                    GameObject terrainFacadeObject = new GameObject("TerrainFacade");
                    terrainFacade = terrainFacadeObject.AddComponent<TerrainFacade>();
                    Debug.Log("Created TerrainFacade");
                }
            }

            // Check if Terrain exists
            Terrain terrain = FindObjectOfType<Terrain>();
            if (terrain == null)
            {
                GameObject terrainObject = new GameObject("GeneratedTerrain");
                terrain = terrainObject.AddComponent<Terrain>();
                terrainObject.AddComponent<TerrainCollider>();
                Debug.Log("Created Terrain");
            }

            // Ensure UI exists
            TerrainUIManager uiManager = FindObjectOfType<TerrainUIManager>();
            if (uiManager == null)
            {
                // This is optional since UI might be set up manually
                // We don't create it automatically as it would require a canvas and other elements
                Debug.LogWarning("No UIManager found in scene. UI functionality will be limited.");
            }
        }

        /// <summary>
        /// Manual method to regenerate terrain with current settings
        /// </summary>
        public void RegenerateTerrain()
        {
            if (terrainFacade != null)
            {
                terrainFacade.GenerateTerrain();
            }
        }

        /// <summary>
        /// Manual method to generate terrain with a random biome
        /// </summary>
        public void GenerateRandomBiomeTerrain()
        {
            if (biomeManager != null)
            {
                biomeManager.SelectRandomBiome();
            }
        }
    }
}
