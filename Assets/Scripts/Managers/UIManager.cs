using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] private BossHealthUI bossHealthUI;
    [SerializeField] private StageUI stageUI;
    [SerializeField] private PlayerHealthUI playerHealthUI;
    [SerializeField] private SkillUIContainer playerSkillUI;
    [SerializeField] private ShieldBarUI shieldBarUI;
    [SerializeField] private AugmentPanelUI augmentPanelUI;
    [SerializeField] private GameObject stageClearPanel;
    [SerializeField] private TMP_Text stageClearStageNameText;
    [SerializeField] private TMP_Text stageClearRecordText;
    [SerializeField] private GameObject nextStageButton;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text gameOverStageNameText;
    [SerializeField] private TMP_Text gameOverReachedRoomText;
    [SerializeField] private TMP_Text stageClearRewardText;
    [SerializeField] private TMP_Text gameOverRewardText;
    [SerializeField] private GameObject gameOverBestRecordGroup;
    [SerializeField] private TMP_Text gameOverBestRecordText;
    [SerializeField] private TMP_Text endlessRunGoldText;
    [SerializeField] private TMP_Text endlessBestRoomText;
    [SerializeField] private GameObject rewardedReviveButton;

    [Header("Localization")]
    [SerializeField] private LocalizedString clearedRoomsFormat;
    [SerializeField] private LocalizedString roomNumberFormat;
    [SerializeField] private LocalizedString goldRewardFormat;
    [SerializeField] private LocalizedString endlessGoldFormat;
    [SerializeField] private LocalizedString endlessBestFormat;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ShowBossHealth(Health health, EnemyController enemy)
    {
        bossHealthUI.Initialize(health, enemy);
        bossHealthUI.gameObject.SetActive(true);
    }

    public void HideBossHealth()
    {
        bossHealthUI.gameObject.SetActive(false);
    }

    public void ShowStageUI(RoomType type, int roomNum)
    {
        stageUI.Initialize(type, roomNum);
        stageUI.gameObject.SetActive(true);
    }

    public void HideStageUI()
    {
        stageUI.gameObject.SetActive(false);
    }

    public void ShowPlayerHealth(Health health)
    {
        playerHealthUI.gameObject.SetActive(true);
        playerHealthUI.Initialize(health);
    }

    public void HidePlayerHealth()
    {
        playerHealthUI.gameObject.SetActive(false);
    }
    public void ShowPlayerSkillUI(PlayerSkill skill)
    {
        playerSkillUI.gameObject.SetActive(true);
        playerSkillUI.RegisterSkill(skill);
    }
    public void HidePlayerSkillUI()
    {
        playerSkillUI.gameObject.SetActive(false);
    }

    public void ShowPlayerShield(PlayerShield shield)
    {
        shieldBarUI.gameObject.SetActive(true);
        shieldBarUI.Initialize(shield);
    }
    public void HidePlayerShield()
    {
        shieldBarUI.gameObject.SetActive(false);
    }

    public void ShowAugmentChoices(List<AugmentData> choices)
    {
        augmentPanelUI.Show(choices);
    }
    public void HideAugmentChoices(System.Action onComplete)
    {
        augmentPanelUI.Hide(onComplete);
    }
    public void ShowStageClear(StageData stage, int clearedRooms, int earnedGold)
    {
        stageClearPanel.SetActive(true);

        stageClearStageNameText.text = stage.localizedStageName.GetLocalizedString();

        stageClearRecordText.text = clearedRoomsFormat.GetLocalizedString(clearedRooms);

        stageClearRewardText.text = goldRewardFormat.GetLocalizedString(earnedGold);

        nextStageButton.SetActive(stage.nextStage != null);
    }
    public void ShowGameOver(StageData stage, int reachedRoom, int earnedGold)
    {
        bool canRevive = RoomManager.Instance != null && RoomManager.Instance.CanUseRewardedRevive;

        HidePlayerHealth();
        HidePlayerSkillUI();
        HideStageUI();
        HideBossHealth();
        HidePlayerShield();

        rewardedReviveButton.SetActive(canRevive);

        gameOverPanel.SetActive(true);
        gameOverReachedRoomText.text = roomNumberFormat.GetLocalizedString(reachedRoom);

        if (GameSessionData.IsEndless)
        {
            gameOverStageNameText.text = "ENDLESS";

            gameOverRewardText.gameObject.SetActive(true);
            gameOverBestRecordGroup.SetActive(true);

            gameOverRewardText.text = goldRewardFormat.GetLocalizedString(earnedGold);

            gameOverBestRecordText.text = roomNumberFormat.GetLocalizedString(SaveManager.Instance.Data.bestEndlessRoom);
        }
        else
        {
            gameOverStageNameText.text = stage.localizedStageName.GetLocalizedString();

            gameOverRewardText.gameObject.SetActive(false);
            gameOverBestRecordGroup.SetActive(false);
        }
    }
    public void HideGameOverForRevive()
    {
        gameOverPanel.SetActive(false);
        playerHealthUI.gameObject.SetActive(true);
        playerSkillUI.gameObject.SetActive(true);
        stageUI.gameObject.SetActive(true);
        shieldBarUI.gameObject.SetActive(true);

        if (RoomManager.Instance != null &&
            RoomManager.Instance.RoomType == RoomType.Boss)
        {
            bossHealthUI.gameObject.SetActive(true);
        }
    }

    public void RefreshEndlessHUD()
    {
        bool isEndless = GameSessionData.IsEndless;

        endlessRunGoldText.gameObject.SetActive(isEndless);
        endlessBestRoomText.gameObject.SetActive(isEndless);

        if (!isEndless) return;

        endlessRunGoldText.text = endlessGoldFormat.GetLocalizedString(GameSessionData.EndlessRunGold);

        endlessBestRoomText.text = endlessBestFormat.GetLocalizedString(SaveManager.Instance.Data.bestEndlessRoom);
    }
    public void RegisterAugmentSkill(AugmentSkillRuntime skill)
    {
        playerSkillUI.RegisterSkill(skill);
    }
}
