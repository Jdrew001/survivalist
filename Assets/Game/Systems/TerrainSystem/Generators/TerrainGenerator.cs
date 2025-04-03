using Assets.Game.Systems.TerrainSystem.Biomes;
using Assets.Game.Systems.TerrainSystem.NoiseGenerators;
using System;
using UnityEngine;

namespace Assets.Game.Systems.TerrainSystem.Generators
{
    public class TerrainGenerator
    {
        // Terrain parameters
        private int width;
        private int height;
        private float maxHeight;

        // Dependencies (following Dependency Inversion Principle)
        private CombinedNoiseGenerator noiseGenerator;

        // Event for notifying when terrain generation is complete
        public event Action<TerrainData> OnTerrainGenerated;

        public TerrainGenerator(int width, int height, CombinedNoiseGenerator noiseGenerator)
        {
            this.width = width;
            this.height = height;
            this.noiseGenerator = noiseGenerator;
        }

        /// <summary>
        /// Generates terrain data using the combined noise generator.
        /// </summary>
        /// <param name="biomeConfig">Biome configuration to use for terrain generation</param>
        /// <returns>Generated UnityEngine.TerrainData</returns>
        public TerrainData GenerateTerrain(BiomeConfig biomeConfig, Vector2Int chunkCoord = default)
        {
            // Get max height from biome configuration
            maxHeight = biomeConfig.maxHeight;

            // Create new terrain data
            TerrainData terrainData = new TerrainData();

            // Set terrain size (XZ scale and height)
            terrainData.heightmapResolution = width + 1;
            terrainData.size = new Vector3(width, maxHeight, height);

            // Add terrain layers to the terrain data
            // This goes here, after creating TerrainData but before calling ApplyTerrainTextures
            if (biomeConfig.terrainLayers != null && biomeConfig.terrainLayers.Length > 0)
            {
                terrainData.terrainLayers = biomeConfig.terrainLayers;
            }
            else
            {
                Debug.LogWarning("No terrain layers assigned to biome: " + biomeConfig.biomeName);
            }

            // Generate the heightmap with chunk coordinates
            float[,] heights = GenerateHeightmap(chunkCoord);

            // Apply smoothing
            heights = SmoothHeightmap(heights, biomeConfig.smoothingIterations, biomeConfig.smoothingFactor);

            // Apply erosion if iterations > 0
            if (biomeConfig.erosionIterations > 0)
            {
                TerrainErosion erosion = new TerrainErosion();
                heights = erosion.ApplyThermalErosion(heights, biomeConfig.erosionIterations, biomeConfig.erosionTalus);
                heights = erosion.ApplyHydraulicErosion(heights, biomeConfig.erosionIterations / 2);
            }

            // Set heights on terrain data
            terrainData.SetHeights(0, 0, heights);

            // Apply textures to the terrain
            // ApplyTerrainTextures(terrainData, biomeConfig);

            // Notify listeners that terrain generation is complete
            OnTerrainGenerated?.Invoke(terrainData);

            return terrainData;
        }

        private float[,] GenerateHeightmap(Vector2Int chunkCoord = default)
        {
            float[,] heights = new float[width + 1, height + 1];

            // Generate height values for each point, offset by chunk coordinates
            int xOffset = chunkCoord.x * width;
            int yOffset = chunkCoord.y * height;

            for (int x = 0; x <= width; x++)
            {
                for (int y = 0; y <= height; y++)
                {
                    heights[x, y] = noiseGenerator.GenerateNoise(
                        x + xOffset,
                        y + yOffset
                    );
                }
            }

            return heights;
        }

        /// <summary>
        /// Updates terrain generator parameters.
        /// </summary>
        public void UpdateParameters(int newWidth, int newHeight)
        {
            this.width = newWidth;
            this.height = newHeight;
        }

        private float[,] SmoothHeightmap(float[,] heights, int iterations, float factor)
        {
            int width = heights.GetLength(0);
            int height = heights.GetLength(1);
            float[,] smoothedHeights = (float[,])heights.Clone();

            for (int iter = 0; iter < iterations; iter++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    for (int y = 1; y < height - 1; y++)
                    {
                        float avgHeight = 0;
                        int count = 0;

                        for (int nx = -1; nx <= 1; nx++)
                        {
                            for (int ny = -1; ny <= 1; ny++)
                            {
                                avgHeight += heights[x + nx, y + ny];
                                count++;
                            }
                        }

                        avgHeight /= count;
                        smoothedHeights[x, y] = Mathf.Lerp(heights[x, y], avgHeight, factor);
                    }
                }

                // Copy smoothed back to heights for next iteration
                heights = (float[,])smoothedHeights.Clone();
            }

            return smoothedHeights;
        }

