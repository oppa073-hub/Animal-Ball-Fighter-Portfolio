using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 마녀 패턴1: 독 물약 투척. 경고 표시와 함께 마녀가 물약병을 포물선으로 던지고, 병이 깨진 자리에 원형 늪 장판이 생긴다.
// 하나는 플레이어 위치, 나머지는 무작위.
public class WitchSwampPattern : BossPattern
{
    [Header("Prefabs")]
    [SerializeField] private GameObject warningPrefab;  //기존 BossWarningIndicator 재사용
    [SerializeField] private GameObject fieldPrefab;

    [Header("Field")]
    [SerializeField] private int fieldCount = 3;
    [SerializeField] private float warningDuration = 1f;
    [SerializeField] private float fieldDuration = 6f;
    [SerializeField] private float damagePerTick = 3f;
    [SerializeField] private float tickInterval = 0.5f;
    [SerializeField, Range(0.1f, 1f)] private float slowMultiplier = 0.4f;  //장판 안 이동속도 배율 (0.4 = 60% 감속)
    [SerializeField] private float fieldMinSpacing = 2.5f;

    [Header("Potion Throw")]
    [SerializeField] private Material glowMaterial;   //가산 둥근 빛 (BossGlowOrb)
    [SerializeField] private Material trailMaterial;  //가산 빔 (BossGlowBeam)
    [SerializeField] private Color potionColor = new Color(0.55f, 0.95f, 0.35f, 1f);
    [SerializeField] private float throwArcHeight = 3.5f;
    [SerializeField] private float throwInterval = 0.1f;  //병 사이 던지는 간격
    [SerializeField] private GameObject splashVfxPrefab;

    private class Potion
    {
        public Transform root;
        public TrailRenderer trail;
        public Transform glow;
    }

    private readonly List<Potion> potions = new List<Potion>();
    private Transform potionRoot;
    private MaterialPropertyBlock propertyBlock;

    private BoxCollider spawnArea;
    private Rigidbody rb;
    private Transform player;
    private EnemyBallController enemyBallController;
    private EnemyAnimationController animationController;

    private Coroutine patternCoroutine;
    private readonly List<GameObject> warnings = new List<GameObject>();
    private readonly List<WitchSwampField> activeFields = new List<WitchSwampField>();

    protected override void Awake()
    {
        base.Awake();

        GameObject spawnAreaObject = GameObject.FindWithTag("SpawnArea");
        if (spawnAreaObject != null) spawnArea = spawnAreaObject.GetComponent<BoxCollider>();

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null) player = playerObject.transform;

        rb = GetComponent<Rigidbody>();
        enemyBallController = GetComponent<EnemyBallController>();
        animationController = GetComponent<EnemyAnimationController>();

