using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class MissileWarningPattern : BossPattern
{
    [SerializeField] private GameObject warningPrefab;
    [SerializeField] private int missileCountMin;
    [SerializeField] private int missileCountMax;
    [SerializeField] private float warningDuration;
    [SerializeField] private float damageRadius;
    [SerializeField] private float damage;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private StatusEffectData statusEffectOnHit;

    [Header("Warning VFX")]
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private float impactVfxDuration = 1.5f;
    [SerializeField] private Vector3 impactVfxOffset;

    [Header("Prepare VFX")]
    [SerializeField] private GameObject prepareVfxPrefab;
    [SerializeField] private Transform prepareVfxPoint;
    [SerializeField] private float prepareVfxDuration = 1f;

    private BoxCollider spawnArea;
    private Coroutine warningCoroutine;
    private List<GameObject> warningPrefabs = new List<GameObject>();
    protected override void Awake()
    {
        base.Awake();
        spawnArea = GameObject.FindWithTag("SpawnArea").GetComponent<BoxCollider>();
    }
    public override void UsePattern()
    {
        if (warningCoroutine != null) return;
        Debug.Log("MissileWarningPattern 시작");
        if (prepareVfxPrefab != null)
        {
            Transform parent = prepareVfxPoint != null ? prepareVfxPoint : transform;
            
            VFXManager.Instance.PlayAttached(prepareVfxPrefab, parent, Vector3.zero, Quaternion.identity, prepareVfxDuration);
        }
        SoundManager.Instance?.PlaySFX(SoundId.RobotMissilePrepare);

        warningCoroutine = StartCoroutine(WaitWarning(warningDuration));
    }

    private IEnumerator WaitWarning(float warningDuration)
    {
        int count = UnityEngine.Random.Range(missileCountMin, missileCountMax + 1);
        var bounds = spawnArea.bounds;
        for (int i = 0; i < count; i++)
        {
            float x = UnityEngine.Random.Range(bounds.min.x, bounds.max.x);
            float z = UnityEngine.Random.Range(bounds.min.z, bounds.max.z);

            Vector3 spawnPosition = new Vector3(
                 x,
                 0,
                 z
                );

            GameObject warning = ObjectPoolManager.Instance.GetObject(warningPrefab, spawnPosition, Quaternion.identity);

            if (warning == null) continue;

            warningPrefabs.Add(warning);

            warning.GetComponent<BossWarningIndicator>()?.Play(warningDuration);
        }

        yield return new WaitForSeconds(warningDuration);

        SoundManager.Instance?.PlaySFX(SoundId.RobotMissileImpact);

        for (int i = 0; i < warningPrefabs.Count; i++)
        {
            Vector3 impactPosition = warningPrefabs[i].transform.position + impactVfxOffset;

            VFXManager.Instance.Play(impactVfxPrefab, impactPosition, Quaternion.identity, impactVfxDuration);

            Collider[] hits = Physics.OverlapSphere(warningPrefabs[i].transform.position, damageRadius, targetLayer);

            if (hits.Length > 0)
            {
                DamageManager.Instance.ApplyFixedDamage(hits[0].gameObject, damage, DamageTextType.Normal);
                if (statusEffectOnHit != null)
                {
                    hits[0].GetComponent<StatusEffectReceiver>()?.ApplyStatus(statusEffectOnHit);
                }
            }
            warningPrefabs[i].GetComponent<PooledObject>()?.ReturnToPool();
        }

        warningPrefabs.Clear();

        warningCoroutine = null;

        bossController.EndPattern();
    }
    public override void CancelPattern()
    {
        if (warningCoroutine != null)
        {
            StopCoroutine(warningCoroutine);
            warningCoroutine = null;
        }

        for (int i = 0; i < warningPrefabs.Count; i++)
        {
            if (warningPrefabs[i] != null)
            {
                warningPrefabs[i].GetComponent<PooledObject>()?.ReturnToPool();
            }
        }

        warningPrefabs.Clear();
    }
}
