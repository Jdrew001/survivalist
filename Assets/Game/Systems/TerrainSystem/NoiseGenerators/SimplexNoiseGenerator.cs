using UnityEngine; // Make sure FastNoiseLite is imported in your project

namespace Assets.Game.Systems.TerrainSystem.NoiseGenerators
{
    public class SimplexNoiseGenerator : INoiseGenerator
    {
        private FastNoiseLite noise;
        private float scale = 50f;

        public SimplexNoiseGenerator()
        {
            noise = new FastNoiseLite();
            // Using OpenSimplex2 for a high-quality simplex noise variant.
            noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            SetSeed(Random.Range(0, 100000));
        }

        public SimplexNoiseGenerator(float scale, int seed)
        {
            noise = new FastNoiseLite();
            noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            this.scale = scale;
            SetSeed(seed);
        }

        public float GenerateNoise(int x, int y)
        {
            // FastNoiseLite returns noise values in [-1, 1]. Normalize to [0, 1].
            float simplexValue = noise.GetNoise(x / scale, y / scale);
            return (simplexValue + 1f) * 0.5f;
        }

        public void SetScale(float scale)
        {
            this.scale = Mathf.Max(0.1f, scale);
        }

        public void SetSeed(int seed)
        {
            noise.SetSeed(seed);
        }
    }
}
