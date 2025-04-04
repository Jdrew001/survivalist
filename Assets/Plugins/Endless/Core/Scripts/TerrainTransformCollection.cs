using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

namespace EndlessTerrain
{
    public class TerrainTransformCollection
    {
        public NativeArray<TerrainObjectTransform> transforms;
        public bool combined;
        public Mesh combinedMesh;
        public List<GameObject> spawnedObjects = new List<GameObject>();
        public bool spawned;

        public TerrainTransformCollection(NativeArray<TerrainObjectTransform> transforms, bool combined)
        {
            this.transforms = transforms;
            this.combined = combined;
        }

        public void Dispose()
        {
            if (transforms.IsCreated)
            {
                transforms.Dispose();
            }
        }
    }

    public class TerrainStructureCollection
    {
        public NativeArray<TerrainStructureTransform> transforms;
        public List<GameObject> spawnedObjects = new List<GameObject>();
        public bool spawned;

        public TerrainStructureCollection(NativeArray<TerrainStructureTransform> transforms)
        {
            this.transforms = transforms;
        }

        public void Dispose()
        {
            if (transforms.IsCreated)
            {
                transforms.Dispose();
            }
        }
    }
}
