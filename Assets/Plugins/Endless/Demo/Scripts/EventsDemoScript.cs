using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

namespace EndlessTerrain
{
    public class EventsDemoScript : MonoBehaviour
    {
        [SerializeField]
        private TerrainManager terrainManager;
        [SerializeField]
        private float spawnChance;
        [SerializeField]
        private float distance;

        private void Start()
        { 
            terrainManager.TextureNoiseCalculated += OnChunkNoiseGenerated;
        }

        private void OnChunkNoiseGenerated(object source, ref NativeArray<float> textureNoise, 
            Vector2Int size, Vector2Int coords)
        {
            int seed = TerrainManagerUtility.GetSeed(coords);
            System.Random prng = new System.Random(seed);
            if(prng.NextDouble() <= spawnChance)
            {
                for(int i = 0; i < size.x * size.y; i++)
                {
                    int xIndex = i % size.y;
                    int zIndex = i / size.y;

                    float dist = 1f - Mathf.Clamp01(Vector2.Distance(new Vector2(xIndex, zIndex), 
                        new Vector2(size.x / 2f, size.y / 2f)) / distance);

                    textureNoise[i] -= dist;
                }
            }
        }
    }
}
