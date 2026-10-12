using UnityEngine;
using System.Collections;

// 히어로 보스 패턴2: 히어로 착지 (도약 내려찍기).
// 웅크림 → 높이 도약 → 바닥 표식이 플레이어를 쫓아감 → 표식 고정(깜빡임) → 내려찍기 →
// 범위 안이면 피해 + 바깥으로 튕겨냄, 황금빛 충격파 고리와 화면 흔들림. 분노(2페이즈)에서는 두 번 연속으로 뛴다.
public class HeroLeapSlamPattern : BossPattern
{
    [Header("Leap")]
    [SerializeField] private float crouchDuration = 0.45f;
    [SerializeField] private float jumpHeight = 4.5f;
    [SerializeField] private float riseDuration = 0.35f;
    [SerializeField] private float trackDuration = 0.9f;  //공중에서 표식이 플레이어를 따라가는 시간
    [SerializeField] private float markerFollowSpeed = 9f;  //표식 추적 속도 (플레이어보다 느려서 피할 수 있음)
    [SerializeField] private float lockDuration = 0.4f;  //표식 고정 후 내려찍기까지
    [SerializeField] private float fallDuration = 0.16f;
    [SerializeField] private float recoverDuration = 1.2f;  //착지 후 경직 (반격 타이밍)
    [SerializeField] private int rageSlamCount = 1;

    [Header("Impact")]
    [SerializeField] private float slamRadius = 4.2f;
    [SerializeField] private float damage = 15f;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private float knockbackMultiplier = 2.2f;  //플레이어 이동속도 배율
    [SerializeField] private float knockbackDuration = 0.35f;
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private float impactShakeDuration = 0.3f;
    [SerializeField] private float impactShakeStrength = 0.55f;

    [Header("Visual")]
    [SerializeField] private Material glowMaterial;  //가산 빔 (BossGlowBeam)
    [SerializeField] private Color markerColor = new Color(1f, 0.25f, 0.15f, 0.8f);
    [SerializeField] private Color markerLockColor = new Color(1f, 0.9f, 0.6f, 1f);
    [SerializeField] private Color shockwaveColor = new Color(1f, 0.8f, 0.3f, 1f);

    private const float GroundY = 0.06f;
    private const float ShockwaveDuration = 0.4f;

    private Transform player;
    private Rigidbody rb;
    private Collider bodyCollider;
    private EnemyBallController enemyBallController;
    private EnemyAnimationController animationController;
    private MovementFacingController facingController;
    private Transform visualBody;
    private Vector3 visualBaseScale = Vector3.one;

    private Transform visualRoot;
    private LineRenderer markerRing;
    private LineRenderer markerFill;
    private LineRenderer shockwaveRing;
    private LineRenderer dropLine;  //공중의 보스와 바닥 표식을 잇는 빛줄기 (카메라가 기울어 있어 둘이 떨어져 보이는 것 보완)

    private Coroutine slamCoroutine;
    private bool isAirborne;
    private float groundY;

    protected override void Awake()
    {
        base.Awake();

        player = GameObject.FindWithTag("Player").transform;
        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
        enemyBallController = GetComponent<EnemyBallController>();
        animationController = GetComponent<EnemyAnimationController>();
        facingController = GetComponent<MovementFacingController>();

        Animator animator = GetComponentInChildren<Animator>(true);
        visualBody = animator != null ? animator.transform : null;
        if (visualBody != null) visualBaseScale = visualBody.localScale;

        visualRoot = new GameObject("HeroLeapSlamVisual").transform;
        visualRoot.SetParent(transform.parent, false);
        markerRing = BossGlowVisual.CreateRing(visualRoot, "SlamMarker", glowMaterial);
        markerFill = BossGlowVisual.CreateRing(visualRoot, "SlamMarkerFill", glowMaterial);
        shockwaveRing = BossGlowVisual.CreateRing(visualRoot, "SlamShockwave", glowMaterial);
        dropLine = BossGlowVisual.CreateLine(visualRoot, "SlamDropLine", glowMaterial, 2);

        if (impactVfxPrefab != null && ObjectPoolManager.Instance != null) ObjectPoolManager.Instance.EnsureCapacity(impactVfxPrefab, 3);
    }

    private void OnDestroy()
    {
        if (visualRoot != null) Destroy(visualRoot.gameObject);
    }

    private void OnDisable()
    {
        slamCoroutine = null;  //비활성화로 코루틴이 멈추므로 풀에서 다시 나왔을 때 패턴이 막히지 않게
        RestoreBody();
        HideVisuals();
    }

    public override void UsePattern()
    {
        if (slamCoroutine != null) return;

        enemyBallController.SetPatternMoving(true);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        slamCoroutine = StartCoroutine(SlamRoutine());
    }

