using UnityEngine;
using System.Collections;

// 로봇 보스 패턴1 '산데비스탄': 플레이어 증강 스킬(SandevistanAugmentSkill)과 같은 흐름을 보스가 쓴다.
// 사용하면 플레이어와 다른 적의 시간이 느려지고(TimeSlowController.BeginBoss) 화면이 붉은 자홍색으로 지지직거린다.
// 보스만 원래 속도로 준비 동작(조준선) → 잔상을 남기며 플레이어에게 돌진하고, 감속이 끝날 때까지 빠르게 움직인다.
//
// 플레이어 산데비스탄과 겹치면 (추천 대응):
// - 서로의 감속이 상쇄되어 플레이어와 보스 모두 원래 속도로 움직인다 (나머지 적만 느림). 겹치는 순간 화면 글리치 + 흔들림.
// - 그 상태에서 둘이 부딪히면 '산데비스탄 충돌': 서로 크게 튕겨나가고 양쪽 산데비스탄이 즉시 풀리며,
//   플레이어는 접촉 피해를 받지 않고 보스는 잠깐 휘청인다 (반격 보상).
public class SandevistanChargePattern : BossPattern, IEnemyContactOverride
{
    private Transform player;
    private Rigidbody rb;
    private Coroutine chargeCoroutine;
    private EnemyBallController enemyBallController;
    private EnemyAnimationController animationController;
    private MovementFacingController facingController;
    private Animator animator;

    [SerializeField] private float chargeDelay;  //준비 동작 (실제 시간)
    [SerializeField] private float chargeForce;  //(예전 값, 사용 안 함) 돌진 속도는 dashSpeed
    [SerializeField] private float chargeDuration;  //돌진 시간 (실제 시간)
    [SerializeField] private SandevistanEffect effect;
    [SerializeField] private float minEchoSpeed = 30f;

    [Header("Prepare VFX")]
    [SerializeField] private GameObject prepareVfxPrefab;
    [SerializeField] private Transform prepareVfxPoint;

    [Header("Sandevistan (플레이어 증강 스킬과 같은 흐름)")]
    [SerializeField] private float slowScale = 0.3f;  //플레이어/다른 적이 흐르는 시간 배율
    [SerializeField] private float slowDuration = 2.4f;  //실제 시간(초)
    [SerializeField] private float dashSpeed = 30f;  //실제 시간 기준 이동 속도
    [SerializeField] private float dashShakeDuration = 0.15f;
    [SerializeField] private float dashShakeStrength = 0.25f;

    [Header("Aim Line")]
    [SerializeField] private Material aimLineMaterial;  //가산 빔 (BossGlowBeam)
    [SerializeField] private Color aimLineColor = new Color(1f, 0.15f, 0.45f, 0.6f);
    [SerializeField] private float aimLineWidth = 0.35f;

    [Header("Clash (플레이어 산데비스탄과 충돌)")]
    [SerializeField] private float clashKnockbackSpeed = 22f;  //보스가 튕겨나가는 속도
    [SerializeField] private float clashPlayerKnockbackMultiplier = 2f;  //플레이어 이동속도 배율
    [SerializeField] private float clashPlayerKnockbackDuration = 0.35f;
    [SerializeField] private float clashStaggerDuration = 0.8f;
    [SerializeField] private GameObject clashVfxPrefab;

    private SandevistanScreenEffect screenEffect;
    private LineRenderer aimLine;
    private GameObject activePrepareVfx;

    private bool isActive;
    private bool isDashing;
    private Vector3 dashDirection;
    private float originalAnimatorSpeed = 1f;
    private bool isAnimatorCompensated;

