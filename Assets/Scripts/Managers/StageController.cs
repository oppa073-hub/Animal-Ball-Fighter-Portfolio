using UnityEngine;
using UnityEngine.SceneManagement;

public class StageController : MonoBehaviour
{
    private StageData currentStage;

    public int TotalRooms => currentStage != null ? currentStage.totalRooms : 0;

    private void Start()
    {
        currentStage = GameSessionData.SelectedStage;

        if (currentStage == null)
        {
            Debug.LogWarning("[Stage] 선택된 스테이지 데이터가 없습니다.");
            return;
        }

        Debug.Log($"[Stage] {currentStage.stageName} 시작");
    }
    public void ReturnToLobby()
    {
        RoomManager.Instance?.ClaimEndlessRunGold();
        LoadSceneWithLoading("MainLobby", false);
    }
    public void StartNextStage()
    {
        if (currentStage == null || currentStage.nextStage == null) return;

        StageData nextStage = currentStage.nextStage;

        GameSessionData.SetStage(nextStage);

        LoadSceneWithLoading(nextStage.sceneName);
    }
    public void RetryStage()
    {
        if (GameSessionData.IsEndless)
        {
            RetryEndless();
            return;
        }

        if (currentStage == null) return;

        GameSessionData.SetStage(currentStage);

        LoadSceneWithLoading(currentStage.sceneName);
    }
    private void RetryEndless()
    {
        RoomManager.Instance?.ClaimEndlessRunGold();
        GameSessionData.ResetEndlessRunGold();

        StageData randomStage = StageDatabase.Instance.GetRandomNormalStage();

        if (randomStage == null) return;

        GameSessionData.SetEndless(true);
        GameSessionData.SetStage(randomStage);

        Debug.Log($"[Endless] 재도전 맵 : {randomStage.stageName}");

        LoadSceneWithLoading(randomStage.sceneName);
    }

    public void UnlockNextStage()
    {
        if (currentStage == null || currentStage.nextStage == null)
            return;

        int nextStageNumber = currentStage.nextStage.stageNumber;

        if (SaveManager.Instance.Data.highestUnlockedStage < nextStageNumber)
        {
            SaveManager.Instance.Data.highestUnlockedStage = nextStageNumber;
            SaveManager.Instance.Save();

            Debug.Log($"[Save] Stage {nextStageNumber} 해금");
        }
    }
    private void LoadSceneWithLoading(string sceneName, bool waitForSceneReady = true)
    {
        if (LoadingScreenManager.Instance != null)
        {
            LoadingScreenManager.Instance.LoadScene(sceneName, waitForSceneReady);
            return;
        }

        // Bootstrap을 거치지 않고 스테이지 씬을 직접 테스트할 때만 사용
        Debug.LogWarning($"[Loading] LoadingScreenManager가 없어 직접 로드합니다: {sceneName}");

        SceneManager.LoadScene(sceneName);
    }
}