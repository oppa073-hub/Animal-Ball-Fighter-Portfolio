using System.Collections;
using UnityEngine;
using UnityEngine.Localization;

// 증강 스킬 '산데비스탄': 로봇 보스 패턴1(SandevistanChargePattern)을 플레이어가 쓴다.
// 사용하면 적/보스의 시간이 거의 멈춘 듯 느려지고(TimeSlowController) 화면이 청록색으로 지지직거린다.
// 준비 동작(로봇 준비 이펙트 + 차지 효과음) 후 가장 가까운 적에게 잔상을 남기며 돌진(로봇 돌진 효과음)한다.
public class SandevistanAugmentSkill : AugmentSkillRuntime
{
    [Header("Time Slow")]
    [SerializeField] private float baseSlowScale = 0.15f;  //적이 흐르는 시간 배율
    [SerializeField] private float baseSlowDuration = 2.5f;  //실제 시간(초)
    [SerializeField] private float slowDurationIncreaseAfterLevel5 = 0.25f;

    [Header("Charge (로봇 패턴1과 같은 흐름)")]
    [SerializeField] private float prepareDuration = 0.4f;  //실제 시간
    [SerializeField] private float dashDuration = 1f;  //실제 시간
    [SerializeField] private float dashSpeedMultiplier = 20f;  //이동속도 배율 (최대 속도에서 잘림)

    [Header("VFX (로봇 보스와 같은 에셋)")]
    [SerializeField] private GameObject prepareVfxPrefab;
    [SerializeField] private Material echoMaterial;
    [SerializeField] private Gradient echoGradient;
    [SerializeField] private float echoInterval = 0.04f;
    [SerializeField] private float echoLifeTime = 0.3f;
    [SerializeField] private float dashShakeDuration = 0.15f;
    [SerializeField] private float dashShakeStrength = 0.2f;

    [Header("Localization")]
    [SerializeField] private LocalizedString durationUpgradeText;
    [SerializeField] private LocalizedString cooldownUpgradeText;
    [SerializeField] private LocalizedString slowUpgradeText;
    [SerializeField] private LocalizedString dashUpgradeText;

    private PlayerBallController playerController;
    private Rigidbody playerRigidbody;
    private Animator playerAnimator;
    private MeshEchoTrail echoTrail;
    private SandevistanScreenEffect screenEffect;

    private float slowScale;
    private float slowDuration;
    private float currentDashDuration;
    private float cooldownMultiplier = 1f;

    private Coroutine skillCoroutine;
    private GameObject activePrepareVfx;
    private float originalAnimatorSpeed = 1f;
    private bool isAnimatorCompensated;

    public override float CurrentCooldown => Cooldown * cooldownMultiplier;
    public override bool IsBusy => skillCoroutine != null;

    public override void Initialize(AugmentSkillData skillData, Sprite icon)
    {
        base.Initialize(skillData, icon);

        slowScale = baseSlowScale;
        slowDuration = baseSlowDuration;
        currentDashDuration = dashDuration;
        cooldownMultiplier = 1f;

        playerController = GetComponentInParent<PlayerBallController>();
        playerRigidbody = playerController != null ? playerController.GetComponent<Rigidbody>() : null;
        playerAnimator = playerController != null ? playerController.GetComponent<Animator>() : null;

        if (prepareVfxPrefab != null && ObjectPoolManager.Instance != null) ObjectPoolManager.Instance.RegisterPool(prepareVfxPrefab, 2);

        PlayerCharacterSetup characterSetup = playerController != null ? playerController.GetComponent<PlayerCharacterSetup>() : null;
        Transform model = characterSetup != null && characterSetup.ModelRoot != null ? characterSetup.ModelRoot : playerController != null ? playerController.transform : transform;

        echoTrail = gameObject.AddComponent<MeshEchoTrail>();
        echoTrail.Setup(model, playerRigidbody, echoMaterial, echoGradient, echoInterval, echoLifeTime, 1f);

        screenEffect = SandevistanScreenEffect.Create();
    }

    protected override bool ActivateSkill()
    {
        if (skillCoroutine != null || playerController == null) return false;

        skillCoroutine = StartCoroutine(SkillRoutine());
        return true;
    }

    private IEnumerator SkillRoutine()
    {
        // 1) 시간 감속 + 화면 연출 + 준비 동작
        TimeSlowController.Begin(slowScale, slowDuration);
        screenEffect.Show(0.15f);
        SetPlayerAnimatorCompensated(true);

        SoundManager.Instance?.PlaySFX(SoundId.RobotChargeStart);
        playerController.SetHold(true);
        PlayPrepareVfx();

        yield return WaitRealTime(prepareDuration);
        if (!IsBattleRunning()) { CleanupSkill(true); yield break; }

        // 2) 가장 가까운 적에게 잔상을 남기며 돌진
        StopPrepareVfx();
        playerController.StartDash(FindDashDirection(), dashSpeedMultiplier, currentDashDuration);
        echoTrail.StartEcho();

        if (CombatFeedbackManager.Instance != null)
        {
            CombatFeedbackManager.Instance.RequestImpact(SoundId.RobotDash, dashShakeDuration, dashShakeStrength, priority: 4);  //효과음 + 화면 흔들림
        }
        else
        {
            SoundManager.Instance?.PlaySFX(SoundId.RobotDash);
        }

        yield return WaitRealTime(currentDashDuration);
        if (!IsBattleRunning()) { CleanupSkill(true); yield break; }

        echoTrail.StopEcho();

        // 3) 감속이 끝날 때까지 연출 유지
        while (TimeSlowController.IsPlayerAccelerated) yield return null;

        CleanupSkill(false);
    }

