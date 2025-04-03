using UnityEngine;

namespace Assets.Game.Systems.TerrainSystem.NoiseGenerators
{
    public class RidgedNoiseGenerator : INoiseGenerator
    {
        private float scale = 50f;
        private int seed;
        private Vector2 offset;

        public RidgedNoiseGenerator()
        {
            SetSeed(Random.Range(0, 100000));
        }

        public RidgedNoiseGenerator(float scale, int seed)
        {
            this.scale = scale;
            SetSeed(seed);
        }

        public float GenerateNoise(int x, int y)
        {
            float xCoord = (float)x / scale + offset.x;
            float yCoord = (float)y / scale + offset.y;

            // Generate base Perlin noise
            float noise = Mathf.PerlinNoise(xCoord, yCoord);

            // Invert and redistribute to create ridges
            // 1.0 - |2 * (noise - 0.5)| creates sharp ridges at 0.5 value
            float ridgedNoise = 1f - Mathf.Abs(2f * (noise - 0.5f));

            // Shape the ridges further with a power function
            ridgedNoise = Mathf.Pow(ridgedNoise, 2f);

            return ridgedNoise;
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
