using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace EndlessTerrain
{
    public static class ObjectPoolManager
    {
        public static Dictionary<string, ObjectPool> pools = new Dictionary<string, ObjectPool>();

        public static ObjectPool CreatePool(GameObject prefab, int size)
        {
            GameObject poolObject = new GameObject();
            ObjectPool pool = poolObject.AddComponent<ObjectPool>();
            poolObject.name = prefab.name + " Object Pool";

            pool.SetPrefab(prefab);
            pool.AddToPool(size);
            pools.Add(prefab.name, pool);

            return pool;
        }

        public static bool PoolExists(string name)
        {
            return pools.ContainsKey(name);
        }

        public static void ClearPools()
        {
            foreach (KeyValuePair<string, ObjectPool> pool in pools)
            {
                pool.Value.DestroyPool();
            }
            pools.Clear();
        }
    }
}