        // 보스 풀은 로딩 중에 생성되므로 여기서 등록하면 첫 패턴 때 프레임이 튀지 않는다 (이미 있으면 바로 반환)
        if (ObjectPoolManager.Instance != null)
        {
            if (fieldPrefab != null) ObjectPoolManager.Instance.RegisterPool(fieldPrefab, fieldCount * 2);
            if (warningPrefab != null) ObjectPoolManager.Instance.RegisterPool(warningPrefab, fieldCount * 2);
            if (splashVfxPrefab != null) ObjectPoolManager.Instance.EnsureCapacity(splashVfxPrefab, fieldCount * 2);
        }
    }

    private void OnDestroy()
    {
        if (potionRoot != null) Destroy(potionRoot.gameObject);
    }

    private void OnDisable()
    {
        CleanupPatternObjects();
    }

    public override void UsePattern()
    {
        if (patternCoroutine != null || spawnArea == null || fieldPrefab == null)
        {
            bossController.EndPattern();
            return;
        }

        ObjectPoolManager.Instance.RegisterPool(fieldPrefab, fieldCount * 2);  //이미 있으면 바로 반환
        if (warningPrefab != null) ObjectPoolManager.Instance.RegisterPool(warningPrefab, fieldCount * 2);

        enemyBallController?.SetPatternMoving(true);
        StopBody();

        animationController?.PlayPrepare();
        SoundManager.Instance?.PlaySFX(SoundId.WitchCast);

        patternCoroutine = StartCoroutine(PatternRoutine());
    }

    private IEnumerator PatternRoutine()
    {
        List<Vector3> positions = PickPositions();

        foreach (Vector3 position in positions)
        {
            if (warningPrefab == null) break;

            GameObject warning = ObjectPoolManager.Instance.GetObject(warningPrefab, position, Quaternion.identity);
            if (warning == null) continue;

            warning.GetComponent<BossWarningIndicator>()?.Play(warningDuration);
            warnings.Add(warning);
        }

        // 물약병을 포물선으로 던져 경고가 끝나는 순간 떨어지게
        EnsurePotions(positions.Count);
        Vector3 hand = transform.position + Vector3.up * 1.2f;
        float flightTime = Mathf.Max(0.2f, warningDuration - throwInterval * (positions.Count - 1) - 0.15f);

        float timer = 0f;
        while (timer < warningDuration)
        {
            StopBody();

            for (int i = 0; i < positions.Count; i++)
            {
                float local = timer - 0.15f - i * throwInterval;
                Potion potion = potions[i];

                if (local < 0f) continue;
                if (local > flightTime)
                {
                    if (potion.root.gameObject.activeSelf) HidePotion(potion);
                    continue;
                }

                if (!potion.root.gameObject.activeSelf) ShowPotion(potion, hand);

                float t = local / flightTime;
                Vector3 position = Vector3.Lerp(hand, positions[i], t);
                position.y = Mathf.Lerp(hand.y, positions[i].y + 0.2f, t) + throwArcHeight * 4f * t * (1f - t);
                potion.root.position = position;
                potion.root.Rotate(540f * Time.deltaTime, 0f, 360f * Time.deltaTime, Space.Self);  //빙글빙글
                if (potion.glow != null) BossGlowVisual.FaceCamera(potion.glow);
            }

            timer += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < potions.Count; i++) HidePotion(potions[i]);

        SoundManager.Instance?.PlaySFX(SoundId.HeroPoisonSpawn);

        if (splashVfxPrefab != null && VFXManager.Instance != null)
        {
            foreach (Vector3 position in positions) VFXManager.Instance.Play(splashVfxPrefab, position, Quaternion.identity, 1.2f);
        }

        ReturnWarnings();

        foreach (Vector3 position in positions)
        {
            GameObject fieldObject = ObjectPoolManager.Instance.GetObject(fieldPrefab, position, Quaternion.identity);
            if (fieldObject == null) continue;

            WitchSwampField field = fieldObject.GetComponent<WitchSwampField>();
            if (field == null)
            {
                fieldObject.GetComponent<PooledObject>()?.ReturnToPool();
                continue;
            }

            activeFields.Add(field);
            field.Activate(fieldDuration, damagePerTick, tickInterval, slowMultiplier, HandleFieldReturned);
        }

        enemyBallController?.SetPatternMoving(false);
        rb?.WakeUp();

        patternCoroutine = null;
        bossController.EndPattern();
    }

    private List<Vector3> PickPositions()
    {
        Bounds bounds = spawnArea.bounds;
        float floorY = spawnArea.transform.position.y + 0.05f;
        List<Vector3> positions = new List<Vector3>();

        // 첫 장판은 플레이어 위치 (경고 시간 동안 피할 수 있음)
        if (player != null)
        {
            positions.Add(new Vector3(
                Mathf.Clamp(player.position.x, bounds.min.x, bounds.max.x),
                floorY,
                Mathf.Clamp(player.position.z, bounds.min.z, bounds.max.z)));
        }

        int tries = 0;
        while (positions.Count < fieldCount && tries < 40)
        {
            tries++;

            Vector3 candidate = new Vector3(Random.Range(bounds.min.x, bounds.max.x), floorY, Random.Range(bounds.min.z, bounds.max.z));

            bool tooClose = false;
            foreach (Vector3 placed in positions)
            {
                if (Vector3.Distance(placed, candidate) < fieldMinSpacing) { tooClose = true; break; }
            }

            if (!tooClose) positions.Add(candidate);
        }

        return positions;
    }

    private void EnsurePotions(int count)
    {
        if (potionRoot == null)
        {
            potionRoot = new GameObject("WitchPotionVisual").transform;
            potionRoot.SetParent(transform.parent, false);
        }

        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

        while (potions.Count < count)
        {
            Potion potion = new Potion();
            GameObject root = new GameObject("WitchPotion");
            root.transform.SetParent(potionRoot, false);
            potion.root = root.transform;

            // 둥근 병 + 코르크 마개
            GameObject flask = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(flask.GetComponent<Collider>());
            flask.transform.SetParent(potion.root, false);
            flask.transform.localScale = Vector3.one * 0.38f;
            TintRenderer(flask.GetComponent<Renderer>(), potionColor);

            GameObject neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(neck.GetComponent<Collider>());
            neck.transform.SetParent(potion.root, false);
            neck.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            neck.transform.localScale = new Vector3(0.12f, 0.08f, 0.12f);
            TintRenderer(neck.GetComponent<Renderer>(), new Color(0.55f, 0.35f, 0.2f));

            if (glowMaterial != null)
            {
                potion.glow = BossGlowVisual.CreateOrb(potion.root, "Glow", glowMaterial, out Renderer glowRenderer);
                potion.glow.localScale = Vector3.one * 0.9f;
                BossGlowVisual.SetOrbColor(glowRenderer, new MaterialPropertyBlock(), new Color(potionColor.r, potionColor.g, potionColor.b, 0.6f));
                potion.glow.gameObject.SetActive(true);
            }

            if (trailMaterial != null)
            {
                TrailRenderer trail = root.AddComponent<TrailRenderer>();
                trail.sharedMaterial = trailMaterial;
                trail.time = 0.25f;
                trail.minVertexDistance = 0.08f;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(1f, 0f));
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(potionColor, 0f), new GradientColorKey(new Color(0.6f, 0.3f, 0.9f), 1f) },
                    new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = gradient;
                potion.trail = trail;
            }

            root.SetActive(false);
            potions.Add(potion);
        }
    }

    private void TintRenderer(Renderer target, Color color)
    {
        target.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        target.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", color);
        target.SetPropertyBlock(propertyBlock);
    }

    private void ShowPotion(Potion potion, Vector3 position)
    {
        potion.root.position = position;
        potion.root.gameObject.SetActive(true);
        if (potion.trail != null) potion.trail.Clear();
    }

    private void HidePotion(Potion potion)
    {
        if (potion.root == null) return;
        if (potion.trail != null) potion.trail.Clear();
        potion.root.gameObject.SetActive(false);
    }

    private void StopBody()
    {
        if (rb == null || rb.isKinematic) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void HandleFieldReturned(WitchSwampField field)
    {
        activeFields.Remove(field);
    }

    private void ReturnWarnings()
    {
        foreach (GameObject warning in warnings)
        {
            if (warning != null && warning.activeSelf) warning.GetComponent<PooledObject>()?.ReturnToPool();
        }

        warnings.Clear();
    }

    private void CleanupPatternObjects()
    {
        if (patternCoroutine != null)
        {
            StopCoroutine(patternCoroutine);
            patternCoroutine = null;
        }

        ReturnWarnings();

        for (int i = 0; i < potions.Count; i++) HidePotion(potions[i]);

        while (activeFields.Count > 0)
        {
            int last = activeFields.Count - 1;
            WitchSwampField field = activeFields[last];
            activeFields.RemoveAt(last);

            if (field != null && field.gameObject.activeSelf) field.ForceReturnToPool();
        }
    }

    public override void CancelPattern()
    {
        CleanupPatternObjects();
        StopBody();
        enemyBallController?.SetPatternMoving(false);
    }
}
