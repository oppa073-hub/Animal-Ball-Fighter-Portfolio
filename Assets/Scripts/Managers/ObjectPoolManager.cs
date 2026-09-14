using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; }

    [System.Serializable]
    public class PoolData
    {
        public GameObject prefab;
        public int size = 10;
    }

    [SerializeField] private PoolData[] pools;

    // GameObject 참조 대신 프리팹 이름을 키로 사용
    private readonly Dictionary<string, Queue<GameObject>> poolDictionary = new Dictionary<string, Queue<GameObject>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RegisterInitialPools();
    }

    private void RegisterInitialPools()
    {
        if (pools == null) return;

        for (int i = 0; i < pools.Length; i++)
        {
            RegisterPool(pools[i].prefab, pools[i].size);
        }
    }

    private string GetPoolKey(GameObject prefab)
    {
        return prefab == null ? null : prefab.name;
    }

    public void RegisterPool(GameObject prefab, int size)
    {
        string key = GetPoolKey(prefab);

        if (string.IsNullOrEmpty(key)) return;
        if (poolDictionary.ContainsKey(key)) return;

        Queue<GameObject> queue = new Queue<GameObject>();

        for (int i = 0; i < size; i++)
        {
            GameObject obj = Instantiate(prefab, transform);

            PooledObject pooledObject = obj.GetComponent<PooledObject>();

            if (pooledObject == null)
            {
                pooledObject = obj.AddComponent<PooledObject>();
            }

            pooledObject.OriginPrefab = prefab;

            obj.SetActive(false);
            queue.Enqueue(obj);
        }

        poolDictionary.Add(key, queue);

        Debug.Log($"[Pool] 등록 완료: {key} / {size}개");
    }

    public GameObject GetObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        string key = GetPoolKey(prefab);

        if (string.IsNullOrEmpty(key)) return null;

        if (!poolDictionary.TryGetValue(key, out Queue<GameObject> queue))
        {
            Debug.LogWarning($"[Pool] 등록되지 않은 프리팹: {key}");
            return null;
        }

        if (queue.Count == 0)
        {
            Debug.LogWarning($"[Pool] 수량 부족: {key}");
            return null;
        }

        GameObject obj = queue.Dequeue();

        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);

        return obj;
    }

    public void ReturnObject(GameObject obj, GameObject prefab)
    {
        if (obj == null) return;

        string key = GetPoolKey(prefab);

        obj.SetActive(false);
        obj.transform.SetParent(transform);

        if (string.IsNullOrEmpty(key) || !poolDictionary.TryGetValue(key, out Queue<GameObject> queue))
        {
            Debug.LogWarning($"[Pool] 반환할 풀을 찾지 못함: {key}");
            Destroy(obj);
            return;
        }

        queue.Enqueue(obj);
    }
}