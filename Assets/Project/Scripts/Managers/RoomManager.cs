using System.Collections.Generic;
using UnityEngine;
using System.Collections;
public enum RoomType
{
    Normal, Elite, Event, Boss,
    Shop, Rest  //갈림길로 고르는 전투 없는 방 (새 값은 끝에 추가)
}

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    [SerializeField] private EnemySpawner spawner;
    [SerializeField] private RoomObstacleSpawner obstacleSpawner;  //비어 있으면 같은 오브젝트에서 찾음 (없으면 벽 없음)
    public List<EnemyController> enemies = new List<EnemyController>();
    private readonly List<EnemyController> dyingEnemies = new List<EnemyController>();  //사망 애니메이션 재생 중(풀 반환 전)인 적

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
    public int CurrentRoom => currentRoom;
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
        if (obstacleSpawner == null) obstacleSpawner = GetComponent<RoomObstacleSpawner>();
        RunFlow.Create();  //로그라이크 진행 (갈림길/상점/이벤트/런 골드). 적 데이터 로드 완료 전에 있어야 함
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

        CombatJuice.Create();  //처치 연출, 코인, 피버 등 (씬 로컬)

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

    private Vector3 lastBossDeathPosition;
    private bool hasBossDeathPosition;

    public void OnEnemyDead(EnemyController enemy)
    {
        dyingEnemies.Add(enemy);

        if (enemy.IsBoss)  //보스 처치 연출 초점 (소환수가 마지막에 죽어도 보스 쪽으로)
        {
            lastBossDeathPosition = enemy.transform.position;
            hasBossDeathPosition = true;
        }

        if (enemies.Contains(enemy))
        {
            enemies.Remove(enemy);

            if (GameManager.Instance.currentState == GameState.GameOver) return;

            if (enemies.Count == 0)
            {
                if (roomType == RoomType.Boss) PlayBossKillCinematic(hasBossDeathPosition ? lastBossDeathPosition : enemy.transform.position);
                else PlayBossKillCinematic(enemy.transform.position, lastKillOnly: true);  //일반/엘리트 방도 마지막 한 마리는 짧은 슬로모션
            }
        }
    }

    private void PlayBossKillCinematic(Vector3 focusPoint, bool lastKillOnly = false)  //마지막 적 처치: 슬로모션 + 줌인 후 방 클리어 (보스 방은 길고 강하게)
    {
        GameManager.Instance.currentState = GameState.RoomClear;  //연출 중 플레이어 정지, 일시정지 불가
        hasBossDeathPosition = false;

        if (playerHealth != null) playerHealth.IsInvulnerable = true;  //남은 미사일/레이저에 죽어 클리어가 막히지 않도록

        BossKillCinematic.Play(focusPoint, () =>
        {
            if (this == null || GameManager.Instance == null) return;

            if (playerHealth != null) playerHealth.IsInvulnerable = false;
            if (GameManager.Instance.currentState == GameState.GameOver) return;

            ClearRoom();
        }, lastKillOnly);
    }

    private void ReturnDyingEnemies()  //사망 연출 중인 시체 위에 새 적이 겹쳐 스폰되지 않도록 먼저 회수
    {
        foreach (EnemyController enemy in dyingEnemies)
        {
            if (enemy != null && enemy.IsDead && enemy.gameObject.activeInHierarchy)
            {
                enemy.ReturnToPoolNow();
            }
        }

        dyingEnemies.Clear();
    }

    private RoomType? pendingRoomType;  //갈림길에서 고른 다음 방 종류

    public void SetNextRoomType(RoomType type)
    {
        pendingRoomType = type;
    }

    public bool IsBossRoomNumber(int room)  //일반 스테이지는 마지막 방, 무한 모드는 5의 배수 방
    {
        if (GameSessionData.IsEndless || stageController == null || stageController.TotalRooms <= 0) return room % 5 == 0;
        return room >= stageController.TotalRooms;
    }

    private static bool IsSpecialRoom(RoomType type) => type == RoomType.Shop || type == RoomType.Rest || type == RoomType.Event;

    private RoomType DetermineRoomType()
    {
        RoomType? chosen = pendingRoomType;
        pendingRoomType = null;

        if (IsBossRoomNumber(currentRoom)) return RoomType.Boss;
        if (chosen.HasValue) return chosen.Value;
        if (currentRoom == 1) return RoomType.Normal;

        return currentRoom % 3 == 0 ? RoomType.Elite : RoomType.Normal;  //갈림길 없이 진행된 경우 (예전 규칙)
    }

    public void StartRoom()
    {
        ReturnDyingEnemies();
        enemies.Clear();
        ComboSystem.ResetCombo();

        roomType = DetermineRoomType();

        PlayRoomBGM();

        if (TutorialMode.IsActive)
        {
            //튜토리얼: 기록 없음
        }
        else if (GameSessionData.IsEndless)
        {
            SaveManager.Instance.UpdateBestEndlessRoom(currentRoom);
        }
        else
        {
            SaveManager.Instance.UpdateReachedRoom(GameSessionData.SelectedStage.stageNumber, currentRoom);
        }

        if (IsSpecialRoom(roomType) && RunFlow.Instance != null)  //상점/휴식/이벤트: 적 없이 팝업으로 진행
        {
            if (obstacleSpawner != null) obstacleSpawner.ClearWalls();
            RoomGimmickSpawner.GetOrCreate().Clear();
            UIManager.Instance.ShowStageUI(roomType, currentRoom);
            UIManager.Instance.ShowPlayerHealth(this.playerHealth);  //아래 지역 변수 playerHealth와 구분
            RunFlow.Instance.EnterSpecialRoom(roomType);
            return;
        }

        if (IsSpecialRoom(roomType)) roomType = RoomType.Normal;  //RunFlow가 없으면 일반 전투로

        if (TutorialMode.IsActive)  //튜토리얼은 배우는 데 집중하도록 벽/기믹 없이
        {
            if (obstacleSpawner != null) obstacleSpawner.ClearWalls();
            RoomGimmickSpawner.GetOrCreate().Clear();
        }
        else
        {
            if (obstacleSpawner != null)
            {
                obstacleSpawner.SpawnForRoom(roomType, playerSpawnPoint.position);  //적 스폰 전에 벽 배치
            }

            RoomGimmickSpawner.GetOrCreate().SpawnForRoom(roomType, currentRoom, playerSpawnPoint.position);  //범퍼/폭발 통/포털 (벽 뒤, 적 앞)
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

        if (TutorialMode.IsActive)  //튜토리얼: 미션/런 골드 없음, 마지막 방이면 완료 연출
        {
            if (currentRoom >= stageController.TotalRooms)
            {
                CombatTutorialGuide.Instance?.OnTutorialCleared();
                return;
            }
        }
        else
        {
            GameEvents.RaiseRoomCleared(currentRoom, GameSessionData.IsEndless);
            RunFlow.Instance?.OnRoomCleared(roomType);  //런 골드 보너스
        }

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
            AugmentManager.Instance.ShowAugmentChoices(includeLegendary: roomType == RoomType.Elite || roomType == RoomType.Boss);  //엘리트/보스 보상은 전설 증강 한 칸
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

        earnedGold = Mathf.RoundToInt(earnedGold * RunState.AscensionGoldReward * RemoteTuning.StageGoldMultiplier);  //승천 단계 보상, 원격 조정

        // 이 단계로 클리어하면 다음 승천 단계 해금
        SaveManager.Instance.UnlockAscension(Mathf.Min(RunState.Ascension + 1, RunState.MaxAscension));

        SaveManager.Instance.AddGold(earnedGold);

        SaveManager.Instance.MarkStageCleared(stage.stageNumber);

        stageController.UnlockNextStage();

        UIManager.Instance.ShowStageClear(stage, currentRoom, earnedGold);
        RunFlow.Instance?.ShowSummary(true, currentRoom);  //클리어 패널 위에 런 요약 (확인 누르면 닫힘)

        GameEvents.RaiseStageCleared(stage.stageNumber);
    }
  
    public void ApplyAscensionToCurrentEnemies()  //판 시작 때 승천을 고르기 전에 이미 나온 첫 방 적에게 보정 적용
    {
        foreach (EnemyController enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;

            float hp = RunState.AscensionEnemyHp * (enemy.IsBoss ? RunState.AscensionBossHp : 1f);
            enemy.ApplyStatMultiplier(hp, RunState.AscensionEnemyAttack);
        }
    }

    public void StartNextRoom()
    {
        currentRoom++;

        EndlessMapRotator.OnNextRoom(currentRoom);  //무한 모드: 보스까지 한 사이클을 끝냈으면 맵만 교체

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
#if UNITY_EDITOR
        if (DebugTestSettings.StartRoomOverride > 1) currentRoom = DebugTestSettings.StartRoomOverride;  //Tools > BounceSquad Debug
#endif
        StartRoom();
        LoadingScreenManager.Instance?.NotifySceneReady();
        RunFlow.Instance?.ShowBlessingIfNeeded();  //첫 방 시작 전 축복 3택 1
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
        GameAnalytics.Record("ad_revive_used", ("room", currentRoom), ("endless", GameSessionData.IsEndless ? 1 : 0));

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
        int stageNumber = GameSessionData.SelectedStage != null ? GameSessionData.SelectedStage.stageNumber : 0;  //스테이지 씬을 바로 실행한 경우 등
        Debug.Log($"[GameOver] Stage {stageNumber}, Room {currentRoom}");
        GameAnalytics.Record("player_died", ("stage", stageNumber), ("room", currentRoom), ("endless", GameSessionData.IsEndless ? 1 : 0), ("final", hasUsedRewardedRevive ? 1 : 0));

        if (GameSessionData.IsEndless)
        {
            int earnedGold = GameSessionData.EndlessRunGold;

            // 이미 광고 부활을 사용했다면 이번 사망이 최종 사망
            if (hasUsedRewardedRevive)
            {
                ClaimEndlessRunGold();
            }

            UIManager.Instance.ShowGameOver(GameSessionData.SelectedStage, currentRoom, earnedGold);
            if (hasUsedRewardedRevive) RunFlow.Instance?.ShowSummary(false, currentRoom);  //최종 사망일 때만 요약

            return;
        }

        UIManager.Instance.ShowGameOver(GameSessionData.SelectedStage, currentRoom, 0);
        if (hasUsedRewardedRevive) RunFlow.Instance?.ShowSummary(false, currentRoom);
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

        int stageNumber = GameSessionData.SelectedStage != null ? GameSessionData.SelectedStage.stageNumber : 1;  //스테이지 씬을 바로 실행한 경우 등

        switch (stageNumber)
        {
            case 0:  //전투 튜토리얼 (1스테이지 맵)
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
