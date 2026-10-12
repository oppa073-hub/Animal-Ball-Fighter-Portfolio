using UnityEngine;
using System.Collections;

// 히어로 보스 패턴1: 눈에서 부채꼴 5갈래 레이저.
// 차지(눈에 빛이 모이고 바닥 조준선이 뻗어 나감 → 앞부분은 플레이어를 따라 조준, 뒷부분은 고정되며 깜빡임)
// → 발사(굵은 빛 + 하얀 심지, 순간 팽창 후 지지직 유지, 벽에 닿는 곳까지만, 끝점 불꽃, 화면 흔들림) → 수축.
// 분노(2페이즈)에서는 사이사이 각도로 한 번 더 쏜다.
public class EyeLaserPattern : BossPattern
{
    [Header("Reference")]
    [SerializeField] private Transform eyePoint;

    private Transform player;
    private Rigidbody rb;
    private EnemyBallController enemyBallController;
    private EnemyAnimationController animationController;
    private MovementFacingController facingController;

    [Header("Laser")]
    [SerializeField] private float warningDuration = 1f;
    [SerializeField] private float laserRange = 20f;
    [SerializeField] private float damage = 13f;
    [SerializeField] private float fanAngle = 48f;
    [SerializeField] private int beamCount = 5;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private float laserHitRadius = 0.5f;
    [SerializeField] private float laserDuration = 0.5f;
    [SerializeField, Range(0f, 1f)] private float aimTrackRatio = 0.55f;  //차지 시간 중 이 비율까지는 플레이어를 따라 조준
    [SerializeField] private float aimTurnSpeed = 220f;  //조준 회전 속도 (도/초)

    [Header("Rage (2페이즈)")]
    [SerializeField] private bool rageSecondVolley = true;
    [SerializeField] private float secondVolleyChargeDuration = 0.55f;

    [Header("Charge VFX")]
    [SerializeField] private GameObject chargeVfxPrefab;
    [SerializeField] private Transform chargeVfxPoint;

    [Header("Visual")]
    [SerializeField] private Material beamMaterial;  //가산 빔 (BossGlowBeam)
    [SerializeField] private Material orbMaterial;   //가산 둥근 빛 (BossGlowOrb)
    [SerializeField] private Color beamColor = new Color(1f, 0.28f, 0.12f, 1f);
    [SerializeField] private Color coreColor = new Color(1f, 0.95f, 0.85f, 1f);
    [SerializeField] private Color telegraphColor = new Color(1f, 0.2f, 0.1f, 0.45f);
    [SerializeField] private float glowWidth = 1.2f;
    [SerializeField] private float coreWidth = 0.32f;
    [SerializeField] private float telegraphWidth = 0.32f;
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private float fireShakeDuration = 0.22f;
    [SerializeField] private float fireShakeStrength = 0.35f;

    private const float ExpandTime = 0.07f;
    private const float CollapseTime = 0.18f;
    private const float GroundY = 0.08f;

    private LineRenderer[] glowLines;
    private LineRenderer[] coreLines;
    private LineRenderer[] telegraphLines;
    private Transform orb;
    private Renderer orbRenderer;
    private MaterialPropertyBlock orbBlock;
    private Vector3[] directions;
    private Vector3[] endPoints;
    private readonly RaycastHit[] wallHits = new RaycastHit[16];

    private Coroutine laserCoroutine;

    protected override void Awake()
    {
        base.Awake();

        player = GameObject.FindWithTag("Player").transform;

        rb = GetComponent<Rigidbody>();
        enemyBallController = GetComponent<EnemyBallController>();
        animationController = GetComponent<EnemyAnimationController>();
        facingController = GetComponent<MovementFacingController>();

        BuildVisuals();

        if (impactVfxPrefab != null && ObjectPoolManager.Instance != null) ObjectPoolManager.Instance.EnsureCapacity(impactVfxPrefab, Mathf.Max(1, beamCount) * 2);
    }

