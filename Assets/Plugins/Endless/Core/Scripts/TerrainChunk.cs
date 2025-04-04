using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

namespace EndlessTerrain
{
    public class TerrainChunk
    {
        private Vector3Int size;
        public Vector3Int Size
        {
            get { return size; }
        }

        private Vector2Int chunkCoords;
        public Vector2Int ChunkCoords
        {
            get { return chunkCoords; }
        }

        private NativeArray<Vector3> verts;
        public NativeArray<Vector3> Verts
        {
            get { return verts; }
        }

        private NativeArray<Vector3> normals;
        public NativeArray<Vector3> Normals
        {
            get { return normals; }
        }

        private NativeArray<int> biomeArr;
        public NativeArray<int> BiomeArr
        {
            get { return biomeArr; }
        }

        private NativeArray<float> tempNoise;
        public NativeArray<float> TempNoise
        {
            get { return tempNoise; }
        }

        private NativeArray<float> moistureNoise;
        public NativeArray<float> MoistureNoise
        {
            get { return moistureNoise; }
        }

        private NativeArray<float> weights;
        public NativeArray<float> Weights
        {
            get { return weights; }
            set { weights = value; }
        }
        public void SetWeight(int index, float weight)
        {
            weights[index] = weight;
        }

        public void AddWeight(int index, float weight)
        {
            weights[index] += weight;
        }


        private NativeArray<float> vegetationNoise;
        public NativeArray<float> VegetationNoise
        {
            get { return vegetationNoise; }
        }
        public float GetVegetationNoise(int xCoord, int yCoord)
        {
            return vegetationNoise[xCoord * size.x + yCoord];
        }

        private NativeArray<float> rockNoise;
        public NativeArray<float> RockNoise
        {
            get { return rockNoise; }
        }

        private NativeArray<float> roadWeights;
        public NativeArray<float> RoadWeights
        {
            get { return roadWeights; }
        }

        private NativeArray<float> roadStartHeights;
        public NativeArray<float> RoadStartHeights
        {
            get { return roadStartHeights; }
        }



        private GameObject chunkObject;
        public GameObject ChunkObject
        {
            get { return chunkObject; }
            set { chunkObject = value; }
        }

        private GameObject waterObject;
        public GameObject WaterObject
        {
            get { return waterObject; }
            set { waterObject = value; }
        }

        private Mesh terrainMesh;
        public Mesh TerrainMesh
        {
            get { return terrainMesh; }
        }
        public void ApplyTerrainMesh(Mesh terrainMesh)
        {
            this.terrainMesh = terrainMesh;
            if (chunkObject != null)
            {
                chunkObject.GetComponent<MeshFilter>().mesh = terrainMesh;
                chunkObject.GetComponent<MeshCollider>().sharedMesh = terrainMesh;
            }
        }

        private Dictionary<int, TerrainTransformCollection> terrainObjectTransforms =
            new Dictionary<int, TerrainTransformCollection>();
        public Dictionary<int, TerrainTransformCollection> TerrainObjectTransforms
        {
            get { return terrainObjectTransforms; }
            set { terrainObjectTransforms = value; }
        }

        private Dictionary<int, TerrainStructureCollection> terrainStructureTransforms =
            new Dictionary<int, TerrainStructureCollection>();
        public Dictionary<int, TerrainStructureCollection> TerrainStructureTransforms
        {
            get { return terrainStructureTransforms; }
            set { terrainStructureTransforms = value; }
        }

        public TerrainChunk()
        {
        }

        public TerrainChunk(Vector2Int chunkCoords, ref NativeArray<float> weights, NativeArray<Vector3> normals,
            NativeArray<Vector3> verts, ref NativeArray<float> tempNoise, ref NativeArray<float> moistureNoise,
            ref NativeArray<int> biomeArr, ref NativeArray<float> vegetationNoise, ref NativeArray<float> rockNoise,
            ref NativeArray<float> roadWeights, ref NativeArray<float> roadStartHeights, Mesh terrainMesh, Vector3Int size)
        {
            this.chunkCoords = chunkCoords;
            this.weights = weights;
            this.normals = normals;
            this.verts = verts;
            this.tempNoise = tempNoise;
            this.moistureNoise = moistureNoise;
            this.biomeArr = biomeArr;
            this.vegetationNoise = vegetationNoise;
            this.rockNoise = rockNoise;
            this.terrainMesh = terrainMesh;
            this.size = size;
            this.roadWeights = roadWeights;
            this.roadStartHeights = roadStartHeights;
        }

        public void Dispose()
        {
            if (weights.IsCreated)
            {
                weights.Dispose();
            }
            if (verts.IsCreated)
            {
                verts.Dispose();
            }
            if (normals.IsCreated)
            {
                normals.Dispose();
            }
            if (tempNoise.IsCreated)
            {
                tempNoise.Dispose();
            }
            if (moistureNoise.IsCreated)
            {
                moistureNoise.Dispose();
            }
            if (biomeArr.IsCreated)
            {
                biomeArr.Dispose();
            }
            if (vegetationNoise.IsCreated)
            {
                vegetationNoise.Dispose();
            }
            if(rockNoise.IsCreated)
            {
                rockNoise.Dispose();
            }
            if (roadWeights.IsCreated)
            {
                roadWeights.Dispose();
            }
            if (roadStartHeights.IsCreated)
            {
                roadStartHeights.Dispose();
            }

            foreach (KeyValuePair<int, TerrainTransformCollection> transformCollection in terrainObjectTransforms)
            {
                transformCollection.Value.Dispose();
            }
            foreach (KeyValuePair<int, TerrainStructureCollection> transformCollection in terrainStructureTransforms)
            {
                transformCollection.Value.Dispose();
            }
        }
    }
}