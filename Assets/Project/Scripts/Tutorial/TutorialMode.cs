using UnityEngine;
using UnityEngine.SceneManagement;

// 전투 튜토리얼 시작/판정. 튜토리얼 스테이지 데이터(Resources/TutorialStage, isTutorial)로 1스테이지 맵을 연다.
// 진행 연출은 CombatTutorialGuide, 방 구성은 EnemySpawner.SpawnTutorialRoom이 맡는다.
// 튜토리얼 중에는 기록/해금/골드/축복/갈림길이 없고 플레이어가 쓰러지지 않는다.
public static class TutorialMode
{
    public const string StageResourcePath = "TutorialStage";
    public const int CompleteGoldReward = 200;

    public static bool IsActive => !GameSessionData.IsEndless && GameSessionData.SelectedStage != null && GameSessionData.SelectedStage.isTutorial;

    // 처음 하는 유저가 스테이지 1에 도전하면 튜토리얼로 보냄 (한 판이라도 해 본 유저는 제외)
    public static bool ShouldAutoStart(StageData selected)
    {
        if (selected == null || selected.isEndless || selected.stageNumber != 1) return false;
        if (SaveManager.Instance == null || SaveManager.Instance.Data == null) return false;
        if (TutorialProgress.IsDone(TutorialStep.CombatTutorial)) return false;

        return SaveManager.Instance.Data.stageRecords == null || SaveManager.Instance.Data.stageRecords.Count == 0;
    }

    public static bool Start()
    {
        StageData stage = Resources.Load<StageData>(StageResourcePath);
        if (stage == null)
        {
            Debug.LogWarning("[Tutorial] Resources/TutorialStage가 없어 튜토리얼을 시작하지 못했습니다.");
            return false;
        }

        GameSessionData.SetEndless(false);
        GameSessionData.SetStage(stage);

        if (LoadingScreenManager.Instance != null) LoadingScreenManager.Instance.LoadScene(stage.sceneName, true);
        else SceneManager.LoadScene(stage.sceneName);

        return true;
    }

    public static void ExitToLobby()  //도중에 나가기: 다시 자동으로 시작하지 않도록 본 것으로 기록
    {
        TutorialProgress.MarkDone(TutorialStep.CombatTutorial);
        LeaveStage();
    }

    // 모두 마침: 처음이면 골드 보상. 지급한 골드(없으면 0)를 돌려준다
    public static int Complete()
    {
        TutorialProgress.MarkDone(TutorialStep.CombatTutorial);
        if (TutorialProgress.IsDone(TutorialStep.CombatTutorialReward) || SaveManager.Instance == null) return 0;

        TutorialProgress.MarkDone(TutorialStep.CombatTutorialReward);
        SaveManager.Instance.AddGold(CompleteGoldReward);
        return CompleteGoldReward;
    }

    public static void LeaveStage()
    {
        GameSessionData.SetStage(null);  //로비에서 다른 시스템이 튜토리얼 스테이지로 착각하지 않도록

        if (LoadingScreenManager.Instance != null) LoadingScreenManager.Instance.LoadScene("MainLobby", false);
        else SceneManager.LoadScene("MainLobby");
    }
}
