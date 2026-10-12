using System;
using System.Collections;
using UnityEngine;

public class PoisonTrap : MonoBehaviour
{
    [SerializeField] private StatusEffectData poisonData;

    private PooledObject pooledObject;
    private Coroutine lifeCoroutine;

    private Action<PoisonTrap> returnedCallback;

    private void Awake()
    {
        pooledObject = GetComponent<PooledObject>();
    }

    private void OnDisable()
    {
        if (lifeCoroutine != null)
        {
            StopCoroutine(lifeCoroutine);
            lifeCoroutine = null;
        }

        returnedCallback = null;
    }

    public void Activate(float lifeTime, Action<PoisonTrap> onReturned = null)
    {
        if (pooledObject == null) pooledObject = GetComponent<PooledObject>();

        if (lifeCoroutine != null) StopCoroutine(lifeCoroutine);

        returnedCallback = onReturned;
        lifeCoroutine = StartCoroutine(LifeRoutine(lifeTime));
    }

    private IEnumerator LifeRoutine(float lifeTime)
    {
        yield return new WaitForSeconds(lifeTime);

        ReturnToPool();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.currentState != GameState.Playing)
        {
            return;
        }

        if (!other.CompareTag("Player")) return;

        StatusEffectReceiver receiver = other.GetComponent<StatusEffectReceiver>();

        if (receiver == null) return;

        receiver.ApplyStatus(poisonData);

        ReturnToPool();
    }

    public void ForceReturnToPool()
    {
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (lifeCoroutine != null)
        {
            StopCoroutine(lifeCoroutine);
            lifeCoroutine = null;
        }

        Action<PoisonTrap> callback = returnedCallback;
        returnedCallback = null;

        callback?.Invoke(this);

        if (pooledObject == null) pooledObject = GetComponent<PooledObject>();
        pooledObject?.ReturnToPool();
    }
}