using UnityEngine;
using UnityEngine.EventSystems;

// 전투 중(공이 움직이는 동안) 화면 아래쪽 엄지 영역을 길게 누르거나 끌면 시간이 멈추고 대쉬 방향을 조준한다 (짧은 탭은 퍼펙트 반사).
// 놓으면 드래그한 방향으로 짧게 돌진(돼지 돌진 이펙트)한 뒤 그 방향으로 계속 이동한다.
// 유예 시간 안에 놓지 않으면 게이지만 소비되고 그대로 재개. 게이지는 칸 단위로 시간이 지나면 다시 찬다.
public class PlayerDash : MonoBehaviour
{
    public static event System.Action OnAimStarted;    //대쉬 조준 시작 (시간을 멈추기 직전, 튜토리얼이 자기 정지를 풀 수 있게)
    public static event System.Action OnDashPerformed; //실제로 돌진함

    [Header("Input")]
    [SerializeField, Range(0.1f, 1f)] private float inputZoneHeight = 0.45f;  //화면 아래에서부터 이 비율까지가 대쉬 드래그 영역
    [SerializeField, Range(0.1f, 1f)] private float inputZoneWidth = 0.66f;  //왼쪽부터 이 비율까지 (오른쪽 스킬 버튼 쪽은 제외)
    [SerializeField] private float minDragDistance = 40f;  //픽셀
    [SerializeField] private float holdThreshold = 0.2f;  //실제 시간. 이보다 길게 누르거나 끌면 대쉬, 짧게 떼면 탭(퍼펙트 반사)
    [SerializeField] private float aimGuideMaxDrag = 300f;

    [Header("Dash")]
    [SerializeField] private float dashSpeedMultiplier = 1.8f;
    [SerializeField] private float dashDuration = 0.25f;
    [SerializeField] private float graceDuration = 1.5f;  //실제 시간. 이 안에 놓아야 대쉬

    [Header("Gauge")]
    [SerializeField] private int baseCharges = 1;
    [SerializeField] private float baseRechargeTime = 8f;  //1칸 충전 시간
    [SerializeField] private float rechargeSpeedPerLevel = 0.2f;  //상점 충전 속도 강화 레벨당 +20%

    [Header("Impact (돼지 돌진과 같은 이펙트)")]
    [SerializeField] private GameObject chargeVfxPrefab;
    [SerializeField] private Vector3 vfxRotationOffset;
    [SerializeField] private float vfxDuration = 0.5f;

    [SerializeField] private PlayerLaunchFeedback launchFeedback;

    private PlayerBallController controller;
    private AugmentSkillController augmentSkills;
    private DashHud hud;
    private PlayerPerfectBounce perfectBounce;

    private int maxCharges;
    private float rechargeTime;
    private float charges;

    private bool isAiming;
    private bool isHolding;  //눌렀지만 아직 탭인지 대쉬인지 모름
    private float holdTimer;
    private Vector3 dragStart;
    private float aimTimer;

    private void Awake()
    {
        controller = GetComponent<PlayerBallController>();
        augmentSkills = GetComponentInChildren<AugmentSkillController>();
        if (launchFeedback == null) launchFeedback = GetComponentInChildren<PlayerLaunchFeedback>();
    }

    private void Start()
    {
        int capacityLevel = SaveManager.Instance != null ? SaveManager.Instance.GetUpgradeLevel(ShopItemType.DashCapacity) : 0;
        int rechargeLevel = SaveManager.Instance != null ? SaveManager.Instance.GetUpgradeLevel(ShopItemType.DashRecharge) : 0;

        maxCharges = baseCharges + capacityLevel;
        rechargeTime = baseRechargeTime / (1f + rechargeSpeedPerLevel * rechargeLevel);
        charges = maxCharges;
        perfectBounce = GetComponent<PlayerPerfectBounce>();  //PlayerBallController.Awake에서 붙음

        if (chargeVfxPrefab != null && ObjectPoolManager.Instance != null) ObjectPoolManager.Instance.RegisterPool(chargeVfxPrefab, 2);

        hud = DashHud.Create();
        hud?.SetGauge(charges, maxCharges);
    }

    private void Update()
    {
        GameState state = GameManager.Instance != null ? GameManager.Instance.currentState : GameState.Ready;

        if (isAiming)
        {
            if (state != GameState.Playing)  //일시정지 등: 시간은 일시정지 메뉴가 맡음
            {
                EndAim(false, false, Vector3.zero, state != GameState.Paused);
                return;
            }

            UpdateAim();
            return;
        }

        if (state == GameState.Playing && !TimeFreeze.IsActive && charges < maxCharges)
        {
            charges = Mathf.Min(maxCharges, charges + Time.deltaTime / rechargeTime);
        }

        hud?.SetGauge(charges, maxCharges);

        if (isHolding)
        {
            UpdateHold(state);
            return;
        }

        if (state != GameState.Playing || !Input.GetMouseButtonDown(0)) return;
        if (!IsInInputZone(Input.mousePosition) || IsPointerOverUI()) return;
        if (IsSkillActive()) return;  //스킬(캐릭터/증강)이 동작하는 동안은 대쉬 불가

        isHolding = true;  //짧게 떼면 탭(퍼펙트 반사), 길게 누르거나 끌면 대쉬
        dragStart = Input.mousePosition;
        holdTimer = 0f;
    }