    protected override void Awake()
    {
        base.Awake();
        player = GameObject.FindWithTag("Player").transform;
        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;  //빠른 돌진/충돌에도 벽을 뚫지 않도록
        enemyBallController = GetComponent<EnemyBallController>();
        animationController = GetComponent<EnemyAnimationController>();
        facingController = GetComponent<MovementFacingController>();
        animator = GetComponentInChildren<Animator>(true);

        if (aimLineMaterial != null)
        {
            aimLine = BossGlowVisual.CreateLine(transform, "SandevistanAimLine", aimLineMaterial, 2);
        }

        if (prepareVfxPrefab != null && ObjectPoolManager.Instance != null) ObjectPoolManager.Instance.RegisterPool(prepareVfxPrefab, 2);
        if (clashVfxPrefab != null && ObjectPoolManager.Instance != null) ObjectPoolManager.Instance.EnsureCapacity(clashVfxPrefab, 2);
    }

    private void OnEnable()
    {
        TimeSlowController.OnClash += HandleClash;
    }

    private void OnDisable()
    {
        TimeSlowController.OnClash -= HandleClash;

        if (isActive) CancelPattern();  //패턴 도중 풀로 돌아가면 감속/화면 연출을 바로 정리
    }

    private void OnDestroy()
    {
        if (screenEffect != null) Destroy(screenEffect.gameObject);
    }

    public override void UsePattern()
    {
        if (chargeCoroutine != null) return;

        isActive = true;
        enemyBallController.SetPatternMoving(true);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // 1) 시간 감속 + 화면 연출 + 준비 동작
        TimeSlowController.BeginBoss(transform, slowScale, slowDuration);

        if (screenEffect == null) screenEffect = SandevistanScreenEffect.Create(SandevistanScreenEffect.BossTintColor);
        screenEffect.Show(0.15f);

        SetAnimatorCompensated(true);
        animationController?.PlayPrepare();
        SoundManager.Instance?.PlaySFX(SoundId.RobotChargeStart);
        PlayPrepareVfx();

        chargeCoroutine = StartCoroutine(SandevistanRoutine());
    }

    private IEnumerator SandevistanRoutine()
    {
        // 준비: 플레이어를 바라보며 조준선
        float timer = 0f;
        while (timer < chargeDelay && IsBattleRunning())
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            Vector3 aim = player.position - transform.position;
            aim.y = 0f;
            facingController?.SetFacingOverride(aim);
            UpdateAimLine(timer / Mathf.Max(0.01f, chargeDelay));

            if (GameManager.Instance.currentState == GameState.Playing) timer += Time.unscaledDeltaTime;
            yield return null;
        }

        HideAimLine();
        StopPrepareVfx();
        if (!IsBattleRunning()) { Cleanup(true); yield break; }

        // 2) 잔상을 남기며 돌진
        dashDirection = player.position - transform.position;
        dashDirection.y = 0f;
        dashDirection = dashDirection.sqrMagnitude > 0.001f ? dashDirection.normalized : transform.forward;
        facingController?.SetFacingOverride(dashDirection, true);

        isDashing = true;
        effect?.StartEcho(rb, minEchoSpeed);

        if (CombatFeedbackManager.Instance != null)
        {
            CombatFeedbackManager.Instance.RequestImpact(SoundId.RobotDash, dashShakeDuration, dashShakeStrength, priority: 4);
        }
        else
        {
            SoundManager.Instance?.PlaySFX(SoundId.RobotDash);
        }

        timer = 0f;
        while (timer < chargeDuration && isDashing && IsBattleRunning())
        {
            if (GameManager.Instance.currentState == GameState.Playing) timer += Time.unscaledDeltaTime;
            yield return null;
        }

        isDashing = false;
        facingController?.ClearFacingOverride();
        effect?.StopEchoLoop();
        if (!IsBattleRunning()) { Cleanup(true); yield break; }

        // 3) 감속이 끝날 때까지 빠른 상태로 일반 이동 (EnemyBallController가 시간 보정)
        enemyBallController.SetPatternMoving(false);
        while (TimeSlowController.IsBossAccelerated && IsBattleRunning()) yield return null;

