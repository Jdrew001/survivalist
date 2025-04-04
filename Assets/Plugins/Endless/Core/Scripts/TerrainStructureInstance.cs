using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessTerrain
{
    [System.Serializable]
    public class TerrainStructureInstance
    {
        //Has no purpose other than organizational
        public string name;

        public float spawnChance;
        public float minRoadWeight;
        public float minElevation;
        public float maxElevation;
        public int minSpawnedPrefabs;
        public int maxSpawnedPrefabs;
        public float structureRadius;
        public List<TerrainStructurePrefab> terrainStructurePrefabs = new List<TerrainStructurePrefab>();
        public enum ConnectionMode { None, Nearest }
        public ConnectionMode connectionMode;

        [HideInInspector]
        public int biome;
        [HideInInspector]
        public int index;
    }

    public struct TerrainStructureInstanceStruct
    {
        public float spawnChance;
        public float minRoadWeight;
        public float minElevation;
        public float maxElevation;
        public int minSpawnedPrefabs;
        public int maxSpawnedPrefabs;
        public float structureRadius;
        public TerrainStructureInstance.ConnectionMode connectionMode;

        public int biome;
        public int index;

        public TerrainStructureInstanceStruct(TerrainStructureInstance structure)
        {
            spawnChance = structure.spawnChance;
            minRoadWeight = structure.minRoadWeight;
            minElevation = structure.minElevation;
            maxElevation = structure.maxElevation;
            minSpawnedPrefabs = structure.minSpawnedPrefabs;
            maxSpawnedPrefabs = structure.maxSpawnedPrefabs;
            structureRadius = structure.structureRadius;
            connectionMode = structure.connectionMode;
            biome = structure.biome;
            index = structure.index;
        }
    }

    [System.Serializable]
    public class TerrainStructurePrefab
    {
        public GameObject prefab;
        public float spawnChance;
        public float cullDistance;
        public Vector3Int influenceBounds;
        public float minNeighborDistance;
        public int maxSpawned;
        public enum HeightMode { Elevation, Height };
        public HeightMode heightMode;
        public float minimumSpawnHeight;
        public float maximumSpawnHeight;
        public float spawnHeight;
        [HideInInspector]
        public int index;
    }

    public struct TerrainStructurePrefabStruct
    {
        public float spawnChance;
        public Vector3Int influenceBounds;
        public float minNeighborDistance;
        public int maxSpawned;
        public Vector3 scale;
        public int index;

        public TerrainStructurePrefabStruct(TerrainStructurePrefab terrainStructurePrefab)
        {
            spawnChance = terrainStructurePrefab.spawnChance;
            influenceBounds = terrainStructurePrefab.influenceBounds;
            minNeighborDistance = terrainStructurePrefab.minNeighborDistance;
            maxSpawned = terrainStructurePrefab.maxSpawned;
            scale = terrainStructurePrefab.prefab.transform.localScale;
            index = terrainStructurePrefab.index;
        }
    }
}