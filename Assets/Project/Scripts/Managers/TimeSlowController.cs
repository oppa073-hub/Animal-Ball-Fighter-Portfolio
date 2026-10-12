using UnityEngine;

// 산데비스탄 시간 감속. 게임 전체 시간을 느리게 하고, "가속 중인 쪽"만 속도를 보정해 원래 속도로 움직이게 한다.
// - 플레이어 산데비스탄(증강 스킬): 적/보스/투사체가 느려지고 플레이어만 원래 속도
// - 보스 산데비스탄(로봇 보스 패턴1): 플레이어와 다른 적이 느려지고 그 보스만 원래 속도
// - 둘이 겹치면(충돌): 두 쪽 모두 원래 속도로 움직이고 나머지만 느려진다. 겹치는 순간 OnClash가 불린다.
// 일시정지(PauseMenuUI가 timeScale 0/1을 직접 씀)와 겹쳐도 풀린 뒤 다시 적용하고, 전투가 끝나면 즉시 복구한다.
public class TimeSlowController : MonoBehaviour
{
    private static TimeSlowController instance;

    private const float DefaultFixedDeltaTime = 0.02f;

    private float baseFixedDeltaTime = DefaultFixedDeltaTime;
    private float slowScale = 1f;
    private bool isActive;

    private float playerRemaining;
    private float playerScale = 1f;
    private float bossRemaining;
    private float bossScale = 1f;
    private Transform acceleratedBoss;

    public static event System.Action OnClash;

    public static bool IsActive => instance != null && instance.isActive;
    public static bool IsPlayerAccelerated => IsActive && instance.playerRemaining > 0f;
    public static bool IsBossAccelerated => IsActive && instance.bossRemaining > 0f;
    public static bool IsClashing => IsPlayerAccelerated && IsBossAccelerated;

    private static bool IsSlowApplied => IsActive && Time.timeScale > 0f && Mathf.Approximately(Time.timeScale, instance.slowScale);

    // 플레이어 속도 보정 배율 (플레이어가 가속 중이면 1/timeScale, 아니면 1 → 보스 산데비스탄 중에는 플레이어도 느려짐)
    public static float PlayerCompensation => IsPlayerAccelerated && IsSlowApplied ? 1f / instance.slowScale : 1f;

    // 적 속도 보정 배율 (산데비스탄을 쓴 그 보스만 1/timeScale)
    public static float GetEnemyCompensation(Transform enemy)
    {
        if (!IsBossAccelerated || !IsSlowApplied || enemy == null || instance.acceleratedBoss != enemy) return 1f;
        return 1f / instance.slowScale;
    }

    // 플레이어 산데비스탄
    public static void Begin(float timeScale, float realDuration)
    {
        EnsureInstance().BeginInternal(true, timeScale, realDuration, null);
    }

    // 보스 산데비스탄
    public static void BeginBoss(Transform boss, float timeScale, float realDuration)
    {
        EnsureInstance().BeginInternal(false, timeScale, realDuration, boss);
    }

    public static void End()  //전부 즉시 해제 (보스 처치 연출 등)
    {
        if (instance != null) instance.EndInternal();
    }

    public static void EndPlayer()
    {
        if (instance != null) instance.EndOwner(true);
    }

    public static void EndBoss(Transform boss)
    {
        if (instance != null && (boss == null || instance.acceleratedBoss == boss)) instance.EndOwner(false);
    }

    private static TimeSlowController EnsureInstance()
    {
        if (instance == null)
        {
            instance = new GameObject("TimeSlowController").AddComponent<TimeSlowController>();
        }

        return instance;
    }

