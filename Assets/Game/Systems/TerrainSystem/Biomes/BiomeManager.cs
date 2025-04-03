using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Game.Systems.TerrainSystem.Biomes
{
    public class BiomeManager : MonoBehaviour
    {
        [SerializeField] private List<BiomeConfig> availableBiomes = new List<BiomeConfig>();

        private BiomeConfig currentBiome;

        // Event for notifying when biome changes
        public event Action<BiomeConfig> OnBiomeChanged;

        private void Awake()
        {
            // Find biome ScriptableObjects if list is empty
            if (availableBiomes.Count == 0)
            {
                LoadBiomesFromResources();
            }

            // Set default biome if available
            if (availableBiomes.Count > 0)
            {
                SetBiome(availableBiomes[0]);
            }
        }

        /// <summary>
        /// Loads biome configurations from Resources folder
        /// </summary>
        private void LoadBiomesFromResources()
        {
            BiomeConfig[] biomes = Resources.LoadAll<BiomeConfig>("Biomes");
            if (biomes != null && biomes.Length > 0)
            {
                availableBiomes.AddRange(biomes);
                Debug.Log($"Loaded {biomes.Length} biomes from Resources");
            }
            else
            {
                Debug.LogWarning("No biomes found in Resources/Biomes folder");
            }
        }

        /// <summary>
        /// Sets the active biome by type
        /// </summary>
        public void SetBiome(BiomeConfig.BiomeType biomeType)
        {
            foreach (var biome in availableBiomes)
            {
                if (biome.biomeType == biomeType)
                {
                    SetBiome(biome);
                    return;
                }
            }

            Debug.LogWarning($"Biome of type {biomeType} not found");
        }

        /// <summary>
        /// Sets the active biome directly
        /// </summary>
        public void SetBiome(BiomeConfig biomeConfig)
        {
            if (biomeConfig != null)
            {
                currentBiome = biomeConfig;
                OnBiomeChanged?.Invoke(currentBiome);
                Debug.Log($"Biome set to: {currentBiome.biomeName}");
            }
        }

        /// <summary>
        /// Selects a random biome from available biomes
        /// </summary>
        public void SelectRandomBiome()
        {
            if (availableBiomes.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, availableBiomes.Count);
                SetBiome(availableBiomes[randomIndex]);
            }
        }

        /// <summary>
        /// Gets the current active biome configuration
        /// </summary>
        public BiomeConfig GetCurrentBiome()
        {
            return currentBiome;
        }

        /// <summary>
        /// Gets all available biomes
        /// </summary>
        public List<BiomeConfig> GetAvailableBiomes()
        {
            return availableBiomes;
        }
    }
}