    private IEnumerator WaitRealTime(float duration)  //일시정지 중에는 세지 않고, 전투가 끝나면(방 클리어, 게임오버 등) 바로 빠져나옴
    {
        float timer = 0f;

        while (timer < duration && IsBattleRunning())
        {
            if (GameManager.Instance.currentState == GameState.Playing) timer += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private bool IsBattleRunning()
    {
        if (GameManager.Instance == null) return false;

        GameState state = GameManager.Instance.currentState;
        return state == GameState.Playing || state == GameState.Paused;
    }

    private Vector3 FindDashDirection()
    {
        Vector3 origin = playerController.transform.position;
        EnemyController nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (EnemyController enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
        {
            if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy) continue;

            float distance = (enemy.transform.position - origin).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = enemy;
            }
        }

        if (nearest == null) return Vector3.zero;  //적이 없으면 현재 이동 방향

        Vector3 direction = nearest.transform.position - origin;
        direction.y = 0f;
        return direction;
    }

    private void PlayPrepareVfx()
    {
        if (prepareVfxPrefab == null || VFXManager.Instance == null) return;

        // 반납 타이머가 게임 시간 기준이라 감속 배율만큼 줄여서 넘김, 파티클은 실제 시간으로 재생
        activePrepareVfx = VFXManager.Instance.PlayAttached(prepareVfxPrefab, playerController.transform, Vector3.zero, Quaternion.identity, prepareDuration * slowScale);
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

    private void SetPlayerAnimatorCompensated(bool compensated)  //플레이어 애니메이션은 원래 속도로
    {
        if (playerAnimator == null) return;

        if (compensated)
        {
            if (!isAnimatorCompensated) originalAnimatorSpeed = playerAnimator.speed;
            isAnimatorCompensated = true;
            playerAnimator.speed = originalAnimatorSpeed * TimeSlowController.PlayerCompensation;
        }
        else
        {
            if (isAnimatorCompensated) playerAnimator.speed = originalAnimatorSpeed;
            isAnimatorCompensated = false;
        }
    }

    protected override void Update()
    {
        base.Update();  //쿨타임 진행

        // 보스 산데비스탄과 부딪혀 감속이 일찍 풀리거나 배율이 바뀌어도 애니메이션 속도를 맞춘다
        if (isAnimatorCompensated && playerAnimator != null) playerAnimator.speed = originalAnimatorSpeed * TimeSlowController.PlayerCompensation;
    }

    private void CleanupSkill(bool immediate)  //immediate: 방 클리어, 게임오버, 비활성화 등으로 바로 끝낼 때
    {
        if (immediate) TimeSlowController.EndPlayer();

        StopPrepareVfx();
        if (echoTrail != null) echoTrail.StopEcho();
        if (playerController != null) playerController.SetHold(false);
        SetPlayerAnimatorCompensated(false);

        if (screenEffect != null)
        {
            if (immediate) screenEffect.HideImmediate();
            else screenEffect.Hide(0.25f);
        }

        skillCoroutine = null;
    }

    private void OnEnable()
    {
        TimeSlowController.OnClash += HandleClash;
    }

    private void HandleClash()  //보스 산데비스탄과 겹침: 화면이 크게 지지직 (효과음/흔들림은 보스 쪽에서)
    {
        if (skillCoroutine != null && screenEffect != null) screenEffect.Burst(0.35f);
    }

    private void OnDisable()
    {
        TimeSlowController.OnClash -= HandleClash;

        if (skillCoroutine == null) return;

        StopCoroutine(skillCoroutine);
        CleanupSkill(true);
    }

    private void OnDestroy()
    {
        if (screenEffect != null) Destroy(screenEffect.gameObject);
    }

    protected override void OnLevelUp()
    {
        switch (Level)
        {
            case 2:
                slowDuration = baseSlowDuration + 0.5f;
                break;

            case 3:
                cooldownMultiplier = 0.85f;
                break;

            case 4:
                slowScale = 0.1f;
                break;

            case 5:
                currentDashDuration = dashDuration + 0.3f;
                cooldownMultiplier = 0.7f;
                break;

            default:
                if (Level > data.specialUpgradeLevel)
                {
                    slowDuration += slowDurationIncreaseAfterLevel5;
                }
                break;
        }
    }

    public override string GetNextLevelDescription()
    {
        int nextLevel = Level + 1;

        switch (nextLevel)
        {
            case 2:
                return durationUpgradeText.GetLocalizedString();

            case 3:
                return cooldownUpgradeText.GetLocalizedString();

            case 4:
                return slowUpgradeText.GetLocalizedString();

            case 5:
                return dashUpgradeText.GetLocalizedString();

            default:
                return durationUpgradeText.GetLocalizedString();
        }
    }
}
