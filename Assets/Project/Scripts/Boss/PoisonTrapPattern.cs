using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PoisonTrapPattern : BossPattern
{
    [Header("Trap")]
    [SerializeField] private GameObject warningPrefab;
    [SerializeField] private GameObject trapPrefab;

    [SerializeField] private int trapCount = 3;
    [SerializeField] private float warningDuration = 1f;
    [SerializeField] private float trapDuration = 6f;

    private BoxCollider spawnArea;
    private Rigidbody rb;
    private EnemyBallController enemyBallController;
    private EnemyAnimationController animationController;

    private Coroutine trapCoroutine;

    private readonly List<GameObject> warnings = new List<GameObject>();
    private readonly List<PoisonTrap> activeTraps = new List<PoisonTrap>();

    protected override void Awake()
    {
        base.Awake();

        spawnArea = GameObject.FindWithTag("SpawnArea").GetComponent<BoxCollider>();

        rb = GetComponent<Rigidbody>();

        enemyBallController = GetComponent<EnemyBallController>();
        animationController = GetComponent<EnemyAnimationController>();
    }
    private void OnDisable()
    {
        CleanupPatternObjects();
    }

    public override void UsePattern()
    {
        if (trapCoroutine != null) return;

        enemyBallController.SetPatternMoving(true);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        animationController?.PlayPrepare();

        SoundManager.Instance?.PlaySFX(SoundId.HeroPoisonPrepare);

        trapCoroutine = StartCoroutine(TrapRoutine());
    }

    private IEnumerator TrapRoutine()
    {
        Bounds bounds = spawnArea.bounds;

        for (int i = 0; i < trapCount; i++)
        {
            Vector3 position = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                0.05f,
                Random.Range(bounds.min.z, bounds.max.z)
            );

            GameObject warning = ObjectPoolManager.Instance.GetObject( warningPrefab, position, Quaternion.identity);

            if (warning == null) continue;

            warning.GetComponent<BossWarningIndicator>()?.Play(warningDuration);

            warnings.Add(warning);
        }

        float timer = 0f;

        while (timer < warningDuration)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            timer += Time.deltaTime;
            yield return null;
        }

        SoundManager.Instance?.PlaySFX(SoundId.HeroPoisonSpawn);

        SpawnTraps();

        enemyBallController.SetPatternMoving(false);

        rb.WakeUp();

        trapCoroutine = null;

        bossController.EndPattern();
    }
    private void SpawnTraps()
    {
        for (int i = 0; i < warnings.Count; i++)
        {
            GameObject warning = warnings[i];

            if (warning == null) continue;

            Vector3 position = warning.transform.position;

            GameObject trapObject = ObjectPoolManager.Instance.GetObject(trapPrefab, position, Quaternion.identity);

            if (trapObject != null)
            {
                PoisonTrap trap = trapObject.GetComponent<PoisonTrap>();

                if (trap != null)
                {
                    activeTraps.Add(trap);

                    trap.Activate(trapDuration, HandleTrapReturned);
                }
                else
                {
                    Debug.LogError(
                        "[PoisonTrapPattern] " +
                        "PoisonTrap 컴포넌트가 없습니다."
                    );

                    trapObject.GetComponent<PooledObject>() ?.ReturnToPool();
                }
            }

            warning.GetComponent<PooledObject>() ?.ReturnToPool();
        }

        warnings.Clear();
    }
    private void HandleTrapReturned(PoisonTrap trap)
    {
        activeTraps.Remove(trap);
    }

    private void CleanupPatternObjects()
    {
        if (trapCoroutine != null)
        {
            StopCoroutine(trapCoroutine);
            trapCoroutine = null;
        }

        for (int i = 0; i < warnings.Count; i++)
        {
            if (warnings[i] != null && warnings[i].activeSelf)
            {
                warnings[i].GetComponent<PooledObject>()?.ReturnToPool();
            }
        }

        warnings.Clear();

        while (activeTraps.Count > 0)
        {
            int lastIndex = activeTraps.Count - 1;

            PoisonTrap trap = activeTraps[lastIndex];

            activeTraps.RemoveAt(lastIndex);

            if (trap != null && trap.gameObject.activeSelf)
            {
                trap.ForceReturnToPool();
            }
        }

        //activeTraps.Clear();
    }

    public override void CancelPattern()
    {
        CleanupPatternObjects();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (enemyBallController != null)
        {
            enemyBallController.SetPatternMoving(false);
        }
    }
}