        private void ApplyTerrainTextures(TerrainData terrainData, BiomeConfig biomeConfig)
        {
            // You'll need to add textures for each biome in the BiomeConfig
            // This is just a starting structure

            // Get heights and calculate slopes
            float[,] heights = terrainData.GetHeights(0, 0, terrainData.heightmapResolution, terrainData.heightmapResolution);
            float[,] slopeMap = CalculateSlopeMap(heights, terrainData);

            // Create alphamap for splatting
            int alphamapResolution = terrainData.alphamapResolution;
            float[,,] alphamap = new float[alphamapResolution, alphamapResolution, 4]; // 4 textures

            for (int y = 0; y < alphamapResolution; y++)
            {
                for (int x = 0; x < alphamapResolution; x++)
                {
                    // Convert alphamap coordinates to heightmap coordinates
                    float normX = x / (float)alphamapResolution;
                    float normY = y / (float)alphamapResolution;

                    int heightmapX = Mathf.FloorToInt(normX * terrainData.heightmapResolution);
                    int heightmapY = Mathf.FloorToInt(normY * terrainData.heightmapResolution);

                    // Get height and slope at this location
                    float height = heights[heightmapX, heightmapY];
                    float slope = slopeMap[heightmapX, heightmapY];

                    // Calculate texture weights based on biome, height and slope
                    // e.g., steep slopes get rock texture, flat highlands get grass, etc.
                    float[] weights = CalculateTextureWeights(height, slope, biomeConfig);

                    // Apply the weights to the alphamap
                    for (int i = 0; i < 4; i++)
                    {
                        alphamap[y, x, i] = weights[i];
                    }
                }
            }

            // Apply the alphamap to the terrain
            terrainData.SetAlphamaps(0, 0, alphamap);
        }

        private float[,] CalculateSlopeMap(float[,] heights, TerrainData terrainData)
        {
            int width = heights.GetLength(0);
            int height = heights.GetLength(1);
            float[,] slopeMap = new float[width, height];

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    // Calculate slope using neighbors (simple gradient magnitude)
                    float dx = heights[x + 1, y] - heights[x - 1, y];
                    float dy = heights[x, y + 1] - heights[x, y - 1];

                    // Normalize by cell size
                    dx /= 2f / width;
                    dy /= 2f / height;

                    // Calculate slope (0-1 range, where 1 is 45 degrees)
                    float slope = Mathf.Sqrt(dx * dx + dy * dy);
                    slopeMap[x, y] = Mathf.Clamp01(slope * 5f); // Scale for better visibility
                }
            }

            return slopeMap;
        }

        private float[] CalculateTextureWeights(float height, float slope, BiomeConfig biomeConfig)
        {
            float[] weights = new float[4]; // Assuming we use 4 textures

            // Initialize all weights to 0
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = 0;
            }

            // Calculate weights based on biome type, height and slope
            switch (biomeConfig.biomeType)
            {
                case BiomeConfig.BiomeType.Forest:
                    // Example: Texture 0 = dirt, 1 = grass, 2 = rock, 3 = forest floor
                    weights[0] = Mathf.Clamp01((0.1f - height) * 10); // Dirt at very low heights
                    weights[1] = Mathf.Clamp01(1 - slope * 2) * Mathf.Clamp01((height - 0.1f) * 5); // Grass on flat areas
                    weights[2] = Mathf.Clamp01(slope * 1.5f); // Rock on slopes
                    weights[3] = Mathf.Clamp01((height - 0.3f) * 3) * Mathf.Clamp01(1 - slope); // Forest floor at higher flat areas
                    break;

                case BiomeConfig.BiomeType.Desert:
                    // Example: Texture 0 = sand, 1 = dry ground, 2 = rock, 3 = dunes
                    weights[0] = Mathf.Clamp01(1 - height * 5); // Sand at low heights
                    weights[1] = Mathf.Clamp01((height - 0.2f) * 5) * Mathf.Clamp01(1 - slope); // Dry ground at mid heights
                    weights[2] = Mathf.Clamp01(slope * 2); // Rock on slopes
                    weights[3] = Mathf.Clamp01(height * 3) * Mathf.Clamp01(1 - slope * 3); // Dunes at higher flat areas
                    break;

                // Add cases for other biome types...

                default:
                    // Default: even distribution
                    for (int i = 0; i < weights.Length; i++)
                    {
                        weights[i] = 0.25f;
                    }
                    break;
            }

            // Normalize weights
            float totalWeight = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                totalWeight += weights[i];
            }

            if (totalWeight > 0)
            {
                for (int i = 0; i < weights.Length; i++)
                {
                    weights[i] /= totalWeight;
                }
            }
            else
            {
                // If all weights are 0, use the first texture
                weights[0] = 1;
            }

            return weights;
        }
    }
}
