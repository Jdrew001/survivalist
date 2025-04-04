using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessTerrain
{
    public struct TerrainObjectTransform
    {
        private Vector3 position;
        private Quaternion rotation;
        private Vector3 scale;

        public TerrainObjectTransform(Vector3 position, Vector3 eulerAngles, Vector3 scale)
        {
            this.position = position;
            rotation = new Quaternion();
            rotation.eulerAngles = eulerAngles;
            this.scale = scale;
        }

        public TerrainObjectTransform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }

        public Vector3 GetPos()
        {
            return position;
        }

        public Vector3 GetPos(bool noHeight)
        {
            if (noHeight)
            {
                return new Vector3(position.x, 1f, position.z);
            }
            else
            {
                return position;
            }
        }

        public Quaternion GetRot()
        {
            return rotation;
        }

        public Vector3 GetScale()
        {
            return scale;
        }

        public void SetPos(Vector3 position)
        {
            this.position = position;
        }

        public void SetRot(Quaternion rotation)
        {
            this.rotation = rotation;
        }

        public void SetScale(Vector3 scale)
        {
            this.scale = scale;
        }
    }

    public struct TerrainStructureTransform
    {
        private Vector3 position;
        private Quaternion rotation;
        private Vector3 scale;
        private Vector3Int influenceBounds;
        private int index;

        public TerrainStructureTransform(Vector3 position, Vector3 eulerAngles, Vector3 scale,
            Vector3Int influenceBounds, int index)
        {
            this.position = position;
            rotation = new Quaternion();
            rotation.eulerAngles = eulerAngles;
            this.scale = scale;
            this.influenceBounds = influenceBounds;
            this.index = index;
        }

        public TerrainStructureTransform(Vector3 position, Quaternion rotation, Vector3 scale,
            Vector3Int influenceBounds, int index)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
            this.influenceBounds = influenceBounds;
            this.index = index;
        }

        public Vector3 GetPos()
        {
            return position;
        }

        public Quaternion GetRot()
        {
            return rotation;
        }

        public Vector3 GetScale()
        {
            return scale;
        }

        public int GetIndex()
        {
            return index;
        }

        public Vector3Int GetInfluenceBounds()
        {
            return influenceBounds;
        }

        public void SetPos(Vector3 position)
        {
            this.position = position;
        }

        public void SetRot(Quaternion rotation)
        {
            this.rotation = rotation;
        }

        public void SetScale(Vector3 scale)
        {
            this.scale = scale;
        }

        public void SetIndex(int index)
        {
            this.index = index;
        }

        public void SetInfluenceBounds(Vector3Int influenceBounds)
        {
            this.influenceBounds = influenceBounds;
        }
    }
}