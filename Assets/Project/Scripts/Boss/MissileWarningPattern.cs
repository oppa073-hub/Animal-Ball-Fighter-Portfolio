using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// 로봇 보스 패턴2: 미사일 폭격.
// 몸(어깨)에서 미사일이 연기 꼬리를 남기며 하늘로 솟구쳐 화면 밖으로 사라지고, 바닥 경고 원이 차오른 뒤
// 같은 미사일이 위에서 경고 원으로 내리꽂혀 폭발한다 (범위 안이면 피해 + 화상).
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

    [Header("Missile Flight")]
    [SerializeField] private Material trailMaterial;  //가산 빔 (BossGlowBeam)
    [SerializeField] private Material glowMaterial;   //가산 둥근 빛 (BossGlowOrb)
    [SerializeField] private Color missileColor = new Color(0.85f, 0.85f, 0.9f, 1f);
    [SerializeField] private Color flameColor = new Color(1f, 0.55f, 0.15f, 1f);
    [SerializeField] private float launchHeight = 13f;  //화면 밖까지 올라가는 높이
    [SerializeField] private float launchDuration = 0.5f;
    [SerializeField] private float launchInterval = 0.08f;  //미사일 사이 발사 간격
    [SerializeField] private float fallDuration = 0.3f;
    [SerializeField] private float bodyLaunchHeight = 1.3f;  //몸에서 나오는 높이

    private const float MissileLength = 0.9f;

    private class Missile
    {
        public Transform root;
        public TrailRenderer trail;
        public Transform flame;
    }

    private BoxCollider spawnArea;
    private Coroutine warningCoroutine;
    private List<GameObject> warningPrefabs = new List<GameObject>();
    private readonly List<Missile> missiles = new List<Missile>();
    private Transform visualRoot;
    private MaterialPropertyBlock propertyBlock;

    protected override void Awake()
    {
        base.Awake();
        spawnArea = GameObject.FindWithTag("SpawnArea").GetComponent<BoxCollider>();
        propertyBlock = new MaterialPropertyBlock();
    }

    private void OnDestroy()
    {
        if (visualRoot != null) Destroy(visualRoot.gameObject);
    }

    public override void UsePattern()
    {
        if (warningCoroutine != null) return;

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
        int count = Random.Range(missileCountMin, missileCountMax + 1);
        var bounds = spawnArea.bounds;
        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(bounds.min.x, bounds.max.x);
            float z = Random.Range(bounds.min.z, bounds.max.z);

            Vector3 spawnPosition = new Vector3(x, 0, z);

            GameObject warning = ObjectPoolManager.Instance.GetObject(warningPrefab, spawnPosition, Quaternion.identity);

            if (warning == null) continue;

            warningPrefabs.Add(warning);

            warning.GetComponent<BossWarningIndicator>()?.Play(warningDuration);
        }

        // 1) 몸에서 하늘로 발사
        EnsureMissiles(warningPrefabs.Count);
        float launchTotal = launchDuration + launchInterval * Mathf.Max(0, warningPrefabs.Count - 1);
        float elapsed = 0f;
        Vector3 launchOrigin = transform.position;
        bool[] launched = new bool[warningPrefabs.Count];

        while (elapsed < launchTotal)
        {
            launchOrigin = transform.position;  //보스가 움직여도 몸에서 나오도록

            for (int i = 0; i < warningPrefabs.Count; i++)
            {
                float local = elapsed - i * launchInterval;
                if (local < 0f) continue;

                if (!launched[i])
                {
                    launched[i] = true;
                    if (CombatFeedbackManager.Instance != null) CombatFeedbackManager.Instance.RequestSfx(SoundId.EnemyShoot, priority: 1);
                    ShowMissile(missiles[i], LaunchPoint(launchOrigin, i), Vector3.up);
                }

                float t = Mathf.Clamp01(local / launchDuration);
                if (t >= 1f)
                {
                    HideMissile(missiles[i]);
                    continue;
                }

                // 처음엔 느리게 나오다 급가속, 살짝 바깥으로 휘어 올라감
                Vector3 start = LaunchPoint(launchOrigin, i);
                Vector3 side = SideDirection(i) * (1.2f * t);
                Vector3 position = start + side + Vector3.up * (launchHeight * t * t);
                Vector3 velocity = SideDirection(i) * 1.2f + Vector3.up * (2f * launchHeight * Mathf.Max(0.05f, t));
                MoveMissile(missiles[i], position, velocity);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < missiles.Count; i++) HideMissile(missiles[i]);

        // 2) 경고 원이 차오르는 동안 대기 → 마지막에 위에서 내리꽂힘
        float waitBeforeFall = warningDuration - launchTotal - fallDuration;
        if (waitBeforeFall > 0f) yield return new WaitForSeconds(waitBeforeFall);

        float fallTimer = 0f;
        for (int i = 0; i < warningPrefabs.Count; i++)
        {
            Vector3 target = warningPrefabs[i].transform.position;
            ShowMissile(missiles[i], target + Vector3.up * launchHeight, Vector3.down);
        }

        while (fallTimer < fallDuration)
        {
            float t = fallTimer / fallDuration;

            for (int i = 0; i < warningPrefabs.Count; i++)
            {
                Vector3 target = warningPrefabs[i].transform.position;
                Vector3 position = target + Vector3.up * (launchHeight * (1f - t * t) + MissileLength * 0.5f);
                MoveMissile(missiles[i], position, Vector3.down);
            }

            fallTimer += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < missiles.Count; i++) HideMissile(missiles[i]);

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

    private Vector3 LaunchPoint(Vector3 origin, int index)  //양 어깨에서 번갈아
    {
        return origin + Vector3.up * bodyLaunchHeight + SideDirection(index) * 0.35f;
    }

    private Vector3 SideDirection(int index)
    {
        Vector3 right = transform.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.001f) right = Vector3.right;
        right.Normalize();

        float sign = index % 2 == 0 ? -1f : 1f;
        float spread = 1f + (index / 2) * 0.5f;
        return right * sign * spread;
    }

    private void EnsureMissiles(int count)
    {
        if (visualRoot == null)
        {
            // 보스가 움직이거나 회전해도 영향을 받지 않게 보스 밖에 둔다
            visualRoot = new GameObject("RobotMissileVisual").transform;
            visualRoot.SetParent(transform.parent, false);
        }

        while (missiles.Count < count)
        {
            missiles.Add(CreateMissile());
        }
    }

    private Missile CreateMissile()
    {
        Missile missile = new Missile();

        GameObject root = new GameObject("BossMissile");
        root.transform.SetParent(visualRoot, false);
        missile.root = root.transform;

        // 몸통 (기본 머티리얼 캡슐을 길쭉하게, 앞쪽이 +Z)
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(missile.root, false);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.transform.localScale = new Vector3(0.28f, MissileLength * 0.5f, 0.28f);
        Renderer bodyRenderer = body.GetComponent<Renderer>();
        bodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bodyRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", missileColor);
        bodyRenderer.SetPropertyBlock(propertyBlock);

        // 꼬리 불꽃
        if (glowMaterial != null)
        {
            missile.flame = BossGlowVisual.CreateOrb(missile.root, "Flame", glowMaterial, out Renderer flameRenderer);
            missile.flame.localPosition = new Vector3(0f, 0f, -MissileLength * 0.55f);
            missile.flame.localScale = Vector3.one * 0.7f;
            MaterialPropertyBlock flameBlock = new MaterialPropertyBlock();
            BossGlowVisual.SetOrbColor(flameRenderer, flameBlock, flameColor);
            missile.flame.gameObject.SetActive(true);
        }

        // 연기/불꽃 꼬리
        if (trailMaterial != null)
        {
            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMaterial;
            trail.time = 0.3f;
            trail.minVertexDistance = 0.1f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(1f, 0f));
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.9f, 0.6f), 0f), new GradientColorKey(flameColor, 0.3f), new GradientColorKey(new Color(0.5f, 0.5f, 0.5f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.3f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            missile.trail = trail;
        }

        root.SetActive(false);
        return missile;
    }

    private void ShowMissile(Missile missile, Vector3 position, Vector3 forward)
    {
        missile.root.position = position;
        missile.root.rotation = Quaternion.LookRotation(forward);
        missile.root.gameObject.SetActive(true);
        if (missile.trail != null) missile.trail.Clear();
    }

    private void MoveMissile(Missile missile, Vector3 position, Vector3 velocity)
    {
        missile.root.position = position;
        if (velocity.sqrMagnitude > 0.0001f) missile.root.rotation = Quaternion.LookRotation(velocity);
        if (missile.flame != null) BossGlowVisual.FaceCamera(missile.flame);
    }

    private void HideMissile(Missile missile)
    {
        if (missile.trail != null) missile.trail.Clear();
        missile.root.gameObject.SetActive(false);
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

        for (int i = 0; i < missiles.Count; i++)
        {
            if (missiles[i].root != null) HideMissile(missiles[i]);
        }
    }
}
