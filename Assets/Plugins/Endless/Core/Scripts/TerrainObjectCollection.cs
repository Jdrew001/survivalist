using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EndlessTerrain;

namespace EndlessTerrain
{
    public class TerrainObjectCollection
    {
        //Collection of objects
        public Dictionary<string, List<GameObject>> objects = new Dictionary<string, List<GameObject>>();
        //Collection of transforms
        public Dictionary<string, List<TerrainObjectTransform>> transforms = new Dictionary<string, List<TerrainObjectTransform>>();
        //Collection of currently in use Transforms
        public Dictionary<string, List<TerrainObjectTransform>> usedTransforms = new Dictionary<string, List<TerrainObjectTransform>>();
        //Collection of combinedMeshes
        public Dictionary<string, GameObject> combinedMeshes = new Dictionary<string, GameObject>();

        public TerrainObjectCollection()
        {

        }

        public TerrainObjectCollection(Dictionary<string, List<GameObject>> objects, Dictionary<string, List<TerrainObjectTransform>> transforms, Dictionary<string,
            List<TerrainObjectTransform>> usedTransforms, Dictionary<string, GameObject> combinedMeshes)
        {
            this.objects = objects;
            this.transforms = transforms;
            this.usedTransforms = usedTransforms;
            this.combinedMeshes = combinedMeshes;
        }
    }
}