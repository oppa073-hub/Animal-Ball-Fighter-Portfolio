using System.Collections.Generic;
using UnityEngine;
using System.Collections;
public enum RoomType
{
    Normal, Elite, Event, Boss
}

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    [SerializeField] private EnemySpawner spawner;
    public List<EnemyController> enemies = new List<EnemyController>();

    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private PlayerBallController playerController;
    private Health playerHealth;
    private PlayerShield playerShield;
    private int currentRoom = 1;
    private RoomType roomType = RoomType.Normal;

    [SerializeField] private StageController stageController;

    [Header("Rewarded Revive")]
    [SerializeField, Range(0.1f, 1f)]
    private float reviveHpRatio = 0.5f;

    private bool hasUsedRewardedRevive;
    private bool endlessRewardClaimed;
    private bool isReturningToLobbyAfterFailure;

    public bool CanUseRewardedRevive => !hasUsedRewardedRevive;
    public RoomType RoomType => roomType;
    public bool HasActiveEnemies => enemies.Count > 0;

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
        playerHealth = playerController.GetComponent<Health>();
        playerShield = playerController.GetComponent<PlayerShield>();
        spawner.OnEnemyDataLoaded += HandleEnemyDataLoaded;
        spawner.OnEnemyDataLoadFailed += HandleEnemyDataLoadFailed;
    }
    private void OnDestroy()
    {
        if (playerShield != null) playerShield.OnShieldBroken -= HandleShieldBroken;
        if (spawner != null)
        {
            spawner.OnEnemyDataLoaded -= HandleEnemyDataLoaded;
            spawner.OnEnemyDataLoadFailed -= HandleEnemyDataLoadFailed;
        }
        if (playerHealth != null) playerHealth.OnDeath -= HandlePlayerDeath;
    }

    private void Start()
    {
        UIManager.Instance.ShowPlayerShield(playerShield);
        playerShield.OnShieldBroken += HandleShieldBroken;
        playerHealth.OnDeath += HandlePlayerDeath;

        if (GameSessionData.IsEndless)
        {
            UIManager.Instance.RefreshEndlessHUD();
        }
    }
    public void RegisterEnemy(EnemyController enemy)
    {
        enemies.Add(enemy);
        Debug.Log(enemy.EnemyName + "등록");
    }

    public void OnEnemyDead(EnemyController enemy)
    {
        if (enemies.Contains(enemy))
        {
            enemies.Remove(enemy);

            if (GameManager.Instance.currentState == GameState.GameOver) return;

            if (enemies.Count == 0) ClearRoom();
        }
    }

    public void StartRoom()
    {
        enemies.Clear();

        if (currentRoom % 5 == 0)
        {
            roomType = RoomType.Boss;
        }
        else if (currentRoom % 3 == 0)
        {
            roomType = RoomType.Elite;
        }
        else
        {
            roomType = RoomType.Normal;
        }

        PlayRoomBGM();

        if (GameSessionData.IsEndless)
        {
            SaveManager.Instance.UpdateBestEndlessRoom(currentRoom);
        }
        else
        {
            SaveManager.Instance.UpdateReachedRoom(GameSessionData.SelectedStage.stageNumber, currentRoom);
        }

        bool spawnSucceeded = spawner.SpawnEnemies(currentRoom);

        if (!spawnSucceeded)
        {
            HandleRoomStartFailure($"ROOM {currentRoom} 적 생성에 실패했습니다.");
            return;
        }

        UIManager.Instance.ShowStageUI(roomType,currentRoom);

        if (GameSessionData.IsEndless)
        {
            UIManager.Instance.RefreshEndlessHUD();
        }

        Health playerHealth = playerController.GetComponent<Health>();

        if (playerHealth == null) return;

        UIManager.Instance.ShowPlayerHealth(playerHealth);
    }

    public void ClearRoom()
    {
        Debug.Log($"[Room] currentRoom:{currentRoom}, TotalRooms:{stageController.TotalRooms}");
        Debug.Log("방클리어");

        GameManager.Instance.currentState = GameState.RoomClear;

        playerController.GetComponent<PlayerStats>().HealOnRoomClear(playerHealth);
        //나중에 보상연출할때 룸매니저.룸클리어로 바꾸기 잠깐동안만
        SoundManager.Instance?.PlaySFX(SoundId.RoomClear);

        // 일반 스테이지만 마지막 Room에서 클리어
        if (!GameSessionData.IsEndless && currentRoom >= stageController.TotalRooms)
        {
            ClearStage();
            return;
        }
        if (GameSessionData.IsEndless)
        {
            int reward = CalculateEndlessRoomReward(currentRoom);

            GameSessionData.AddEndlessRunGold(reward);

            Debug.Log(
                $"[Endless] ROOM {currentRoom} 보상 +{reward}G / " +
                $"누적 {GameSessionData.EndlessRunGold}G"
            );
        }

        if (roomType == RoomType.Elite && !AugmentManager.Instance.HasAugmentSkill)
        {
            AugmentManager.Instance.ShowSkillAugmentChoices();
        }
        else
        {
            AugmentManager.Instance.ShowAugmentChoices();
        }
    }
    private int CalculateEndlessRoomReward(int room)
    {
        int clearedRooms = room - 1;

        int earlyRooms = Mathf.Min(clearedRooms, 10);
        int midRooms = Mathf.Clamp(clearedRooms - 10, 0, 20);
        int lateRooms = Mathf.Max(clearedRooms - 30, 0);

        int reward =
            20 +
            earlyRooms * 5 +
            midRooms * 8 +
            lateRooms * 12;

        if (roomType == RoomType.Elite) reward = Mathf.RoundToInt(reward * 1.5f);

        if (roomType == RoomType.Boss) reward *= 2;

        return reward;
    }

    private void ClearStage()
    {
        StageData stage = GameSessionData.SelectedStage;

        StageRecordData record = SaveManager.Instance.GetStageRecord(stage.stageNumber);

        bool isFirstClear = record == null || !record.isCleared;

        int earnedGold = stage.clearGoldReward;

        // 최초 클리어 추가 지급
        if (isFirstClear)
        {
            earnedGold += stage.firstClearGoldReward;
        }

        SaveManager.Instance.AddGold(earnedGold);

        SaveManager.Instance.MarkStageCleared(stage.stageNumber);

        stageController.UnlockNextStage();

        UIManager.Instance.ShowStageClear(stage, currentRoom, earnedGold);
    }
  
    public void StartNextRoom()
    {
        currentRoom++;

        GameManager.Instance.currentState = GameState.Ready;

        playerController.ResetForNewRoom(playerSpawnPoint.position);

        StartRoom();
    }
    private void HandleShieldBroken()
    {
        DamageManager.Instance.PlayShieldBreakEffect(playerController.transform.position);
    }
    private void HandleEnemyDataLoaded()
    {
        StartRoom();
        LoadingScreenManager.Instance?.NotifySceneReady();
    }
    public void TryRewardedRevive()
    {
        if (GameManager.Instance.currentState != GameState.GameOver) return;

        if (!CanUseRewardedRevive)
        {
            Debug.LogWarning("[Ads] 이번 플레이에서 이미 부활했습니다.");
            return;
        }

        if (AdManager.Instance == null)
        {
            Debug.LogError("[Ads] AdManager가 없습니다.");
            return;
        }

        AdManager.Instance.ShowRewardedAd(RevivePlayer);
    }

    private void RevivePlayer()
    {
        if (hasUsedRewardedRevive || playerHealth == null) return;

        if (!playerHealth.Revive(reviveHpRatio)) return;

        hasUsedRewardedRevive = true;

        playerController.ResetForNewRoom(playerSpawnPoint.position);

        foreach (EnemyController enemy in enemies)
        {
            if (enemy != null)
            {
                enemy.PrepareForPlayerRevive();
            }
        }

        GameManager.Instance.ResumeAfterRevive();
        UIManager.Instance.HideGameOverForRevive();

        if (GameSessionData.IsEndless)
        {
            UIManager.Instance.RefreshEndlessHUD();
        }

        Debug.Log("[Ads] 플레이어 광고 부활 완료");
    }
    public void ClaimEndlessRunGold()
    {
        if (!GameSessionData.IsEndless || endlessRewardClaimed) return;

        int earnedGold = GameSessionData.EndlessRunGold;

        if (earnedGold > 0)
        {
            SaveManager.Instance.AddGold(earnedGold);
        }

        endlessRewardClaimed = true;
        GameSessionData.ResetEndlessRunGold();

        Debug.Log($"[Endless] 최종 골드 {earnedGold}G 지급");
    }

    private void HandlePlayerDeath()
    {
        Debug.Log($"[GameOver] Stage {GameSessionData.SelectedStage.stageNumber}, Room {currentRoom}");

        if (GameSessionData.IsEndless)
        {
            int earnedGold = GameSessionData.EndlessRunGold;

            // 이미 광고 부활을 사용했다면 이번 사망이 최종 사망
            if (hasUsedRewardedRevive)
            {
                ClaimEndlessRunGold();
            }

            UIManager.Instance.ShowGameOver(GameSessionData.SelectedStage, currentRoom, earnedGold);

            return;
        }

        UIManager.Instance.ShowGameOver(GameSessionData.SelectedStage, currentRoom, 0);
    }

    private void HandleEnemyDataLoadFailed()
    {
        HandleRoomStartFailure("EnemyData를 불러오지 못했습니다.");
    }

    private void HandleRoomStartFailure(string reason)
    {
        if (isReturningToLobbyAfterFailure) return;

        isReturningToLobbyAfterFailure = true;

        Debug.LogError($"[Room] {reason} 로비로 돌아갑니다.");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.currentState = GameState.RoomClear;
        }

        // 현재 스테이지 로딩이 완료될 수 있도록 준비 완료 신호 전달
        LoadingScreenManager.Instance?.NotifySceneReady();

        StartCoroutine(ReturnToLobbyAfterLoading());
    }

    private IEnumerator ReturnToLobbyAfterLoading()
    {
        // 현재 스테이지 로딩이 완전히 끝날 때까지 기다린다.
        while (LoadingScreenManager.Instance != null && LoadingScreenManager.Instance.IsLoading)
        {
            yield return null;
        }

        isReturningToLobbyAfterFailure = false;

        stageController.ReturnToLobby();
    }
    private void PlayRoomBGM()
    {
        if (roomType == RoomType.Boss)
        {
            SoundManager.Instance?.PlayBGM(BgmId.Boss);
            return;
        }

        switch (GameSessionData.SelectedStage.stageNumber)
        {
            case 1:
                SoundManager.Instance?.PlayBGM(BgmId.Stage1_Country);
                break;

            case 2:
                SoundManager.Instance?.PlayBGM(BgmId.Stage2_Town);
                break;

            case 3:
                SoundManager.Instance?.PlayBGM(BgmId.Stage3_City);
                break;
        }
    }
}
