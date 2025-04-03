using Assets.Game.Systems.TerrainSystem.Biomes;
using Assets.Game.Systems.TerrainSystem.NoiseGenerators;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Game.Systems.TerrainSystem
{
    public class CombinedNoiseGenerator
    {
        private class NoiseLayer
        {
            public INoiseGenerator Generator { get; private set; }
            public float Weight { get; private set; }

            public NoiseLayer(INoiseGenerator generator, float weight)
            {
                Generator = generator;
                Weight = weight;
            }
        }

        private List<NoiseLayer> noiseLayers = new List<NoiseLayer>();
        private BiomeConfig biomeConfig;
        private int seed;

        public CombinedNoiseGenerator(BiomeConfig biomeConfig, int seed = 0)
        {
            this.biomeConfig = biomeConfig;
            this.seed = seed;
            InitializeNoiseLayers();
        }

        private void InitializeNoiseLayers()
        {
            // Macro noise (large features)
            var macroNoise = new PerlinNoiseGenerator(biomeConfig.macroNoiseScale, seed);

            // Mid-level detail
            var midNoise = new SimplexNoiseGenerator(biomeConfig.detailNoiseScale, seed + 1);

            // Micro detail
            var microNoise = new PerlinNoiseGenerator(biomeConfig.detailNoiseScale / 4, seed + 2);

            // Ridge detail for mountains
            var ridgeNoise = new RidgedNoiseGenerator(biomeConfig.ridgedNoiseScale, seed + 3);

            // Add with appropriate weights
            AddNoiseGenerator(macroNoise, biomeConfig.macroNoiseWeight * 0.8f);
            AddNoiseGenerator(midNoise, biomeConfig.detailNoiseWeight * 0.6f);
            AddNoiseGenerator(microNoise, biomeConfig.detailNoiseWeight * 0.4f);
            AddNoiseGenerator(ridgeNoise, biomeConfig.ridgedNoiseWeight);
        }

        public void AddNoiseGenerator(INoiseGenerator generator, float weight)
        {
            noiseLayers.Add(new NoiseLayer(generator, weight));
        }

        public float GenerateNoise(int x, int y)
        {
            float totalNoise = GenerateFBM(x, y, 4); // 4 octaves
            float totalWeight = 0f;

            // Combine all noise layers based on their weights
            foreach (var layer in noiseLayers)
            {
                totalNoise += layer.Generator.GenerateNoise(x, y) * layer.Weight;
                totalWeight += layer.Weight;
            }

            // Normalize by total weight if needed
            if (totalWeight > 0)
            {
                totalNoise /= totalWeight;
            }

            // Apply fractal brownian motion (multiple octaves)
          

            // Apply domain warping for more natural distortion
            totalNoise = ApplyDomainWarping(x, y, totalNoise);

            // Apply biome-specific adjustments

            // Apply height exponent for varying terrain steepness
            totalNoise = Mathf.Pow(totalNoise, biomeConfig.heightExponent);

            // Apply flatness (lerp toward flat terrain in areas below threshold)
            if (totalNoise < biomeConfig.flatnessModifier)
            {
                // Smoothly transition to flat areas
                float flatnessInfluence = 1f - (totalNoise / biomeConfig.flatnessModifier);
                totalNoise = Mathf.Lerp(totalNoise, 0f, flatnessInfluence * 0.7f);
            }

            // Apply ruggedness (add small detail noise to terrain above threshold)
            if (totalNoise > (1f - biomeConfig.ruggednessFactor) * 0.5f)
            {
                // Add micro-detail to higher areas
                float detailNoise = Mathf.PerlinNoise(x / 5f + seed, y / 5f + seed) * 0.05f;
                totalNoise += detailNoise * biomeConfig.ruggednessFactor;
            }

            // Ensure output stays in 0-1 range
            return Mathf.Clamp01(totalNoise);
        }

        public void UpdateBiomeConfig(BiomeConfig newConfig)
        {
            this.biomeConfig = newConfig;

            // Update existing noise generators with new scales
            if (noiseLayers.Count >= 3)
            {
                noiseLayers[0].Generator.SetScale(newConfig.macroNoiseScale);
                noiseLayers[1].Generator.SetScale(newConfig.detailNoiseScale);
                noiseLayers[2].Generator.SetScale(newConfig.ridgedNoiseScale);

                // Recreate the list to update weights
                // Update weights by reconstructing the noise layer list.
                var updatedLayers = new List<NoiseLayer>
                {
                    new NoiseLayer(noiseLayers[0].Generator, newConfig.macroNoiseWeight),
                    new NoiseLayer(noiseLayers[1].Generator, newConfig.detailNoiseWeight),
                    new NoiseLayer(noiseLayers[2].Generator, newConfig.ridgedNoiseWeight)
                };

                noiseLayers = updatedLayers;
            }
            else
            {
                // If layers were missing, reinitialize
                noiseLayers.Clear();
                InitializeNoiseLayers();
            }
        }

        private float GenerateFBM(float x, float y, int octaves)
        {
            float total = 0;
            float frequency = 1;
            float amplitude = 1;
            float maxValue = 0;

            for (int i = 0; i < octaves; i++)
            {
                // Use different noise generators for different octaves
                total += noiseLayers[i % noiseLayers.Count].Generator.GenerateNoise(
                    (int)(x * frequency), (int)(y * frequency)) * amplitude;

                maxValue += amplitude;
                amplitude *= 0.5f;
                frequency *= 2;
            }

            return total / maxValue;
        }

        private float ApplyDomainWarping(float x, float y, float baseNoise)
        {
            // Shift coordinates based on another noise function
            float warpX = x + Mathf.PerlinNoise(x * 0.01f, y * 0.01f) * 20;
            float warpY = y + Mathf.PerlinNoise(x * 0.01f, y * 0.01f + 100) * 20;

            // Sample noise at warped coordinates
            return Mathf.Lerp(baseNoise,
                GenerateFBM(warpX, warpY, 2),
                biomeConfig.warpStrength);
        }
    }
}
