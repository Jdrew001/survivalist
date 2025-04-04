using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessTerrain
{
    [System.Serializable]
    public class TextureSet
    {
        [SerializeField]
        public Texture2D baseColor;
        [SerializeField]
        public Texture2D maskMap;
        [SerializeField]
        public Texture2D normalMap;
        [SerializeField]
        public float textureScale;

        public bool CheckNull()
        {
            return baseColor != null && maskMap != null && normalMap != null;
        }

        public bool CheckReadable()
        {
            if (CheckNull())
            {
                return baseColor.isReadable && maskMap.isReadable && normalMap.isReadable;
            }
            return false;
        }

        public bool CheckSize(int size)
        {
            if (CheckNull())
            {
                return baseColor.width == size && baseColor.height == size && maskMap.width == size / 2 &&
                    maskMap.height == size / 2 && normalMap.width == size / 2 && normalMap.height == size / 2;
            }
            return false;
        }
    }

    [System.Serializable]
    public class TerrainLayerTextureSet
    {
        public TextureSet textureSet;
        public float vegetationStartHeight;
    }
}
