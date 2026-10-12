using System.Collections;
using UnityEngine;

[System.Serializable]
public class VFXPoolData
{
    public GameObject prefab;
    public int size = 10;
}

public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    [SerializeField] private VFXPoolData[] vfxPools;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        RegisterVFXPools();
    }

    public GameObject Play(GameObject vfxPrefab, Vector3 position, Quaternion rotation, float duration)
    {
        if (vfxPrefab == null) return null;

        GameObject vfx = ObjectPoolManager.Instance.GetObject(vfxPrefab, position, rotation);

        if (vfx == null)
        {
            Debug.LogWarning($"[VFX] 풀에서 가져오지 못함 : {vfxPrefab.name}");
            return null;
        }

        StartCoroutine(ReturnAfterDuration(vfx, duration));

        return vfx;
    }

    private IEnumerator ReturnAfterDuration(GameObject vfx, float duration)
    {
        yield return new WaitForSeconds(duration);

        if (vfx != null && vfx.activeSelf)
        {
            PooledObject pooledObject = vfx.GetComponent<PooledObject>();

            if (pooledObject != null) pooledObject.ReturnToPool();
        }
    }

    public GameObject PlayAttached(GameObject vfxPrefab, Transform parent, Vector3 localPosition, Quaternion localRotation, float duration)
    {
        if (vfxPrefab == null || parent == null) return null;

        GameObject vfx = ObjectPoolManager.Instance.GetObject( vfxPrefab, parent.position, parent.rotation);

        if (vfx == null) return null;

        vfx.transform.SetParent(parent);
        vfx.transform.localPosition = localPosition;
        vfx.transform.localRotation = localRotation;

        StartCoroutine(ReturnAttachedAfterDuration(vfx, duration));

        return vfx;
    }

    private IEnumerator ReturnAttachedAfterDuration(GameObject vfx, float duration)
    {
        yield return new WaitForSeconds(duration);

        if (vfx != null && vfx.activeSelf)
        {
            vfx.transform.SetParent(null);

            PooledObject pooledObject = vfx.GetComponent<PooledObject>();

            if (pooledObject != null) pooledObject.ReturnToPool();
        }
    }

    private void RegisterVFXPools()
    {
        for (int i = 0; i < vfxPools.Length; i++)
        {
            if (vfxPools[i].prefab == null) continue;

            ObjectPoolManager.Instance.RegisterPool(vfxPools[i].prefab, vfxPools[i].size);
        }
    }
}