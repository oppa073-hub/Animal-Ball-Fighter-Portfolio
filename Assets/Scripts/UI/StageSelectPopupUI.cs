using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Localization;
public class StageSelectPopupUI : MonoBehaviour
{
    [SerializeField] private GameObject root;

    [Header("Stage Data")]
    [SerializeField] private StageData[] stages;

    [Header("UI")]
    [SerializeField] private Image stageImage;
    [SerializeField] private TMP_Text stageNumberText;
    [SerializeField] private TMP_Text stageNameText;
    [SerializeField] private TMP_Text difficultyText;
    [SerializeField] private TMP_Text bestRecordText;
    [SerializeField] private Image[] pageDots;
    [SerializeField] private Color selectedDotColor;
    [SerializeField] private Color normalDotColor;
    [SerializeField] private GameObject lockOverlay;
    [SerializeField] private TMP_Text lockText;
    [SerializeField] private Button challengeButton;
    //최고 점수뭐시기는 나중에 

    [Header("Localization")]
    [SerializeField] private LocalizedString difficultyFormatText;
    [SerializeField] private LocalizedString bestNoneText;
    [SerializeField] private LocalizedString bestRoomText;
    [SerializeField] private LocalizedString bestClearText;

    private int currentIndex;

    public StageData CurrentStage => stages[currentIndex];

    public void Open()
    {
        root.SetActive(true);

        currentIndex = 0;
        Refresh();
    }

    public void Close()
    {
        root.SetActive(false);
    }

    private void Refresh()
    {
        StageData data = stages[currentIndex];

        stageImage.sprite = data.stageImage;
        stageNumberText.text = $"STAGE {data.stageNumber}";
        stageNameText.text = data.localizedStageName.GetLocalizedString();

        string localizedDifficulty = data.localizedDifficulty.GetLocalizedString();
        difficultyText.text = difficultyFormatText.GetLocalizedString(localizedDifficulty);

        RefreshPageIndicator();

        if (data.isEndless)
        {
            int bestRoom = SaveManager.Instance.Data.bestEndlessRoom;

            bestRecordText.text =
                bestRoom > 0
                ? bestRoomText.GetLocalizedString(bestRoom)
                : bestNoneText.GetLocalizedString();
        }
        else
        {
            StageRecordData record = SaveManager.Instance.GetStageRecord(data.stageNumber);

            if (record == null)
            {
                bestRecordText.text = bestNoneText.GetLocalizedString();
            }
            else if (record.isCleared)
            {
                bestRecordText.text = bestClearText.GetLocalizedString();
            }
            else
            {
                bestRecordText.text = bestRoomText.GetLocalizedString(record.bestReachedRoom);
            }
        }

        bool isUnlocked;

        if (data.isEndless)
        {
            isUnlocked = SaveManager.Instance.IsEndlessUnlocked();
        }
        else
        {
            isUnlocked = data.stageNumber <= SaveManager.Instance.Data.highestUnlockedStage;
        }

        lockOverlay.SetActive(!isUnlocked);
        challengeButton.interactable = isUnlocked;
    }
    public void SelectCurrentStage()
    {
        StageData selectedStage = stages[currentIndex];

        if (selectedStage.isEndless)
        {
            StartEndless();
            return;
        }

        GameSessionData.SetEndless(false);
        GameSessionData.SetStage(selectedStage);

        LoadSceneWithLoading(selectedStage.sceneName);
    }
    private void StartEndless()
    {
        StageData randomStage = StageDatabase.Instance.GetRandomNormalStage();

        if (randomStage == null) return;

        GameSessionData.ResetEndlessRunGold();
        GameSessionData.SetEndless(true);
        GameSessionData.SetStage(randomStage);

        Debug.Log($"[Endless] 랜덤 맵 선택 : {randomStage.stageName}");

        LoadSceneWithLoading(randomStage.sceneName);
    }

    public void ShowPreviousStage()
    {
        currentIndex--;

        if (currentIndex < 0)
        {
            currentIndex = stages.Length - 1;
        }

        Refresh();
    }

    public void ShowNextStage()
    {
        currentIndex++;

        if (currentIndex >= stages.Length)
        {
            currentIndex = 0;
        }

        Refresh();
    }
    private void RefreshPageIndicator()
    {
        for (int i = 0; i < pageDots.Length; i++)
        {
            pageDots[i].color = i == currentIndex ? selectedDotColor : normalDotColor;
        }
    }
    private void LoadSceneWithLoading(string sceneName)
    {
        if (LoadingScreenManager.Instance != null)
        {
            LoadingScreenManager.Instance.LoadScene(sceneName, true);
            return;
        }

        // MainLobby 씬을 직접 실행했을 때 사용하는 예비 처리
        Debug.LogWarning($"[Loading] LoadingScreenManager가 없어 직접 로드합니다: {sceneName}");

        SceneManager.LoadScene(sceneName);
    }
    public void RefreshLocalizedUI()
    {
        if (stages == null || stages.Length == 0) return;

        if (currentIndex < 0 || currentIndex >= stages.Length)
        {
            currentIndex = 0;
        }

        Refresh();
    }
}