    private void BuildVisuals()
    {
        int count = Mathf.Max(1, beamCount);
        glowLines = new LineRenderer[count];
        coreLines = new LineRenderer[count];
        telegraphLines = new LineRenderer[count];
        directions = new Vector3[count];
        endPoints = new Vector3[count];

        // 보스가 회전/이동해도 빛이 따라 돌지 않도록 보스 밖(씬 루트)에 둔다
        Transform root = new GameObject("HeroLaserVisual").transform;
        root.SetParent(transform.parent, false);
        visualRoot = root;

        for (int i = 0; i < count; i++)
        {
            telegraphLines[i] = BossGlowVisual.CreateLine(root, "LaserTelegraph", beamMaterial, 2);
            glowLines[i] = BossGlowVisual.CreateLine(root, "LaserGlow", beamMaterial, 6);
            coreLines[i] = BossGlowVisual.CreateLine(root, "LaserCore", beamMaterial, 6);
            glowLines[i].sortingOrder = 1;
            coreLines[i].sortingOrder = 2;
        }

        orb = BossGlowVisual.CreateOrb(root, "LaserEyeOrb", orbMaterial, out orbRenderer);
        orbBlock = new MaterialPropertyBlock();
    }

    private Transform visualRoot;

    private void OnDestroy()
    {
        if (visualRoot != null) Destroy(visualRoot.gameObject);
    }

    private void OnDisable()
    {
        laserCoroutine = null;  //비활성화로 코루틴이 멈추므로 풀에서 다시 나왔을 때 패턴이 막히지 않게
        HideAll();
    }

    public override void UsePattern()
    {
        if (laserCoroutine != null) return;

        // 일반 이동 중지
        enemyBallController.SetPatternMoving(true);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        animationController?.PlayPrepare();

        laserCoroutine = StartCoroutine(LaserRoutine());
    }

    private IEnumerator LaserRoutine()
    {
        if (eyePoint == null || player == null)
        {
            EndLaserPattern();
            yield break;
        }

        Vector3 aim = FlatDirection(player.position - transform.position, transform.forward);
        facingController?.SetFacingOverride(aim, true);

        int volleyCount = rageSecondVolley && bossController != null && bossController.CurrentPhase >= 2 ? 2 : 1;
        float step = beamCount > 1 ? fanAngle / (beamCount - 1) : 0f;

        for (int volley = 0; volley < volleyCount; volley++)
        {
            float chargeTime = volley == 0 ? warningDuration : secondVolleyChargeDuration;
            float angleOffset = volley == 0 ? 0f : step * 0.5f;  //두 번째는 첫 발 사이 빈틈으로

            SoundManager.Instance?.PlaySFX(SoundId.HeroLaserCharge);

            if (chargeVfxPrefab != null && VFXManager.Instance != null)
            {
                Transform point = chargeVfxPoint != null ? chargeVfxPoint : eyePoint;
                VFXManager.Instance.PlayAttached(chargeVfxPrefab, point, Vector3.zero, Quaternion.identity, chargeTime);
            }

            // 1) 차지
            float timer = 0f;
            while (timer < chargeTime)
            {
                StopBody();

                float progress = timer / chargeTime;
                bool tracking = volley == 0 && progress < aimTrackRatio;

                if (tracking)
                {
                    Vector3 target = FlatDirection(player.position - eyePoint.position, aim);
                    aim = Vector3.RotateTowards(aim, target, aimTurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
                    facingController?.SetFacingOverride(aim, true);
                }

                UpdateDirections(aim, angleOffset);
                UpdateTelegraph(progress, tracking);
                UpdateChargeOrb(progress);

                timer += Time.deltaTime;
                yield return null;
            }

            SetTelegraphVisible(false);

            // 2) 발사
            yield return FireRoutine();

            if (volley + 1 < volleyCount) yield return new WaitForSeconds(0.15f);
        }

        HideAll();
        EndLaserPattern();
    }

    private void StopBody()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private static Vector3 FlatDirection(Vector3 vector, Vector3 fallback)
    {
        vector.y = 0f;
        if (vector.sqrMagnitude < 0.0001f)
        {
            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
        }

        return vector.normalized;
    }

    private void UpdateDirections(Vector3 center, float angleOffset)
    {
        int count = directions.Length;
        float start = -fanAngle * 0.5f + angleOffset;
        float step = count > 1 ? fanAngle / (count - 1) : 0f;
        Vector3 origin = eyePoint.position;

        for (int i = 0; i < count; i++)
        {
            // 두 번째 사격은 사이 각도라 하나 적게 (마지막 줄은 부채 밖으로 나가지 않게 첫 줄 왼쪽으로)
            float angle = start + step * i;
            if (angleOffset > 0f && angle > fanAngle * 0.5f) angle = -fanAngle * 0.5f - step * 0.5f;

            directions[i] = Quaternion.AngleAxis(angle, Vector3.up) * center;
            endPoints[i] = FindBeamEnd(origin, directions[i]);
        }
    }

    private Vector3 FindBeamEnd(Vector3 origin, Vector3 direction)  //벽에 닿는 곳까지만 (벽 콜라이더가 낮아도 맞도록 바닥 가까이에서 검사)
    {
        Vector3 probe = new Vector3(origin.x, 0.3f, origin.z);
        int hitCount = Physics.RaycastNonAlloc(probe, direction, wallHits, laserRange, ~0, QueryTriggerInteraction.Ignore);

        float nearest = laserRange;
        for (int i = 0; i < hitCount; i++)
        {
            if (wallHits[i].collider.CompareTag("Wall") && wallHits[i].distance < nearest) nearest = wallHits[i].distance;
        }

        return origin + direction * nearest;
    }

    private void UpdateTelegraph(float progress, bool tracking)
    {
        float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(progress / 0.35f), 3f);  //처음 0.35 동안 뻗어 나감
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f);

