using System.Collections;
using UnityEngine;

public class HitEffectController : MonoBehaviour
{
    private ParticleSystem particles;
    private PooledObject pooledObject;
    private Coroutine returnCoroutine;

    private void Awake()
    {
        particles = GetComponent<ParticleSystem>();
        pooledObject = GetComponent<PooledObject>();
    }

    private void OnEnable()
    {
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
        }

        particles.Clear();
        particles.Play();

        returnCoroutine = StartCoroutine(ReturnAfterPlay());
    }

    private void OnDisable()
    {
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }
    }

    private IEnumerator ReturnAfterPlay()
    {
        yield return new WaitUntil(() => !particles.IsAlive(true));

        returnCoroutine = null;
        pooledObject.ReturnToPool();
    }
}