using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Game.Systems.TerrainSystem.Biomes
{
    [CreateAssetMenu(fileName = "BiomeConfig", menuName = "Terrain/Biome Configuration")]
    public class BiomeConfig : ScriptableObject
    {
        public enum BiomeType
        {
            Forest,
            Desert,
            Tundra,
            Tropical
        }

        [Header("Biome Settings")]
        public BiomeType biomeType;
        public string biomeName;

        [TextArea(3, 5)]
        public string biomeDescription;

        [Header("Noise Scale Multipliers")]
        [Range(1f, 100f)] public float macroNoiseScale = 50f;
        [Range(1f, 100f)] public float detailNoiseScale = 25f;
        [Range(1f, 100f)] public float ridgedNoiseScale = 35f;

        [Header("Noise Weights")]
        [Range(0f, 1f)] public float macroNoiseWeight = 0.6f;
        [Range(0f, 1f)] public float detailNoiseWeight = 0.3f;
        [Range(0f, 1f)] public float ridgedNoiseWeight = 0.1f;

        [Header("Height Modifiers")]
        [Range(0.1f, 5f)] public float heightExponent = 1.5f;
        [Range(0f, 100f)] public float maxHeight = 50f;

        [Header("Terrain Features")]
        [Range(0f, 1f)] public float flatnessModifier = 0.2f;
        [Range(0f, 1f)] public float ruggednessFactor = 0.5f;

        [Header("Refinement Settings")]
        [Range(0f, 1f)] public float warpStrength = 0.3f;
        [Range(0, 100)] public int erosionIterations = 20;
        [Range(0f, 0.1f)] public float erosionTalus = 0.01f;
        [Range(0f, 1f)] public float smoothingFactor = 0.2f;
        [Range(1, 5)] public int smoothingIterations = 2;

        [Header("Terrain Textures")]
        public TerrainLayer[] terrainLayers; // Assign these in the Inspector

        private void OnValidate()
        {
            // Ensure noise weights sum to 1
            float sum = macroNoiseWeight + detailNoiseWeight + ridgedNoiseWeight;
            if (sum != 0)
            {
                macroNoiseWeight /= sum;
                detailNoiseWeight /= sum;
                detailNoiseWeight /= sum;
                ridgedNoiseWeight /= sum;
            }
        }
    }
}
