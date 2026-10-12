using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 스테이지 씬에서 자동으로 생겨 전투 이벤트를 모은다.
// - 오늘의 미션 진행도 (처치, 방 클리어, 보스, 뱅크샷, 퍼펙트 반사, 콤보, 스킬)
// - 도감: 처음 만난 적 기록
// - 보스 등장 연출 (이름 띠 + 카메라 줌인), 보스 첫 처치 보상
// - 분석 이벤트
public class StageProgressTracker : MonoBehaviour
{
    public const int BossFirstKillReward = 300;

    private static readonly int ComboTarget = 10;

    private Coroutine bossIntroRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded += (scene, mode) => Spawn();
        Spawn();
    }

    private static void Spawn()
    {
        if (FindAnyObjectByType<RoomManager>() == null) return;  //스테이지 씬에서만
        if (FindAnyObjectByType<StageProgressTracker>() != null) return;

        new GameObject("StageProgressTracker").AddComponent<StageProgressTracker>();
    }

    private void OnEnable()
    {
        GameEvents.EnemyKilled += HandleEnemyKilled;
        GameEvents.EnemySpawned += HandleEnemySpawned;
        GameEvents.RoomCleared += HandleRoomCleared;
        GameEvents.StageCleared += HandleStageCleared;
        GameEvents.ComboChanged += HandleComboChanged;
        GameEvents.BankShot += HandleBankShot;
        GameEvents.SkillUsed += HandleSkillUsed;
        PlayerPerfectBounce.OnPerfect += HandlePerfect;
    }

    // 전투 튜토리얼: 보스 등장 연출만 하고 미션/도감/보스 첫 처치 보상/분석에는 넣지 않음
    private static bool InTutorial => TutorialMode.IsActive;

    private void Start()
    {
        if (InTutorial) return;

        GameAnalytics.Record("run_started",
            ("stage", GameSessionData.SelectedStage != null ? GameSessionData.SelectedStage.stageNumber : 0),
            ("endless", GameSessionData.IsEndless ? 1 : 0),
            ("character", SaveManager.Instance != null ? SaveManager.Instance.Data.selectedCharacterId.ToString() : "unknown"),
            ("ascension", RunState.Ascension));
    }

    private void OnDisable()
    {
        GameEvents.EnemyKilled -= HandleEnemyKilled;
        GameEvents.EnemySpawned -= HandleEnemySpawned;
        GameEvents.RoomCleared -= HandleRoomCleared;
        GameEvents.StageCleared -= HandleStageCleared;
        GameEvents.ComboChanged -= HandleComboChanged;
        GameEvents.BankShot -= HandleBankShot;
        GameEvents.SkillUsed -= HandleSkillUsed;
        PlayerPerfectBounce.OnPerfect -= HandlePerfect;

        SaveManager.Instance?.Save();  //씬을 나갈 때 쌓인 진행도 저장
    }

    private void HandleEnemyKilled(EnemyController enemy, bool isBoss)
    {
        if (InTutorial) return;

        DailyRewardService.AddProgress("kill_enemies");
        CountKill(enemy);

        if (!isBoss) return;

        DailyRewardService.AddProgress("defeat_boss");

        string bossId = enemy != null && enemy.Data != null ? enemy.Data.name : null;
        GameAnalytics.Record("boss_defeated", ("boss_id", bossId ?? "unknown"), ("room", RoomManager.Instance != null ? RoomManager.Instance.CurrentRoom : 0));

        // 보스 첫 처치 보상
        PlayerSaveData data = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
        if (data == null || string.IsNullOrEmpty(bossId) || data.defeatedBosses.Contains(bossId)) return;

        data.defeatedBosses.Add(bossId);
        SaveManager.Instance.AddGold(BossFirstKillReward);
        SaveManager.Instance.Save();

        StartCoroutine(ShowFirstKillBanner(enemy.EnemyName));
    }

    // 도감 처치 수 (저장은 방 클리어/씬 나갈 때)
    private static void CountKill(EnemyController enemy)
    {
        if (enemy == null || enemy.Data == null || SaveManager.Instance == null) return;

        string enemyId = enemy.Data.name;
        List<EnemyKillCountData> counts = SaveManager.Instance.Data.enemyKillCounts;
        EnemyKillCountData entry = counts.Find(item => item.enemyId == enemyId);
        if (entry == null)
        {
            entry = new EnemyKillCountData { enemyId = enemyId };
            counts.Add(entry);
        }

        entry.count++;
    }

    private IEnumerator ShowFirstKillBanner(string bossName)
    {
        yield return new WaitForSecondsRealtime(1.6f);  //처치 연출이 끝난 뒤
        HudBanner.Show(LobbyUiKit.Loc("boss_first_kill_title", "첫 처치 보상!"), string.Format(LobbyUiKit.Loc("boss_first_kill_format", "{0} +{1} 골드"), bossName, BossFirstKillReward));
    }

    private void HandleEnemySpawned(EnemyController enemy)
    {
        if (enemy == null || enemy.Data == null || SaveManager.Instance == null) return;

        PlayerSaveData data = SaveManager.Instance.Data;
        string enemyId = enemy.Data.name;
        if (!InTutorial && !data.encounteredEnemies.Contains(enemyId)) data.encounteredEnemies.Add(enemyId);  //저장은 방 클리어 때

        if (enemy.Data.attackType == EnemyAttackType.Boss)
        {
            if (bossIntroRoutine != null) StopCoroutine(bossIntroRoutine);
            bossIntroRoutine = StartCoroutine(BossIntroRoutine(enemy));
        }
    }

    // 보스 등장: 이름 띠 + 카메라가 살짝 당겨졌다 돌아옴
    private IEnumerator BossIntroRoutine(EnemyController boss)
    {
        yield return null;  //로딩 화면이 걷힐 때까지
        while (LoadingScreenManager.Instance != null && LoadingScreenManager.Instance.IsLoading) yield return null;
        while (RunPopupUI.IsOpen) yield return null;

        HudBanner.Show(LobbyUiKit.Loc("boss_intro_title", "BOSS"), boss != null ? boss.EnemyName : "", 2f);

        Camera camera = Camera.main;
        if (camera == null || camera.orthographic) yield break;

        float baseFov = camera.fieldOfView;
        float zoomFov = baseFov * 0.82f;
        const float inTime = 0.35f;
        const float holdTime = 0.6f;
        const float outTime = 0.5f;

        float timer = 0f;
        while (timer < inTime + holdTime + outTime)
        {
            if (camera == null) yield break;
            if (GameManager.Instance != null && GameManager.Instance.currentState == GameState.Playing) break;  //발사하면 바로 원래대로

            float fov;
            if (timer < inTime) fov = Mathf.Lerp(baseFov, zoomFov, Ease(timer / inTime));
            else if (timer < inTime + holdTime) fov = zoomFov;
            else fov = Mathf.Lerp(zoomFov, baseFov, Ease((timer - inTime - holdTime) / outTime));

            camera.fieldOfView = fov;
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        if (camera != null) camera.fieldOfView = baseFov;
        bossIntroRoutine = null;
    }

    private static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void HandleRoomCleared(int roomNumber, bool isEndless)
    {
        DailyRewardService.AddProgress("clear_rooms");
        SaveManager.Instance?.Save();

        GameAnalytics.Record("room_cleared", ("room", roomNumber), ("endless", isEndless ? 1 : 0));
    }

    private void HandleStageCleared(int stageNumber)
    {
        GameAnalytics.Record("stage_cleared", ("stage", stageNumber), ("ascension", RunState.Ascension));
    }

    private void HandleComboChanged(int combo)
    {
        if (!InTutorial && combo >= ComboTarget) DailyRewardService.SetProgressAtLeast("combo", combo);
    }

    private void HandleBankShot() { if (!InTutorial) DailyRewardService.AddProgress("bank_shot"); }
    private void HandleSkillUsed() { if (!InTutorial) DailyRewardService.AddProgress("use_skill"); }
    private void HandlePerfect(Vector3 position, int chain) { if (!InTutorial) DailyRewardService.AddProgress("perfect_bounce"); }
}
