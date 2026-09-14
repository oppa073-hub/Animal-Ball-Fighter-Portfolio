using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "StageData", menuName = "Game/Stage Data")]
public class StageData : ScriptableObject
{
    public int stageNumber;
    public string stageName;
    public string sceneName;
    public Sprite stageImage;
    public bool isEndless;

    [Header("Reward")]
    public int clearGoldReward = 100;       // 매번 클리어
    public int firstClearGoldReward = 200;  // 최초 클리어 추가 보상

    [Header("Boss")]
    public BossId bossId;

    public string stageDescription;
    public int recommendedPower;
    public int totalRooms;

    public string difficulty;

    public StageData nextStage;

    [Header("Localization")]
    public LocalizedString localizedStageName;
    public LocalizedString localizedStageDescription;
    public LocalizedString localizedDifficulty;
}