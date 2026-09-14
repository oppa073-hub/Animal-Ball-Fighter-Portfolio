using UnityEngine;

public class PooledObject : MonoBehaviour
{
    public GameObject OriginPrefab { get; set; }

    public bool IsInPool { get; private set; }

    public void MarkTaken()
    {
        IsInPool = false;
    }

    public void MarkReturned()
    {
        IsInPool = true;
    }

    public void ReturnToPool()
    {
        if (IsInPool) return;

        if (ObjectPoolManager.Instance == null)
        {
            Debug.LogWarning($"[Pool] ObjectPoolManager 없음: {name}");

            return;
        }

        if (OriginPrefab == null)
        {
            Debug.LogError($"[Pool] 원본 프리팹 정보 없음: {name}");

            return;
        }

        ObjectPoolManager.Instance.ReturnObject(gameObject, OriginPrefab);
    }
}