        Cleanup(false);
    }

    private void FixedUpdate()
    {
        if (!isDashing || rb == null || rb.isKinematic) return;

        // 벽에 튕기면 방향은 물리에 맡기고 속도만 유지 (시간 보정 포함)
        Vector3 velocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 direction = velocity.sqrMagnitude > 0.01f ? velocity.normalized : dashDirection;

        float compensation = TimeSlowController.GetEnemyCompensation(transform);
        rb.linearVelocity = direction * dashSpeed * compensation + Vector3.up * rb.linearVelocity.y;
    }

    private void Update()
    {
        if (isAnimatorCompensated && animator != null)
        {
            animator.speed = originalAnimatorSpeed * TimeSlowController.GetEnemyCompensation(transform);
        }
    }

    // 플레이어와 부딪힘: 산데비스탄끼리면 '충돌', 아니면 돌진을 멈추고 기본 접촉 피해
    public bool HandlePlayerContact(Collision collision)
    {
        if (!isActive) return false;

        if (TimeSlowController.IsClashing)
        {
            StartCoroutine(ClashRoutine(collision));
            return true;
        }

        isDashing = false;  //한 번 맞히면 돌진 종료 (느려진 플레이어를 연달아 때리지 않게)
        return false;
    }

    private IEnumerator ClashRoutine(Collision collision)
    {
        Vector3 contact = collision.contactCount > 0 ? collision.GetContact(0).point : (transform.position + player.position) * 0.5f;
        Vector3 away = transform.position - player.position;
        away.y = 0f;
        away = away.sqrMagnitude > 0.001f ? away.normalized : -dashDirection;

        // 양쪽 산데비스탄 즉시 해제 (시간 정상화)
        TimeSlowController.EndPlayer();
        TimeSlowController.EndBoss(transform);
        enemyBallController.ResetTimeCompensation();
        enemyBallController.SetPatternMoving(true);  //튕겨나가는 동안 일반 이동이 속도를 덮어쓰지 않게

        if (chargeCoroutine != null)
        {
            StopCoroutine(chargeCoroutine);
            chargeCoroutine = null;
        }

        isDashing = false;
        HideAimLine();
        StopPrepareVfx();
        effect?.StopEchoLoop();
        SetAnimatorCompensated(false);
        screenEffect?.Burst(0.3f);
        screenEffect?.Hide(0.3f);

        if (CombatFeedbackManager.Instance != null)
        {
            CombatFeedbackManager.Instance.RequestImpact(SoundId.RobotMissileImpact, 0.35f, 0.6f, priority: 7);
        }
        else
        {
            SoundManager.Instance?.PlaySFX(SoundId.RobotMissileImpact);
        }

        if (clashVfxPrefab != null && VFXManager.Instance != null)
        {
            VFXManager.Instance.Play(clashVfxPrefab, contact, Quaternion.identity, 1f);
        }

        // 서로 크게 튕겨나감
        rb.linearVelocity = away * clashKnockbackSpeed;
        PlayerBallController playerController = collision.gameObject.GetComponent<PlayerBallController>();
        playerController?.StartDash(-away, clashPlayerKnockbackMultiplier, clashPlayerKnockbackDuration);

        // 보스 휘청임
        animationController?.PlayPrepare();
        float timer = 0f;
        while (timer < clashStaggerDuration && IsBattleRunning())
        {
            if (timer > 0.25f) rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, 10f * Time.deltaTime);
            timer += Time.deltaTime;
            yield return null;
        }

        Cleanup(false);
    }

    private void HandleClash()  //플레이어가 산데비스탄을 겹쳐 씀 (또는 그 반대)
    {
        if (!isActive) return;

        screenEffect?.Burst(0.35f);

        if (CombatFeedbackManager.Instance != null)
        {
            CombatFeedbackManager.Instance.RequestImpact(SoundId.RobotDash, 0.25f, 0.35f, priority: 6);
        }
    }

    private bool IsBattleRunning()
    {
        if (GameManager.Instance == null) return false;

        GameState state = GameManager.Instance.currentState;
        return state == GameState.Playing || state == GameState.Paused;
    }

    private void UpdateAimLine(float progress)
    {
        if (aimLine == null) return;

        aimLine.enabled = true;
        Vector3 start = transform.position; start.y = 0.08f;
        Vector3 end = player.position; end.y = 0.08f;
        aimLine.SetPosition(0, start);
        aimLine.SetPosition(1, end);

        bool flash = progress > 0.65f && Mathf.Repeat(Time.unscaledTime * 14f, 1f) < 0.5f;  //돌진 직전 깜빡임
        Color color = flash ? new Color(1f, 0.85f, 0.95f, 0.9f) : aimLineColor;
        BossGlowVisual.SetColor(aimLine, color);
        BossGlowVisual.SetWidth(aimLine, aimLineWidth * (flash ? 0.6f : 1f));
    }

    private void HideAimLine()
    {
        if (aimLine != null) aimLine.enabled = false;
    }

    private void PlayPrepareVfx()
    {
        if (prepareVfxPrefab == null || VFXManager.Instance == null) return;

        Transform parent = prepareVfxPoint != null ? prepareVfxPoint : transform;

        // 반납 타이머가 게임 시간 기준이라 감속 배율만큼 줄여서 넘김, 파티클은 실제 시간으로 재생
        activePrepareVfx = VFXManager.Instance.PlayAttached(prepareVfxPrefab, parent, Vector3.zero, Quaternion.identity, chargeDelay * slowScale);
        if (activePrepareVfx == null) return;

        foreach (ParticleSystem particle in activePrepareVfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particle.main;
            main.useUnscaledTime = true;
        }
    }

    private void StopPrepareVfx()
    {
        if (activePrepareVfx == null) return;

        foreach (ParticleSystem particle in activePrepareVfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particle.main;
            main.useUnscaledTime = false;  //풀에서 다른 용도로 재사용될 때를 위해 원래대로
        }

        activePrepareVfx = null;
    }

    private void SetAnimatorCompensated(bool compensated)  //보스 애니메이션은 원래 속도로
    {
        if (animator == null) return;

        if (compensated)
        {
            if (!isAnimatorCompensated) originalAnimatorSpeed = animator.speed;
            isAnimatorCompensated = true;
        }
        else
        {
            if (isAnimatorCompensated) animator.speed = originalAnimatorSpeed;
            isAnimatorCompensated = false;
        }
    }

    private void Cleanup(bool immediate)  //immediate: 방 클리어, 게임오버, 비활성화 등으로 바로 끝낼 때
    {
        TimeSlowController.EndBoss(transform);

        isDashing = false;
        HideAimLine();
        StopPrepareVfx();
        effect?.StopEchoLoop();
        SetAnimatorCompensated(false);
        facingController?.ClearFacingOverride();

        if (screenEffect != null)
        {
            if (immediate) screenEffect.HideImmediate();
            else screenEffect.Hide(0.25f);
        }

        if (enemyBallController != null) enemyBallController.SetPatternMoving(false);

        chargeCoroutine = null;

        if (isActive)
        {
            isActive = false;
            bossController.EndPattern();
        }
    }

    public override void CancelPattern()
    {
        StopAllCoroutines();
        chargeCoroutine = null;

        bool wasActive = isActive;
        isActive = false;

        TimeSlowController.EndBoss(transform);

        isDashing = false;
        HideAimLine();
        StopPrepareVfx();
        effect?.StopEchoLoop();
        SetAnimatorCompensated(false);
        facingController?.ClearFacingOverride();
        screenEffect?.HideImmediate();

        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (enemyBallController != null) enemyBallController.SetPatternMoving(false);

        if (wasActive) Debug.Log("[Boss] 산데비스탄 패턴 취소");
    }
}
