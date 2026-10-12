using System;
using UnityEngine;
using UnityEngine.EventSystems;

// 퍼펙트 반사: 공이 벽에 닿는 순간에 맞춰 화면을 탭하면 다음 적 타격이 강해지고 잠깐 빨라진다 (연속 성공 시 더 강해짐).
// 벽에 닿기 직전(earlyWindow)에 누르면 닿는 순간 판정, 닿은 직후(lateWindow)에 누르면 바로 판정. 시간은 모두 실제 시간 기준.
// 판정은 누른 순간에 한다. 짧게 탭하고 뗐는데 타이밍 밖이었으면 MISS, 길게 눌러 대쉬가 되면(PlayerDash) MISS 없음.
// PlayerBallController가 없으면 자동으로 붙인다. 연출은 CombatJuice가 이벤트를 받아 처리한다.
[DefaultExecutionOrder(-50)]
public class PlayerPerfectBounce : MonoBehaviour
{
    [Header("Timing (실제 시간)")]
    [SerializeField] private float earlyWindow = 0.14f;
    [SerializeField] private float lateWindow = 0.1f;
    [SerializeField] private float missLockout = 0.4f;  //빗나가면 잠깐 다시 못 누름 (연타 방지)
    [SerializeField] private float approachTime = 0.4f;  //벽까지 이 시간 이내면 타이밍 링 표시

    [Header("Reward")]
    [SerializeField] private float baseDamageMultiplier = 1.5f;
    [SerializeField] private float chainDamageBonus = 0.25f;  //연속 성공마다 추가
    [SerializeField] private float maxDamageMultiplier = 2.5f;
    [SerializeField] private float speedBoostMultiplier = 1.3f;
    [SerializeField] private float speedBoostDuration = 0.5f;

    public static event Action<Vector3, int> OnPerfect;  //위치, 연속 횟수
    public static event Action<Vector3> OnMiss;
    public static bool LastHitWasPerfect { get; private set; }  //OnPlayerHitEnemy 처리 중 퍼펙트 충전 타격 여부 (전투 연출용)

    public bool IsCharged => chargedMultiplier > 1f;
    public float TimeToWall { get; private set; } = -1f;  //실제 시간(초). 벽이 가깝지 않으면 -1
    public float EarlyWindow => earlyWindow;
    public float ApproachTime => approachTime;

    private PlayerBallController controller;
    private Rigidbody rb;
    private float castRadius = 0.4f;

    private float lastWallHitTime = -999f;
    private float pendingUntil = -1f;  //벽에 닿기 직전에 눌러 닿기를 기다리는 중
    private float lockoutUntil;
    private bool isPressing;
    private bool pressJudged;       //누른 순간 퍼펙트 판정(성공/대기)이 났음
    private bool pressTakenByDash;  //길게 눌러 대쉬가 됨
    private float chargedMultiplier = 1f;
    private int chain;

    private void Awake()
    {
        controller = GetComponent<PlayerBallController>();
        rb = GetComponent<Rigidbody>();

        SphereCollider sphere = GetComponent<SphereCollider>();
        if (sphere != null) castRadius = sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
    }

    private void OnEnable()
    {
        PlayerBallController.OnPlayerHitWall += HandleWallHit;
    }

    private void OnDisable()
    {
        PlayerBallController.OnPlayerHitWall -= HandleWallHit;
        ResetState();
    }

    private void Update()
    {
        GameState state = GameManager.Instance != null ? GameManager.Instance.currentState : GameState.Ready;

        if (state != GameState.Playing)
        {
            if (state != GameState.Paused) ResetState();  //공이 멈추거나 방이 끝나면 충전/연속 초기화
            TimeToWall = -1f;
            return;
        }

        UpdateTimeToWall();

        if (pendingUntil > 0f && Time.unscaledTime > pendingUntil)  //눌렀는데 벽에 안 닿음
        {
            pendingUntil = -1f;
            Miss();
        }

        if (isPressing && Input.GetMouseButtonUp(0))  //뗌: 짧은 탭이었고 타이밍이 안 맞았으면 MISS
        {
            isPressing = false;
            if (!pressJudged && !pressTakenByDash) Miss();
        }

        if (!Input.GetMouseButtonDown(0)) return;
        if (TimeFreeze.IsActive || IsPointerOverUI()) return;
        if (Time.unscaledTime < lockoutUntil) return;

        isPressing = true;
        pressTakenByDash = false;
        pressJudged = true;

        if (pendingUntil > 0f) return;  //이미 판정 대기 중

        if (Time.unscaledTime - lastWallHitTime <= lateWindow)  //막 튕긴 직후
        {
            lastWallHitTime = -999f;
            Perfect();
            return;
        }

        if (TimeToWall >= 0f && TimeToWall <= earlyWindow)  //곧 닿음: 닿는 순간 판정
        {
            pendingUntil = Time.unscaledTime + earlyWindow + 0.05f;
            return;
        }

        pressJudged = false;  //타이밍 밖: 길게 눌러 대쉬가 되면 무시, 짧게 떼면 MISS
    }

    public void NotifyDashStarted()  //PlayerDash가 길게 누른 터치를 대쉬로 가져감
    {
        pressTakenByDash = true;
        pendingUntil = -1f;  //대쉬 조준 중엔 시간이 멈추므로 기다리던 판정은 취소
    }

    private void HandleWallHit()
    {
        if (pendingUntil > 0f)
        {
            pendingUntil = -1f;
            lastWallHitTime = -999f;
            Perfect();
            return;
        }

        lastWallHitTime = Time.unscaledTime;
    }

    private void Perfect()
    {
        chain++;
        chargedMultiplier = Mathf.Min(maxDamageMultiplier, baseDamageMultiplier + chainDamageBonus * (chain - 1));

        controller?.ApplyPerfectBounceBoost(speedBoostMultiplier, speedBoostDuration);
        HapticFeedback.Medium();

        OnPerfect?.Invoke(transform.position, chain);
    }

    private void Miss()
    {
        chain = 0;
        lockoutUntil = Time.unscaledTime + missLockout;

        OnMiss?.Invoke(transform.position);
    }

    public float ConsumeCharge()  //적과 부딪힐 때 호출. 충전된 피해 배율을 돌려주고 초기화
    {
        float multiplier = chargedMultiplier;
        LastHitWasPerfect = multiplier > 1f;
        chargedMultiplier = 1f;
        return multiplier;
    }

    private void ResetState()
    {
        pendingUntil = -1f;
        lastWallHitTime = -999f;
        chargedMultiplier = 1f;
        chain = 0;
        isPressing = false;
    }

    private void UpdateTimeToWall()
    {
        TimeToWall = -1f;
        if (rb == null) return;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;

        float timeScale = Time.timeScale < 0.2f ? 1f : Time.timeScale;  //히트스톱 같은 아주 짧은 정지는 무시 (링 깜빡임 방지)
        float realSpeed = velocity.magnitude * timeScale;
        if (realSpeed < 0.5f) return;

        int mask = Physics.DefaultRaycastLayers & ~(1 << gameObject.layer);

        if (!Physics.SphereCast(transform.position, castRadius * 0.9f, velocity.normalized, out RaycastHit hit, realSpeed * approachTime, mask, QueryTriggerInteraction.Ignore)) return;
        if (!hit.collider.CompareTag("Wall")) return;

        TimeToWall = hit.distance / realSpeed;
    }

    private bool IsPointerOverUI()  //스킬 버튼 등 UI 위 터치는 받지 않음
    {
        if (EventSystem.current == null) return false;

        if (Input.touchCount > 0) return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);

        return EventSystem.current.IsPointerOverGameObject();
    }
}