    private void UpdateHold(GameState state)
    {
        if (state != GameState.Playing || !Input.GetMouseButton(0))  //짧은 탭으로 끝남
        {
            isHolding = false;
            return;
        }

        holdTimer += Time.unscaledDeltaTime;
        bool dragged = (Input.mousePosition - dragStart).magnitude >= minDragDistance;
        if (holdTimer < holdThreshold && !dragged) return;

        isHolding = false;

        if (charges < 1f || IsSkillActive())  //게이지 부족: 대쉬 없이 탭(퍼펙트 반사)으로 처리
        {
            hud?.PunchGauge();
            return;
        }

        perfectBounce?.NotifyDashStarted();
        BeginAim();
    }

    public void RefillCharges()  //전투 튜토리얼: 대쉬를 배우는 동안 게이지가 비어 막히지 않도록
    {
        charges = maxCharges;
        hud?.SetGauge(charges, maxCharges);
    }

    private void BeginAim()
    {
        isAiming = true;
        aimTimer = 0f;

        OnAimStarted?.Invoke();
        TimeFreeze.Begin();  //적과 플레이어 모두 멈춤
        hud?.ShowAim();
        HapticFeedback.Light();
    }

    private void UpdateAim()
    {
        aimTimer += Time.unscaledDeltaTime;
        hud?.SetAimProgress(aimTimer / graceDuration);

        Vector3 drag = Input.mousePosition - dragStart;  //드래그한 방향 그대로 돌진

        if (drag.magnitude > 5f) launchFeedback?.ShowPull(transform.position, GetWorldDirection(drag), Mathf.Clamp01(drag.magnitude / aimGuideMaxDrag));
        else launchFeedback?.Cancel();

        if (aimTimer >= graceDuration)  //유예 시간 초과: 게이지만 소비
        {
            EndAim(true, false, Vector3.zero, true);
            return;
        }

        if (!Input.GetMouseButtonUp(0)) return;

        if (drag.magnitude < minDragDistance) EndAim(false, false, Vector3.zero, true);  //거의 안 끌었으면 취소 (게이지 유지)
        else EndAim(true, true, GetWorldDirection(drag), true);
    }

    private void EndAim(bool consume, bool dash, Vector3 direction, bool restoreTime)
    {
        isAiming = false;
        hud?.HideAim();

        if (consume) charges = Mathf.Max(0f, charges - 1f);
        hud?.SetGauge(charges, maxCharges);

        TimeFreeze.End(restoreTime);

        if (!dash)
        {
            launchFeedback?.Cancel();
            return;
        }

        launchFeedback?.Release();
        controller.StartDash(direction, dashSpeedMultiplier, dashDuration);
        OnDashPerformed?.Invoke();
        PlayImpact(direction);
    }

    private void PlayImpact(Vector3 direction)
    {
        SoundManager.Instance?.PlaySFX(SoundId.PlayerDash);
        HapticFeedback.Medium();

        if (chargeVfxPrefab == null || VFXManager.Instance == null) return;

        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(vfxRotationOffset);
        GameObject vfx = VFXManager.Instance.PlayAttached(chargeVfxPrefab, transform, Vector3.zero, Quaternion.identity, vfxDuration);
        if (vfx != null) vfx.transform.rotation = rotation;
    }

    private Vector3 GetWorldDirection(Vector3 screenDrag)  //화면 드래그를 카메라 기준 바닥 방향으로
    {
        Transform cam = Camera.main.transform;

        Vector3 forward = cam.forward;
        Vector3 right = cam.right;
        forward.y = 0f;
        right.y = 0f;

        return (right.normalized * screenDrag.x + forward.normalized * screenDrag.y).normalized;
    }

    private bool IsInInputZone(Vector3 screenPosition)  //실제 게임 화면(세로 비율 고정 영역) 기준 아래쪽의 가운데~왼쪽
    {
        Rect area = Camera.main != null ? Camera.main.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height);

        float x = (screenPosition.x - area.x) / area.width;
        float y = (screenPosition.y - area.y) / area.height;

        return x >= 0f && x <= inputZoneWidth && y >= 0f && y <= inputZoneHeight;
    }

    private bool IsSkillActive()
    {
        if (controller != null && controller.IsInSkillMotion) return true;  //돼지 돌진, 토끼 점프, 스킬 준비, 대쉬 돌진 중
        if (TimeSlowController.IsActive) return true;  //산데비스탄 시간 감속
        return augmentSkills != null && augmentSkills.IsSkillBusy;  //산데비스탄, 수호 별 확장 등
    }

    private bool IsPointerOverUI()  //스킬 버튼 등 UI 위에서 시작한 터치는 대쉬로 받지 않음
    {
        if (EventSystem.current == null) return false;

        if (Input.touchCount > 0) return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);

        return EventSystem.current.IsPointerOverGameObject();
    }

    private void OnDisable()  //조준 중 꺼져도 시간이 멈춘 채로 남지 않게
    {
        isHolding = false;
        if (!isAiming) return;

        isAiming = false;
        TimeFreeze.End();
        hud?.HideAim();
    }
}