        for (int i = 0; i < telegraphLines.Length; i++)
        {
            LineRenderer line = telegraphLines[i];
            line.enabled = true;

            Vector3 start = eyePoint.position; start.y = GroundY;
            Vector3 end = endPoints[i]; end.y = GroundY;
            line.SetPosition(0, start);
            line.SetPosition(1, Vector3.Lerp(start, end, grow));

            Color color = telegraphColor;
            float width = telegraphWidth;

            if (tracking)
            {
                color.a *= 0.65f + 0.35f * pulse;
            }
            else
            {
                // 조준 고정: 가늘어지며 빠르게 깜빡임 → 곧 발사
                bool flash = Mathf.Repeat(Time.time * 14f, 1f) < 0.5f;
                color = flash ? new Color(1f, 0.85f, 0.75f, 0.9f) : telegraphColor;
                width *= 0.55f;
            }

            BossGlowVisual.SetColor(line, color);
            BossGlowVisual.SetWidth(line, width);
        }
    }

    private void UpdateChargeOrb(float progress)
    {
        if (orb == null) return;

        orb.gameObject.SetActive(true);
        orb.position = eyePoint.position;
        BossGlowVisual.FaceCamera(orb);

        float jitter = 1f + Random.Range(-0.08f, 0.08f);
        orb.localScale = Vector3.one * Mathf.Lerp(0.25f, 1.3f, progress * progress) * jitter;

        Color color = Color.Lerp(beamColor, coreColor, progress * 0.6f);
        color.a = Mathf.Lerp(0.4f, 1f, progress);
        BossGlowVisual.SetOrbColor(orbRenderer, orbBlock, color);
    }

    private IEnumerator FireRoutine()
    {
        if (CombatFeedbackManager.Instance != null)
        {
            CombatFeedbackManager.Instance.RequestImpact(SoundId.HeroLaserFire, fireShakeDuration, fireShakeStrength, priority: 5);
        }
        else
        {
            SoundManager.Instance?.PlaySFX(SoundId.HeroLaserFire);
        }

        Vector3 origin = eyePoint.position;

        for (int i = 0; i < endPoints.Length; i++)
        {
            glowLines[i].enabled = true;
            coreLines[i].enabled = true;
            glowLines[i].SetPosition(0, origin);
            glowLines[i].SetPosition(1, endPoints[i]);
            coreLines[i].SetPosition(0, origin);
            coreLines[i].SetPosition(1, endPoints[i]);
            BossGlowVisual.SetColor(glowLines[i], beamColor);
            BossGlowVisual.SetColor(coreLines[i], coreColor);

            if (impactVfxPrefab != null && VFXManager.Instance != null)
            {
                Vector3 back = origin - endPoints[i]; back.y = 0f;
                VFXManager.Instance.Play(impactVfxPrefab, endPoints[i], Quaternion.LookRotation(back.sqrMagnitude > 0.001f ? back : Vector3.back), 0.8f);
            }
        }

        bool playerDamaged = false;
        float total = ExpandTime + laserDuration + CollapseTime;
        float timer = 0f;

        while (timer < total)
        {
            StopBody();

            float width;
            if (timer < ExpandTime)
            {
                float t = timer / ExpandTime;
                width = Mathf.Lerp(0f, 1.35f, t);  //순간 팽창 (살짝 넘쳤다가)
            }
            else if (timer < ExpandTime + laserDuration)
            {
                float t = (timer - ExpandTime) / laserDuration;
                width = Mathf.Lerp(1.35f, 1f, Mathf.Clamp01(t * 4f)) * (1f + Random.Range(-0.1f, 0.1f));  //지지직 떨림
            }
            else
            {
                float t = (timer - ExpandTime - laserDuration) / CollapseTime;
                width = 1f - t * t;
            }

            for (int i = 0; i < glowLines.Length; i++)
            {
                BossGlowVisual.SetWidth(glowLines[i], glowWidth * width);
                BossGlowVisual.SetWidth(coreLines[i], coreWidth * width * (1f + Random.Range(-0.15f, 0.15f)));
            }

            // 발사 섬광: 크게 터졌다가 빔과 함께 줄어듦
            if (orb != null)
            {
                orb.position = origin;
                BossGlowVisual.FaceCamera(orb);
                orb.localScale = Vector3.one * (timer < ExpandTime ? 2.2f : 1.4f * width);
                BossGlowVisual.SetOrbColor(orbRenderer, orbBlock, coreColor);
            }

            // 빔이 살아 있는 동안 한 번만 피해
            if (!playerDamaged && timer < ExpandTime + laserDuration)
            {
                playerDamaged = CheckBeamHit(origin);
            }

            timer += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < glowLines.Length; i++)
        {
            glowLines[i].enabled = false;
            coreLines[i].enabled = false;
        }

        if (orb != null) orb.gameObject.SetActive(false);
    }

    private bool CheckBeamHit(Vector3 origin)
    {
        if (DamageManager.Instance == null) return false;

        for (int i = 0; i < directions.Length; i++)
        {
            float length = Vector3.Distance(origin, endPoints[i]);

            if (Physics.SphereCast(origin, laserHitRadius, directions[i], out RaycastHit hit, length, targetLayer))
            {
                DamageManager.Instance.ApplyFixedDamage(hit.collider.gameObject, damage, DamageTextType.Normal);
                return true;
            }
        }

        return false;
    }

    private void SetTelegraphVisible(bool visible)
    {
        if (telegraphLines == null) return;

        for (int i = 0; i < telegraphLines.Length; i++)
        {
            if (telegraphLines[i] != null) telegraphLines[i].enabled = visible;
        }
    }

    private void HideAll()
    {
        SetTelegraphVisible(false);

        if (glowLines != null)
        {
            for (int i = 0; i < glowLines.Length; i++)
            {
                if (glowLines[i] != null) glowLines[i].enabled = false;
                if (coreLines[i] != null) coreLines[i].enabled = false;
            }
        }

        if (orb != null) orb.gameObject.SetActive(false);
    }

    private void EndLaserPattern()
    {
        facingController?.ClearFacingOverride();

        enemyBallController.SetPatternMoving(false);

        rb.WakeUp();

        laserCoroutine = null;

        bossController.EndPattern();
    }

    public override void CancelPattern()
    {
        facingController?.ClearFacingOverride();

        if (laserCoroutine != null)
        {
            StopCoroutine(laserCoroutine);
            laserCoroutine = null;
        }

        HideAll();

        if (rb != null && !rb.isKinematic)
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
