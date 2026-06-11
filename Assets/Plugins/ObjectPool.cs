using System;
using System.Collections.Generic;
using UnityCommunity.UnitySingleton;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Lyf.ObjectPool
{
    public class ObjectPool : Singleton<ObjectPool>
    {
        private static int _initialPoolCount = 10;
        private static int _addCount = 5;

        private readonly Dictionary<string, ObjectPoolData> _objectPoolDataDic = new();
        private GameObject _parentPool;

        public void SetInitialPoolCount(int count) => _initialPoolCount = count;
        public void SetAddCount(int count) => _addCount = count;

        /// <summary>
        /// 取对象
        /// </summary>
        public GameObject Allocate(GameObject prefab, Transform parent = null, Action<GameObject> callback = null)
        {
            var prefabName = prefab.name;

            if (!_objectPoolDataDic.TryGetValue(prefabName, out var poolData))
            {
                poolData = InitializePool(prefab);
            }

            if (poolData.AvailableObjects.Count == 0)
            {
                ExpandPool(prefab);
            }

            var obj = poolData.AvailableObjects.Dequeue();

            if (parent != null)
                obj.transform.SetParent(parent, false);

            obj.SetActive(true);

            callback?.Invoke(obj);
            return obj;
        }

        /// <summary>
        /// 回收对象
        /// </summary>
        public void Recycle(GameObject obj)
        {
            var prefabName = obj.name;

            if (!_objectPoolDataDic.TryGetValue(prefabName, out var poolData))
            {
                Debug.LogError($"未找到 {prefabName} 的对象池数据");
                return;
            }

            obj.SetActive(false);
            poolData.AvailableObjects.Enqueue(obj);
        }

        public void ExpandPool(GameObject prefab)
        {
            var prefabName = prefab.name;

            if (!_objectPoolDataDic.TryGetValue(prefabName, out var poolData))
                return;

            var parent = _parentPool.transform.Find(prefabName + "Pool");

            for (var i = 0; i < _addCount; i++)
            {
                var obj = Object.Instantiate(prefab, parent, false);
                obj.name = prefab.name;

                poolData.AddObject(obj);
                obj.SetActive(false);
            }
        }

        private ObjectPoolData InitializePool(GameObject prefab)
        {
            if (!_parentPool)
            {
                _parentPool = new GameObject("ParentPool");
                Object.DontDestroyOnLoad(_parentPool);
            }

            var prefabName = prefab.name;
            var rootObj = new GameObject(prefabName + "Pool");
            rootObj.transform.SetParent(_parentPool.transform);

            var poolData = new ObjectPoolData();
            _objectPoolDataDic[prefabName] = poolData;

            for (var i = 0; i < _initialPoolCount; i++)
            {
                var obj = Object.Instantiate(prefab, rootObj.transform, false);
                obj.name = prefab.name;

                poolData.AddObject(obj);
                obj.SetActive(false);
            }

            return poolData;
        }

        private class ObjectPoolData
        {
            public Queue<GameObject> AvailableObjects { get; } = new();
            public List<GameObject> AllObjects { get; } = new();

            public void AddObject(GameObject obj)
            {
                AllObjects.Add(obj);
                AvailableObjects.Enqueue(obj);
            }
        }
    }
}