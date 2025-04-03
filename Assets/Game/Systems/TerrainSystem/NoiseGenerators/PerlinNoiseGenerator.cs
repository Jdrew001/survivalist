using UnityEngine;

namespace Assets.Game.Systems.TerrainSystem.NoiseGenerators
{
    /// <summary>
    /// Concrete implementation of the INoiseGenerator interface using Perlin noise.
    /// Follows Single Responsibility Principle by handling only Perlin noise generation.
    /// </summary>
    public class PerlinNoiseGenerator : INoiseGenerator
    {
        private float scale = 50f;
        private int seed;
        private Vector2 offset;

        public PerlinNoiseGenerator()
        {
            SetSeed(Random.Range(0, 100000));
        }

        public PerlinNoiseGenerator(float scale, int seed)
        {
            this.scale = scale;
            SetSeed(seed);
        }

        public float GenerateNoise(int x, int y)
        {
            float xCoord = (float)x / scale + offset.x;
            float yCoord = (float)y / scale + offset.y;

            return Mathf.PerlinNoise(xCoord, yCoord);
        }

        public void SetScale(float scale)
        {
            this.scale = Mathf.Max(0.1f, scale);
        }

        public void SetSeed(int seed)
        {
            this.seed = seed;
            System.Random rng = new System.Random(seed);
            offset = new Vector2((float)rng.NextDouble() * 100f, (float)rng.NextDouble() * 100f);
        }
    }
}