    private void BeginInternal(bool isPlayer, float timeScale, float realDuration, Transform boss)
    {
        if (!isActive) baseFixedDeltaTime = Time.timeScale > 0f && Mathf.Approximately(Time.timeScale, 1f) ? Time.fixedDeltaTime : DefaultFixedDeltaTime;

        bool otherSideActive = isPlayer ? bossRemaining > 0f : playerRemaining > 0f;
        float scale = Mathf.Clamp(timeScale, 0.02f, 1f);

        if (isPlayer)
        {
            playerScale = scale;
            playerRemaining = Mathf.Max(playerRemaining, realDuration);
        }
        else
        {
            bossScale = scale;
            bossRemaining = Mathf.Max(bossRemaining, realDuration);
            acceleratedBoss = boss;
        }

        isActive = true;
        RefreshScale();
        Apply();

        if (otherSideActive) OnClash?.Invoke();
    }

    private void RefreshScale()
    {
        float scale = 1f;
        if (playerRemaining > 0f) scale = Mathf.Min(scale, playerScale);
        if (bossRemaining > 0f) scale = Mathf.Min(scale, bossScale);
        slowScale = scale;
    }

    private void Update()
    {
        if (!isActive) return;
        if (TimeFreeze.IsActive) return;  //대쉬 조준으로 시간이 멈춘 동안은 감속을 다시 걸지 않고 시간도 세지 않음

        GameState state = GameManager.Instance != null ? GameManager.Instance.currentState : GameState.Playing;

        if (state == GameState.Paused)
        {
            // 일시정지 메뉴에서 로비로 나가는 경우 등: 상태는 Paused인데 timeScale이 1로 복구됨 → 물리 스텝도 바로 복구
            if (Mathf.Approximately(Time.timeScale, 1f)) EndInternal();
            return;  //일시정지 중에는 시간을 세지 않음
        }

        if (state != GameState.Playing)
        {
            EndInternal();  //방 클리어, 게임오버, 증강 선택 등
            return;
        }

        if (playerRemaining > 0f) playerRemaining -= Time.unscaledDeltaTime;
        if (bossRemaining > 0f) bossRemaining -= Time.unscaledDeltaTime;

        if (playerRemaining <= 0f && bossRemaining <= 0f)
        {
            EndInternal();
            return;
        }

        RefreshScale();
        if (!Mathf.Approximately(Time.timeScale, slowScale)) Apply();  //일시정지 해제(timeScale = 1) 후, 또는 한쪽이 끝나 배율이 바뀐 경우 다시 적용
    }

    private void Apply()
    {
        Time.timeScale = slowScale;
        Time.fixedDeltaTime = baseFixedDeltaTime * slowScale;  //물리 스텝 수를 실제 시간 기준으로 유지 (가속 중인 쪽이 끊기거나 벽을 뚫지 않게)
    }

    private void EndOwner(bool isPlayer)
    {
        if (!isActive) return;

        if (isPlayer) playerRemaining = 0f;
        else
        {
            bossRemaining = 0f;
            acceleratedBoss = null;
        }

        if (playerRemaining <= 0f && bossRemaining <= 0f)
        {
            EndInternal();
            return;
        }

        RefreshScale();
        bool isPaused = GameManager.Instance != null && GameManager.Instance.currentState == GameState.Paused;
        if (!isPaused) Apply();
    }

    private void EndInternal()
    {
        if (!isActive) return;

        isActive = false;
        playerRemaining = 0f;
        bossRemaining = 0f;
        acceleratedBoss = null;

        Time.fixedDeltaTime = baseFixedDeltaTime;

        bool isPaused = GameManager.Instance != null && GameManager.Instance.currentState == GameState.Paused;
        if (!isPaused) Time.timeScale = 1f;  //일시정지 중이면 PauseMenuUI가 재개할 때 1로 돌림
    }

    private void OnDestroy()
    {
        // 씬 전환 등으로 사라질 때 감속이 남지 않도록 복구
        if (isActive)
        {
            Time.fixedDeltaTime = baseFixedDeltaTime;
            if (Mathf.Approximately(Time.timeScale, slowScale)) Time.timeScale = 1f;
            isActive = false;
        }

        if (instance == this) instance = null;
    }
}
