using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessTerrain
{
    [System.Serializable]
    public class TerrainObjectInstance
    {
        //Has no purpose other than organizational
        [Tooltip("The name of this object. Has no purpose other than organizational.")]
        public string name;

        [Tooltip("The prefab used to spawn this object.")]
        public GameObject prefab;

        [Tooltip("How many chunks away an object has to be before it is culled.")]
        public float cullDistance;
        public float minSpawnHeight;
        public float maxSpawnHeight;
        public float minVegetationNoise;
        public float maxVegetationNoise;
        public float minRockNoise;
        public float maxRockNoise;
        [Tooltip("The chance for an object to spawn at a given vertex.")]
        [Range(0f, 100f)]
        public float spawnChance;
        public float minSteepness;
        public float maxSteepness;
        [Tooltip("How strongly this objects rotation is adjusted to match the slope of the vertex.")]
        [Range(0f, 1f)]
        public float slopeWeight;
        [Tooltip("Offset applied to y-value of object spawn position.")]
        public float heightOffset;
        [Tooltip("Whether height offset is applied to world-up or vertex-up.")]
        public bool heightOffsetRelativeToSlope;
        [Tooltip("Whether to randomize scale.")]
        public bool randomScale;
        [Tooltip("Whether randomized scale should have matching x, y, and z values.")]
        public bool uniformScale;
        [Tooltip("The range of randomized scale.")]
        public Vector2 scale;
        [Tooltip("The range of x value of randomized scale.")]
        public Vector2 scaleX;
        [Tooltip("The range of x value of randomized scale.")]
        public Vector2 scaleY;
        [Tooltip("The range of x value of randomized scale.")]
        public Vector2 scaleZ;
        [Tooltip("Whether all instances of this object in one chunk should be combined into one game-object. " +
            "Can help save performance when frequently loading and unloading chunks.")]
        public bool combineMesh;

        [HideInInspector]
        public int biome;
        [HideInInspector]
        public int index;
        [HideInInspector]
        public bool rockNoiseEnabled;
    }

    public struct TerrainObjectInstanceStruct
    {
        public float minSpawnHeight;
        public float maxSpawnHeight;
        public float minVegetationNoise;
        public float maxVegetationNoise;
        public float minRockNoise;
        public float maxRockNoise;
        public float spawnChance;
        public float minSteepness;
        public float maxSteepness;
        public float slopeWeight;
        public float heightOffset;
        public bool heightOffsetRelativeToSlope;
        public bool randomScale;
        public bool uniformScale;
        public Vector2 scale;
        public Vector2 scaleX;
        public Vector2 scaleY;
        public Vector2 scaleZ;
        public bool combineMesh;
        public Vector3 objectScale;
        public bool rockNoiseEnabled;

        public int biome;
        public int index;

        public TerrainObjectInstanceStruct(TerrainObjectInstance terrainObjectInstance)
        {
            minSpawnHeight = terrainObjectInstance.minSpawnHeight;
            maxSpawnHeight = terrainObjectInstance.maxSpawnHeight;
            minVegetationNoise = terrainObjectInstance.minVegetationNoise;
            maxVegetationNoise = terrainObjectInstance.maxVegetationNoise;
            minRockNoise = terrainObjectInstance.minRockNoise;
            maxRockNoise = terrainObjectInstance.maxRockNoise;
            spawnChance = terrainObjectInstance.spawnChance;
            minSteepness = terrainObjectInstance.minSteepness;
            maxSteepness = terrainObjectInstance.maxSteepness;
            slopeWeight = terrainObjectInstance.slopeWeight;
            heightOffset = terrainObjectInstance.heightOffset;
            heightOffsetRelativeToSlope = terrainObjectInstance.heightOffsetRelativeToSlope;
            randomScale = terrainObjectInstance.randomScale;
            uniformScale = terrainObjectInstance.uniformScale;
            scale = terrainObjectInstance.scale;
            scaleX = terrainObjectInstance.scaleX;
            scaleY = terrainObjectInstance.scaleY;
            scaleZ = terrainObjectInstance.scaleZ;
            combineMesh = terrainObjectInstance.combineMesh;
            objectScale = terrainObjectInstance.prefab.transform.localScale;

            rockNoiseEnabled = terrainObjectInstance.rockNoiseEnabled;
            biome = terrainObjectInstance.biome;
            index = terrainObjectInstance.index;
        }
    }
}