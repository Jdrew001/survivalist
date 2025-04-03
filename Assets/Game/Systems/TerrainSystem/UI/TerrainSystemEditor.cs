#if UNITY_EDITOR


using Assets.Game.Systems.TerrainSystem.Biomes;
using Assets.Game.Systems.TerrainSystem.Facade;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assets.Game.Systems.TerrainSystem.UI
{
    [CustomEditor(typeof(TerrainInitializer))]

    public class TerrainSystemEditor : Editor
    {
        private bool showBiomeTools = false;
        private string newBiomeName = "New Biome";
        private BiomeConfig.BiomeType biomeType = BiomeConfig.BiomeType.Forest;

        public override void OnInspectorGUI()
        {
            // Draw the default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Terrain System Tools", EditorStyles.boldLabel);

            TerrainInitializer terrainInitializer = (TerrainInitializer)target;

            // Generate terrain buttons
            if (GUILayout.Button("Generate Terrain"))
            {
                terrainInitializer.RegenerateTerrain();
            }

            if (GUILayout.Button("Generate Random Biome Terrain"))
            {
                terrainInitializer.GenerateRandomBiomeTerrain();
            }

            EditorGUILayout.Space();

            // Biome creation tools
            showBiomeTools = EditorGUILayout.Foldout(showBiomeTools, "Biome Creation Tools", true);
            if (showBiomeTools)
            {
                EditorGUI.indentLevel++;

                // New biome creation
                EditorGUILayout.LabelField("Create New Biome", EditorStyles.boldLabel);
                newBiomeName = EditorGUILayout.TextField("Biome Name", newBiomeName);
                biomeType = (BiomeConfig.BiomeType)EditorGUILayout.EnumPopup("Biome Type", biomeType);

                if (GUILayout.Button("Create Biome"))
                {
                    CreateNewBiome();
                }

                EditorGUILayout.Space();

                // Directory creation
                EditorGUILayout.LabelField("Directory Setup", EditorStyles.boldLabel);
                if (GUILayout.Button("Create Biomes Directory"))
                {
                    CreateBiomesDirectory();
                }

                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// Creates a new BiomeConfig ScriptableObject
        /// </summary>
        private void CreateNewBiome()
        {
            // Ensure directory exists
            string path = "Assets/Resources/Biomes";
            if (!Directory.Exists(path))
            {
                CreateBiomesDirectory();
            }

            // Create new BiomeConfig asset
            BiomeConfig biomeConfig = CreateInstance<BiomeConfig>();
            biomeConfig.biomeName = newBiomeName;
            biomeConfig.biomeType = biomeType;

            // Set default values based on biome type
            SetDefaultBiomeValues(biomeConfig);

            // Create asset file
            string assetPath = Path.Combine(path, $"{newBiomeName}.asset");

            // Ensure filename is unique
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            AssetDatabase.CreateAsset(biomeConfig, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Reset form
            newBiomeName = "New Biome";

            // Select the new asset
            Selection.activeObject = biomeConfig;

            Debug.Log($"Created new biome: {assetPath}");
        }

        /// <summary>
        /// Sets default biome values based on biome type
        /// </summary>
        private void SetDefaultBiomeValues(BiomeConfig biomeConfig)
        {
            switch (biomeConfig.biomeType)
            {
                case BiomeConfig.BiomeType.Forest:
                    biomeConfig.biomeDescription = "Dense woodland with varying elevation and some clearings.";
                    biomeConfig.macroNoiseScale = 65f;
                    biomeConfig.detailNoiseScale = 30f;
                    biomeConfig.ridgedNoiseScale = 40f;
                    biomeConfig.macroNoiseWeight = 0.7f;
                    biomeConfig.detailNoiseWeight = 0.2f;
                    biomeConfig.ridgedNoiseWeight = 0.1f;
                    biomeConfig.heightExponent = 1.3f;
                    biomeConfig.maxHeight = 45f;
                    biomeConfig.flatnessModifier = 0.15f;
                    biomeConfig.ruggednessFactor = 0.3f;
                    // More subtle terrain variations, medium erosion
                    biomeConfig.warpStrength = 0.2f;
                    biomeConfig.erosionIterations = 15;
                    biomeConfig.smoothingFactor = 0.15f;
                    break;

                case BiomeConfig.BiomeType.Desert:
                    biomeConfig.biomeDescription = "Arid landscape with rolling dunes and occasional rocky outcrops.";
                    biomeConfig.macroNoiseScale = 80f;
                    biomeConfig.detailNoiseScale = 20f;
                    biomeConfig.ridgedNoiseScale = 30f;
                    biomeConfig.macroNoiseWeight = 0.75f;
                    biomeConfig.detailNoiseWeight = 0.15f;
                    biomeConfig.ridgedNoiseWeight = 0.1f;
                    biomeConfig.heightExponent = 1.1f;
                    biomeConfig.maxHeight = 35f;
                    biomeConfig.flatnessModifier = 0.4f;
                    biomeConfig.ruggednessFactor = 0.2f;
                    // More warping for dune effects, less erosion
                    biomeConfig.warpStrength = 0.4f;
                    biomeConfig.erosionIterations = 5;
                    biomeConfig.smoothingFactor = 0.1f;
                    break;

                case BiomeConfig.BiomeType.Tundra:
                    biomeConfig.biomeDescription = "Cold northern plains with subtle rolling hills and scattered rocky formations.";
                    biomeConfig.macroNoiseScale = 90f;
                    biomeConfig.detailNoiseScale = 25f;
                    biomeConfig.ridgedNoiseScale = 50f;
                    biomeConfig.macroNoiseWeight = 0.65f;
                    biomeConfig.detailNoiseWeight = 0.2f;
                    biomeConfig.ridgedNoiseWeight = 0.15f;
                    biomeConfig.heightExponent = 1.5f;
                    biomeConfig.maxHeight = 30f;
                    biomeConfig.flatnessModifier = 0.35f;
                    biomeConfig.ruggednessFactor = 0.45f;
                    // More erosion, medium smoothing
                    biomeConfig.warpStrength = 0.15f;
                    biomeConfig.erosionIterations = 25;
                    biomeConfig.smoothingFactor = 0.2f;
                    break;

                case BiomeConfig.BiomeType.Tropical:
                    biomeConfig.biomeDescription = "Lush rainforest terrain with varied elevation and distinctive valleys.";
                    biomeConfig.macroNoiseScale = 55f;
                    biomeConfig.detailNoiseScale = 35f;
                    biomeConfig.ridgedNoiseScale = 45f;
                    biomeConfig.macroNoiseWeight = 0.6f;
                    biomeConfig.detailNoiseWeight = 0.3f;
                    biomeConfig.ridgedNoiseWeight = 0.1f;
                    biomeConfig.heightExponent = 1.8f;
                    biomeConfig.maxHeight = 60f;
                    biomeConfig.flatnessModifier = 0.1f;
                    biomeConfig.ruggednessFactor = 0.7f;
                    // Heavy erosion, less smoothing for rugged rainforest terrain
                    biomeConfig.warpStrength = 0.3f;
                    biomeConfig.erosionIterations = 30;
                    biomeConfig.smoothingFactor = 0.1f;
                    break;
            }
        }

        /// <summary>
        /// Creates the Resources/Biomes directory
        /// </summary>
        private void CreateBiomesDirectory()
        {
            // Create Resources directory if it doesn't exist
            string resourcesPath = "Assets/Resources";
            if (!Directory.Exists(resourcesPath))
            {
                Directory.CreateDirectory(resourcesPath);
                AssetDatabase.Refresh();
            }

            // Create Biomes directory if it doesn't exist
            string biomesPath = Path.Combine(resourcesPath, "Biomes");
            if (!Directory.Exists(biomesPath))
            {
                Directory.CreateDirectory(biomesPath);
                AssetDatabase.Refresh();
            }

            Debug.Log("Created Resources/Biomes directory");
        }
    }
}
#endif