    private IEnumerator SlamRoutine()
    {
        int slamCount = bossController != null && bossController.CurrentPhase >= 2 ? Mathf.Max(1, rageSlamCount) : 1;

        for (int i = 0; i < slamCount; i++)
        {
            yield return LeapOnce(i == 0 ? crouchDuration : crouchDuration * 0.5f);
        }

        HideVisuals();
        facingController?.ClearFacingOverride();
        enemyBallController.SetPatternMoving(false);
        rb.WakeUp();

        // 쿨타임은 착지가 끝난 뒤부터 센다 (공중에 있는 동안은 맞힐 수 없으므로 연달아 쓰지 않게)
        RecordUseTime();

        slamCoroutine = null;
        bossController.EndPattern();
    }

    private IEnumerator LeapOnce(float crouchTime)
    {
        groundY = transform.position.y;

        // 1) 웅크림
        animationController?.PlayPrepare();
        SoundManager.Instance?.PlaySFX(SoundId.HeroPoisonPrepare);

        float timer = 0f;
        while (timer < crouchTime)
        {
            rb.linearVelocity = Vector3.zero;
            FacePlayer();
            float t = timer / crouchTime;
            SetSquash(1f - 0.2f * Mathf.Sin(t * Mathf.PI * 0.5f));  //점점 낮게
            timer += Time.deltaTime;
            yield return null;
        }

        // 2) 도약 (물리/충돌을 끄고 직접 이동)
        isAirborne = true;
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;
        if (bodyCollider != null) bodyCollider.enabled = false;

        Vector3 marker = new Vector3(player.position.x, GroundY, player.position.z);
        Vector3 takeoff = transform.position;

        timer = 0f;
        while (timer < riseDuration)
        {
            float t = timer / riseDuration;
            float ease = 1f - (1f - t) * (1f - t);
            SetSquash(Mathf.Lerp(1.25f, 1f, t));  //위로 길쭉하게 뛰어오름

            marker = MoveMarker(marker);
            Vector3 position = Vector3.Lerp(takeoff, new Vector3(marker.x, groundY, marker.z), ease * 0.5f);
            position.y = groundY + jumpHeight * ease;
            transform.position = position;

            UpdateMarker(marker, ease * 0.3f, false);

            timer += Time.deltaTime;
            yield return null;
        }

        // 3) 공중에서 표식이 플레이어를 쫓아감
        timer = 0f;
        while (timer < trackDuration)
        {
            marker = MoveMarker(marker);
            Vector3 hover = Vector3.Lerp(transform.position, new Vector3(marker.x, groundY + jumpHeight, marker.z), 8f * Time.deltaTime);
            transform.position = hover;
            FacePlayer();

            UpdateMarker(marker, 0.3f + 0.5f * (timer / trackDuration), false);

            timer += Time.deltaTime;
            yield return null;
        }

        // 4) 표식 고정 (깜빡임)
        timer = 0f;
        while (timer < lockDuration)
        {
            transform.position = Vector3.Lerp(transform.position, new Vector3(marker.x, groundY + jumpHeight, marker.z), 10f * Time.deltaTime);
            UpdateMarker(marker, 0.8f + 0.2f * (timer / lockDuration), true);

            timer += Time.deltaTime;
            yield return null;
        }

        // 5) 내려찍기
        Vector3 fallStart = transform.position;
        Vector3 landing = new Vector3(marker.x, groundY, marker.z);

        timer = 0f;
        while (timer < fallDuration)
        {
            float t = timer / fallDuration;
            transform.position = Vector3.Lerp(fallStart, landing, t * t);
            SetSquash(Mathf.Lerp(1f, 1.3f, t));
            UpdateMarker(marker, 1f, true);

            timer += Time.deltaTime;
            yield return null;
        }

        transform.position = landing;
        RestoreBody();
        HideMarker();

        Impact(landing);

        // 6) 착지 반동 후 경직 (제자리에 멈춰 있어 플레이어가 때릴 수 있는 반격 타이밍)
        timer = 0f;
        while (timer < recoverDuration)
        {
            rb.linearVelocity = Vector3.zero;
            float t = Mathf.Clamp01(timer / 0.4f);  //찌그러짐 반동은 처음 0.4초 동안만
            SetSquash(t < 0.3f ? Mathf.Lerp(0.7f, 1.08f, t / 0.3f) : Mathf.Lerp(1.08f, 1f, (t - 0.3f) / 0.7f));

            timer += Time.deltaTime;
            yield return null;
        }

        SetSquash(1f);
    }

    private Vector3 MoveMarker(Vector3 marker)
    {
        Vector3 target = new Vector3(player.position.x, GroundY, player.position.z);
        return Vector3.MoveTowards(marker, target, markerFollowSpeed * Time.deltaTime);
    }

