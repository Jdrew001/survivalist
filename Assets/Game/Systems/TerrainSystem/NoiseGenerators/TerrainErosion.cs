using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Game.Systems.TerrainSystem.NoiseGenerators
{
    public class TerrainErosion
    {
        public float[,] ApplyThermalErosion(float[,] heights, int iterations, float talus)
        {
            int width = heights.GetLength(0);
            int height = heights.GetLength(1);

            for (int iter = 0; iter < iterations; iter++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    for (int y = 1; y < height - 1; y++)
                    {
                        // Check neighbors for steep slopes
                        for (int nx = -1; nx <= 1; nx++)
                        {
                            for (int ny = -1; ny <= 1; ny++)
                            {
                                if (nx == 0 && ny == 0) continue;

                                float heightDiff = heights[x, y] - heights[x + nx, y + ny];
                                if (heightDiff > talus)
                                {
                                    float transfer = heightDiff * 0.5f;
                                    heights[x, y] -= transfer;
                                    heights[x + nx, y + ny] += transfer;
                                }
                            }
                        }
                    }
                }
            }

            return heights;
        }

        public float[,] ApplyHydraulicErosion(float[,] heights, int iterations)
        {
            // Simplified hydraulic erosion
            // In a real implementation, this would simulate water flow and sediment transport
            // Simplified implementation here

            return ApplyThermalErosion(heights, iterations / 2, 0.01f);
        }
    }
}
