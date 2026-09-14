using UnityEngine;
using System.Collections;

public class ShieldBreakEffectController : MonoBehaviour
{
    private ParticleSystem particles;
    private PooledObject pooledObject;
    private Coroutine returnCoroutine;

    private void Awake()
    {
        particles = GetComponent<ParticleSystem>();
        pooledObject = GetComponent<PooledObject>();
    }

    public void Play(Vector3 position)
    {
        transform.position = position;

        if (returnCoroutine != null) StopCoroutine(returnCoroutine);

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