    private void FacePlayer()
    {
        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f) facingController?.SetFacingOverride(direction);
    }

    private void UpdateMarker(Vector3 center, float fill, bool locked)
    {
        if (markerRing == null) return;

        markerRing.enabled = true;
        markerFill.enabled = true;

        BossGlowVisual.SetRing(markerRing, center, slamRadius);
        BossGlowVisual.SetRing(markerFill, center, Mathf.Max(0.05f, slamRadius * Mathf.Clamp01(fill)));  //안쪽 고리가 커지면 곧 떨어짐

        bool flash = locked && Mathf.Repeat(Time.time * 14f, 1f) < 0.5f;
        Color ring = flash ? markerLockColor : markerColor;

        dropLine.enabled = true;
        dropLine.SetPosition(0, transform.position);
        dropLine.SetPosition(1, center);
        Color drop = ring;
        drop.a *= locked ? 0.6f : 0.3f;
        BossGlowVisual.SetColor(dropLine, drop);
        BossGlowVisual.SetWidth(dropLine, locked ? 0.35f : 0.2f);
        BossGlowVisual.SetColor(markerRing, ring);
        BossGlowVisual.SetWidth(markerRing, locked ? 0.28f : 0.2f);

        Color inner = markerColor;
        inner.a *= 0.55f;
        BossGlowVisual.SetColor(markerFill, inner);
        BossGlowVisual.SetWidth(markerFill, 0.14f);
    }

    private void Impact(Vector3 landing)
    {
        if (CombatFeedbackManager.Instance != null)
        {
            CombatFeedbackManager.Instance.RequestImpact(SoundId.RobotMissileImpact, impactShakeDuration, impactShakeStrength, priority: 6);
        }
        else
        {
            SoundManager.Instance?.PlaySFX(SoundId.RobotMissileImpact);
        }

        if (impactVfxPrefab != null && VFXManager.Instance != null)
        {
            VFXManager.Instance.Play(impactVfxPrefab, new Vector3(landing.x, GroundY, landing.z), Quaternion.identity, 1.2f);
        }

        StartCoroutine(ShockwaveRoutine(new Vector3(landing.x, GroundY, landing.z)));

        Collider[] hits = Physics.OverlapSphere(new Vector3(landing.x, player.position.y, landing.z), slamRadius, targetLayer);
        if (hits.Length == 0 || DamageManager.Instance == null) return;

        GameObject target = hits[0].gameObject;
        DamageManager.Instance.ApplyFixedDamage(target, damage, DamageTextType.Normal);

        // 충격파에 밀려 바깥으로 튕겨나감
        PlayerBallController playerController = target.GetComponent<PlayerBallController>();
        if (playerController != null)
        {
            Vector3 away = target.transform.position - landing;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            playerController.StartDash(away, knockbackMultiplier, knockbackDuration);
        }
    }

    private IEnumerator ShockwaveRoutine(Vector3 center)
    {
        if (shockwaveRing == null) yield break;

        shockwaveRing.enabled = true;
        float timer = 0f;

        while (timer < ShockwaveDuration)
        {
            float t = timer / ShockwaveDuration;
            float ease = 1f - (1f - t) * (1f - t);

            BossGlowVisual.SetRing(shockwaveRing, center, Mathf.Lerp(0.4f, slamRadius * 1.5f, ease));
            BossGlowVisual.SetWidth(shockwaveRing, Mathf.Lerp(0.9f, 0.05f, t));

            Color color = shockwaveColor;
            color.a *= 1f - t;
            BossGlowVisual.SetColor(shockwaveRing, color);

            timer += Time.deltaTime;
            yield return null;
        }

        shockwaveRing.enabled = false;
    }

    private void SetSquash(float vertical)  //세로 비율 (가로는 부피 유지)
    {
        if (visualBody == null) return;

        float horizontal = 1f / Mathf.Sqrt(Mathf.Max(0.1f, vertical));
        visualBody.localScale = new Vector3(visualBaseScale.x * horizontal, visualBaseScale.y * vertical, visualBaseScale.z * horizontal);
    }

    private void RestoreBody()
    {
        if (isAirborne)
        {
            isAirborne = false;

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
            }

            if (bodyCollider != null) bodyCollider.enabled = true;

            if (!gameObject.activeInHierarchy || slamCoroutine == null)
            {
                Vector3 position = transform.position;
                position.y = groundY;
                transform.position = position;  //취소되면 공중에 남지 않게
            }
        }

        SetSquash(1f);
    }

    private void HideMarker()
    {
        if (markerRing != null) markerRing.enabled = false;
        if (markerFill != null) markerFill.enabled = false;
        if (dropLine != null) dropLine.enabled = false;
    }

    private void HideVisuals()
    {
        HideMarker();
        if (shockwaveRing != null) shockwaveRing.enabled = false;
    }

    public override void CancelPattern()
    {
        if (slamCoroutine != null)
        {
            StopCoroutine(slamCoroutine);
            slamCoroutine = null;
        }

        RestoreBody();
        HideVisuals();
        facingController?.ClearFacingOverride();

        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (enemyBallController != null) enemyBallController.SetPatternMoving(false);
    }
}
