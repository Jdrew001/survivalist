using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessTerrain
{
    [System.Serializable]
    public class TextureLayer
    {
        public string name;
        //Textures used in this texture layer
        public List<TerrainLayerTextureSet> textures;
        //The start height of the layer based on total height percentage (max height = max possible height of terrain)
        public float startHeight;
    }
}

