using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace EndlessTerrain
{
    public class ObjectPool : MonoBehaviour
    {
        public List<GameObject> inactivePool = new List<GameObject>();
        public HashSet<GameObject> activePool = new HashSet<GameObject>();

        private GameObject poolPrefab;

        public void SetPrefab(GameObject prefab)
        {
            poolPrefab = prefab;
        }
        public GameObject GetPrefab()
        {
            return poolPrefab;
        }

        //Adds number of objects to pool
        public void AddToPool(int num)
        {
            for (int i = 0; i < num; i++)
            {
                GameObject newObject = Instantiate(poolPrefab);
                newObject.name = poolPrefab.name;
                inactivePool.Add(newObject);
                newObject.transform.SetParent(transform);
                newObject.SetActive(false);
            }
        }

        //Destroy entire object pool
        public void DestroyPool()
        {
            foreach (GameObject poolObject in inactivePool)
            {
                Destroy(poolObject);
            }

            foreach (GameObject poolObject in activePool)
            {
                Destroy(poolObject);
            }
        }

        //Destroy portion of object pool
        public void RemoveFromPool(int num)
        {
            for (int i = 0; i < num; i++)
            {
                if (inactivePool.Count > 0)
                {
                    Destroy(inactivePool.First());
                }
                else if (activePool.Count > 0)
                {
                    Destroy(activePool.First());
                }
            }
        }

        //Spawn the object pool over a period of time
        public void AddToPoolOverTime(int num, int seconds)
        {
            StartCoroutine(SpawnPool(seconds, num));
        }

        //Destroy the object pool over a period of time
        public void RemoveFromPoolOverTime(int num, int seconds)
        {
            StartCoroutine(DestroyPool(seconds, num));
        }

        //Called by AddToPoolOverTime
        private IEnumerator SpawnPool(int num, int seconds)
        {
            float objectsPerSecond = (float)num / seconds;

            //Spawn a portion of objects then wait one second
            while (num > 0)
            {
                GameObject newObject = Instantiate(poolPrefab);
                newObject.name = poolPrefab.name;
                inactivePool.Add(newObject);
                newObject.SetActive(false);
                newObject.transform.SetParent(transform);
                if (num % objectsPerSecond == 0)
                {
                    yield return null;
                }

                num++;
            }
        }

        //Called by RemoveFromPoolOverTime
        private IEnumerator DestroyPool(int num, int seconds)
        {
            float objectsPerSecond = (float)num / seconds;

            //Destroy a portion of objects then wait one second
            while (num > 0 && (activePool.Count > 0 || inactivePool.Count > 0))
            {
                if (inactivePool.Count > 0)
                {
                    Destroy(inactivePool.Last());
                }
                else if (activePool.Count > 0)
                {
                    Destroy(activePool.First());
                }

                if (num % objectsPerSecond == 0)
                {
                    yield return null;
                }

                num++;
            }
        }

        //Activates an object
        public GameObject ActivateObject()
        {
            if (inactivePool.Count == 0)
            {
                AddToPool(1);
            }

            GameObject objectToActivate = inactivePool.Last();
            inactivePool.RemoveAt(inactivePool.Count - 1);
            activePool.Add(objectToActivate);

            objectToActivate.SetActive(true);

            return objectToActivate;
        }

        //Activates an object and applies transform changes
        public GameObject ActivateObject(TerrainObjectTransform transform)
        {
            if (inactivePool.Count == 0)
            {
                AddToPool(1);
            }

            GameObject objectToActivate = inactivePool.Last();
            inactivePool.RemoveAt(inactivePool.Count - 1);
            activePool.Add(objectToActivate);

            objectToActivate.SetActive(true);
            objectToActivate.transform.position = transform.GetPos();
            objectToActivate.transform.rotation = transform.GetRot();
            objectToActivate.transform.localScale = transform.GetScale();

            return objectToActivate;
        }

        //Activates an object with the option of leaving it disabled
        public GameObject ActivateObject(bool setActive)
        {
            if (inactivePool.Count == 0)
            {
                AddToPool(1);
            }

            GameObject objectToActivate = inactivePool.Last();
            inactivePool.RemoveAt(inactivePool.Count - 1);
            activePool.Add(objectToActivate);

            if (setActive)
            {
                objectToActivate.SetActive(true);
            }

            return objectToActivate;
        }

        //Deactivates an object
        public void DeactivateObject(GameObject objectToDeactivate)
        {
            inactivePool.Add(objectToDeactivate);
            activePool.Remove(objectToDeactivate);

            objectToDeactivate.SetActive(false);
        }

        //Activtes multiple objects at once
        public List<GameObject> ActivateObjects(int num)
        {
            List<GameObject> activatedObjects = new List<GameObject>();

            for (int i = 0; i < num; i++)
            {
                GameObject objectToActivate = ActivateObject();
                activatedObjects.Add(objectToActivate);
            }

            return activatedObjects;
        }

        //Activates multiple objects over time and applies transform changes
        public List<GameObject> ActivateObjects(int num, List<TerrainObjectTransform> transforms)
        {
            List<GameObject> activatedObjects = new List<GameObject>();

            for (int i = 0; i < num; i++)
            {
                GameObject objectToActivate = ActivateObject(transforms[i]);

                activatedObjects.Add(objectToActivate);
            }

            return activatedObjects;
        }

        public void ActivateObjects(int num, List<TerrainObjectTransform> transforms, List<GameObject> list)
        {
            for (int i = 0; i < num; i++)
            {
                if (transforms.Count > 0)
                {
                    GameObject objectToActivate = ActivateObject(transforms[i]);
                    list.Add(objectToActivate);
                }
            }
        }

        //Coroutine responsible for activating over time with transform data
        public IEnumerator ActivateObjects(int num, List<TerrainObjectTransform> transforms, float time, List<GameObject> list)
        {
            for (int i = 0; i < num; i++)
            {
                if (transforms.Count > 0 && transforms.Count > i)
                {
                    GameObject objectToActivate = ActivateObject(transforms[i]);
                    list.Add(objectToActivate);

                    num++;
                    if ((int)(num % (transforms.Count / time)) == 0)
                    {
                        yield return null;
                    }
                }
            }
        }

        //Coroutine responsible for deactivating over time
        public IEnumerator DeactivateObjects(List<GameObject> objects, float time)
        {
            int num = 0;
            foreach (GameObject obj in objects.ToArray())
            {
                if (objects.Count > 0)
                {
                    DeactivateObject(obj);

                    num++;
                    if ((int)(num % (objects.Count / time)) == 0)
                    {
                        yield return null;
                    }
                }
            }
        }

        //Deactives multiple objects at once
        public void DeactivateObjects(List<GameObject> objects)
        {
            foreach (GameObject obj in objects.ToArray())
            {
                if (objects.Count > 0)
                {
                    DeactivateObject(obj);
                }
            }
        }
    }
}