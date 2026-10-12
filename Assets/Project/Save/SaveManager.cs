using System;
using System.Collections.Generic;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const string SaveKey = "PlayerSaveData";

    public const int MoveSpeedUpgradeMaxLevel = 10;
    public const int DashCapacityMaxLevel = 2;  //대쉬 칸 1 → 최대 3
    public const int DashRechargeMaxLevel = 5;
    public const int MaxNormalStage = 3;  //일반 스테이지 수 (4스테이지는 출시 전에 뺌: 4가 열려 있던 세이브도 아래 Clamp로 3이 됨)

    public PlayerSaveData Data { get; private set; }

    public event Action<int> OnGoldChanged;
    public event Action<CharacterId> OnSelectedCharacterChanged;
    public event Action<ShopItemType> OnUpgradeChanged;
    public event Action<CharacterId> OnCharacterLevelChanged;
    public event Action<CharacterId> OnSkillTreeChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Load();
        }
        else
        {
            Destroy(gameObject);
        }
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9))
        {
            ResetSaveData();
        }
        if (Input.GetKeyDown(KeyCode.F8))
        {
            DebugClearAllNormalStages();
        }
    }
#endif

    public event Action OnSaved;  //랭킹 기록 등록 등

    public void Save()
    {
        string json = JsonUtility.ToJson(Data);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();

        OnSaved?.Invoke();
    }

    // SaveManager가 생성되기 전(Bootstrap 첫 로딩 화면 등)에 선택 캐릭터만 읽기 위한 읽기 전용 조회. 저장하지 않는다.
    public static CharacterId PeekSelectedCharacterId()
    {
        try
        {
            if (PlayerPrefs.HasKey(SaveKey))
            {
                PlayerSaveData data = JsonUtility.FromJson<PlayerSaveData>(PlayerPrefs.GetString(SaveKey));

                if (data != null && data.unlockedCharacters != null && data.unlockedCharacters.Contains(data.selectedCharacterId))
                {
                    return data.selectedCharacterId;
                }
            }
        }
        catch (Exception)
        {
            // 읽기 실패 시 기본 캐릭터
        }

        return CharacterId.Cat;
    }

    public void Load()
    {
        try
        {
            if (PlayerPrefs.HasKey(SaveKey))
            {
                string json = PlayerPrefs.GetString(SaveKey);

                Data = JsonUtility.FromJson<PlayerSaveData>(json);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[Save] 저장 데이터 로드 실패: " +
                $"{exception.Message}"
            );

            Data = null;
        }

        if (Data == null)
        {
            Data = new PlayerSaveData();
        }

        Data.unlockedCharacters ??= new List<CharacterId>();

        Data.stageRecords ??= new List<StageRecordData>();

        Data.characterProgress ??= new List<CharacterProgressData>();

        Data.processedIapTransactionIds ??= new List<string>();

        Data.dailyMissions ??= new List<DailyMissionProgressData>();
        Data.defeatedBosses ??= new List<string>();
        Data.encounteredEnemies ??= new List<string>();
        Data.enemyKillCounts ??= new List<EnemyKillCountData>();
        Data.enemyKillCounts.RemoveAll(entry => entry == null || string.IsNullOrEmpty(entry.enemyId));
        Data.lastAttendanceDate ??= "";
        Data.dailyMissionDate ??= "";

        // 튜토리얼 추가 전부터 플레이하던 유저는 튜토리얼을 건너뜀 (한 번만 판단)
        if (!Data.tutorialChecked)
        {
            bool isExistingPlayer = Data.highestUnlockedStage > 1 || Data.stageRecords.Count > 0 || Data.bestEndlessRoom > 0 || Data.gold > 0;
            if (isExistingPlayer) Data.tutorialFlags = -1;  //전부 본 것으로
            Data.tutorialChecked = true;
        }

        Data.skillTrees ??= new List<SkillTreeProgressData>();
        Data.skillTrees.RemoveAll(tree => tree == null);
        for (int i = Data.skillTrees.Count - 1; i > 0; i--)  //같은 캐릭터 항목이 여러 개면 앞의 것만 남김
        {
            CharacterId id = Data.skillTrees[i].characterId;
            if (Data.skillTrees.FindIndex(tree => tree.characterId == id) < i) Data.skillTrees.RemoveAt(i);
        }
        foreach (SkillTreeProgressData tree in Data.skillTrees)
        {
            tree.tier = Mathf.Clamp(tree.tier, 0, 3);
            tree.branch = tree.tier >= 2 ? Mathf.Clamp(tree.branch, 1, 2) : 0;
            tree.spentGold = Mathf.Max(0, tree.spentGold);
        }

        Data.gold = Mathf.Max(0, Data.gold);
        Data.bestEndlessRoom = Mathf.Max(0, Data.bestEndlessRoom);

        Data.moveSpeedUpgradeLevel = Mathf.Clamp(Data.moveSpeedUpgradeLevel,0,MoveSpeedUpgradeMaxLevel);
        Data.dashCapacityUpgradeLevel = Mathf.Clamp(Data.dashCapacityUpgradeLevel, 0, DashCapacityMaxLevel);
        Data.dashRechargeUpgradeLevel = Mathf.Clamp(Data.dashRechargeUpgradeLevel, 0, DashRechargeMaxLevel);

        Data.highestUnlockedStage = Mathf.Clamp(Data.highestUnlockedStage, 1, MaxNormalStage);

        if (!Data.unlockedCharacters.Contains(CharacterId.Cat))
        {
            Data.unlockedCharacters.Add(CharacterId.Cat);
        }

        if (!Data.unlockedCharacters.Contains(Data.selectedCharacterId))
        {
            Data.selectedCharacterId = CharacterId.Cat;
        }

        Save();
    }

    public StageRecordData GetStageRecord(int stageNumber)  //StageRecordData를 가져오기
    {
        return Data.stageRecords.Find(record => record.stageNumber == stageNumber);
    }

    public void UpdateReachedRoom(int stageNumber, int reachedRoom)  //스테이지 번호와 도달한 방 번호를 업데이트
    {
        StageRecordData record = GetStageRecord(stageNumber);

        if (record == null)
        {
            record = new StageRecordData
            {
                stageNumber = stageNumber,
                bestReachedRoom = reachedRoom,
                isCleared = false
            };

            Data.stageRecords.Add(record);
        }
        else if (reachedRoom > record.bestReachedRoom)
        {
            record.bestReachedRoom = reachedRoom;
        }

        Save();
    }

    public void MarkStageCleared(int stageNumber)  //스테이지 클리어 여부를 업데이트
    {
        StageRecordData record = GetStageRecord(stageNumber);

        if (record == null)
        {
            record = new StageRecordData
            {
                stageNumber = stageNumber,
                isCleared = true
            };

            Data.stageRecords.Add(record);
        }
        else
        {
            record.isCleared = true;
        }

        Save();
    }
    public bool IsEndlessUnlocked()
    {
        for (int stageNumber = 1; stageNumber <= 3; stageNumber++)
        {
            StageRecordData record = GetStageRecord(stageNumber);

            if (record == null || !record.isCleared) return false;
        }

        return true;
    }
    public void UpdateBestEndlessRoom(int reachedRoom)
    {
        if (reachedRoom <= Data.bestEndlessRoom) return;

        Data.bestEndlessRoom = reachedRoom;
        Save();

        Debug.Log($"[Endless] 최고 도달 ROOM {Data.bestEndlessRoom}");
    }

#if UNITY_EDITOR
    public void ResetSaveData()  //세이브 초기화용
    {
        PlayerPrefs.DeleteKey("PlayerSaveData");
        PlayerPrefs.Save();

        Data = new PlayerSaveData();
        Save();

        Debug.Log("[Save] 저장 데이터 초기화 완료");
    }
    private void DebugClearAllNormalStages()
    {
        for (int stageNumber = 1; stageNumber <= MaxNormalStage; stageNumber++)
        {
            MarkStageCleared(stageNumber);
        }

        Data.highestUnlockedStage = MaxNormalStage;
        Save();

        Debug.Log($"[DEBUG] Stage 1~{MaxNormalStage} 클리어 처리 완료");
    }
#endif
    public void SetSelectedCharacter(CharacterId characterId)
    {
        if (!IsCharacterUnlocked(characterId))
        {
            Debug.LogWarning($"[Character] 잠긴 캐릭터 선택 시도: {characterId}");
            return;
        }

        Data.selectedCharacterId = characterId;
        Save();

        OnSelectedCharacterChanged?.Invoke(characterId);

        Debug.Log($"[Save] 선택 캐릭터 ID : {characterId}");
    }
    public bool TryGrantIapGold(string transactionId, int amount)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
        {
            Debug.LogError("[IAP Save] 거래 ID가 없습니다.");
            return false;
        }

        if (Data.processedIapTransactionIds.Contains(transactionId))
        {
            return false;
        }

        Data.gold += amount;
        Data.processedIapTransactionIds.Add(transactionId);

        // 골드 지급과 거래 ID 기록을 한 번에 저장
        Save();

        OnGoldChanged?.Invoke(Data.gold);

        Debug.Log($"[IAP Save] 거래 처리 완료: {transactionId}");

        return true;
    }

    public void AddGold(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"[Gold] 잘못된 지급량: {amount}");
            return;
        }

        Data.gold += amount;
        Save();

        OnGoldChanged?.Invoke(Data.gold);
    }

    public bool SpendGold(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"[Gold] 잘못된 차감량: {amount}");
            return false;
        }

        if (Data.gold < amount)
            return false;

        Data.gold -= amount;
        Save();

        OnGoldChanged?.Invoke(Data.gold);

        return true;
    }

    public int GetGold()
    {
        return Data.gold;
    }

    public SkillTreeProgressData GetSkillTree(CharacterId characterId)  //없으면 null (읽기 전용 조회)
    {
        return Data.skillTrees.Find(tree => tree.characterId == characterId);
    }

    public bool TryBuySkillNode(CharacterId characterId, SkillTreeNode node)
    {
        SkillTreeProgressData tree = GetSkillTree(characterId);
        if (!SkillTreeCatalog.CanBuy(tree, node)) return false;

        int cost = SkillTreeCatalog.GetCost(node);
        if (!SpendGold(cost)) return false;

        if (tree == null)
        {
            tree = new SkillTreeProgressData { characterId = characterId };
            Data.skillTrees.Add(tree);
        }

        tree.tier = SkillTreeCatalog.GetTier(node);
        int branch = SkillTreeCatalog.GetBranch(node);
        if (branch != 0) tree.branch = branch;
        tree.spentGold += cost;

        Save();
        OnSkillTreeChanged?.Invoke(characterId);
        return true;
    }

    public bool TryResetSkillTree(CharacterId characterId)  //쓴 골드를 돌려주되 수수료를 뗌
    {
        SkillTreeProgressData tree = GetSkillTree(characterId);
        if (tree == null || tree.tier == 0) return false;

        int fee = SkillTreeCatalog.GetResetFee(tree);
        int refund = Mathf.Max(0, tree.spentGold - fee);  //데이터가 어긋나도 초기화가 골드를 깎지는 않음

        Data.gold += refund;
        tree.tier = 0;
        tree.branch = 0;
        tree.spentGold = 0;

        Save();
        OnGoldChanged?.Invoke(Data.gold);
        OnSkillTreeChanged?.Invoke(characterId);
        return true;
    }

    public int GetUpgradeLevel(ShopItemType type)
    {
        switch (type)
        {
            case ShopItemType.MaxHp: return Data.maxHpUpgradeLevel;
            case ShopItemType.Attack: return Data.attackUpgradeLevel;
            case ShopItemType.MoveSpeed: return Data.moveSpeedUpgradeLevel;
            case ShopItemType.DashCapacity: return Data.dashCapacityUpgradeLevel;
            case ShopItemType.DashRecharge: return Data.dashRechargeUpgradeLevel;
        }

        return 0;
    }

    public static int GetMaxUpgradeLevel(ShopItemType type)  //제한 없으면 int.MaxValue
    {
        switch (type)
        {
            case ShopItemType.MoveSpeed: return MoveSpeedUpgradeMaxLevel;
            case ShopItemType.DashCapacity: return DashCapacityMaxLevel;
            case ShopItemType.DashRecharge: return DashRechargeMaxLevel;
        }

        return int.MaxValue;
    }

    public void AddUpgradeLevel(ShopItemType type)
    {
        switch (type)
        {
            case ShopItemType.MaxHp:
                Data.maxHpUpgradeLevel++;
                break;

            case ShopItemType.Attack:
                Data.attackUpgradeLevel++;
                break;

            case ShopItemType.MoveSpeed:
                if (Data.moveSpeedUpgradeLevel >= MoveSpeedUpgradeMaxLevel)
                {
                    Debug.LogWarning("[Upgrade] 이동속도 강화가 최대 레벨입니다.");
                    return;
                }

                Data.moveSpeedUpgradeLevel++;
                break;

            case ShopItemType.DashCapacity:
            case ShopItemType.DashRecharge:
                if (GetUpgradeLevel(type) >= GetMaxUpgradeLevel(type))
                {
                    Debug.LogWarning($"[Upgrade] {type} 강화가 최대 레벨입니다.");
                    return;
                }

                if (type == ShopItemType.DashCapacity) Data.dashCapacityUpgradeLevel++;
                else Data.dashRechargeUpgradeLevel++;
                break;
        }

        Save();

        Debug.Log(
         $"[Upgrade Save] HP:{Data.maxHpUpgradeLevel} / " +
         $"ATK:{Data.attackUpgradeLevel} / " +
         $"SPD:{Data.moveSpeedUpgradeLevel}"
        );

        OnUpgradeChanged?.Invoke(type);
    }
    public CharacterProgressData GetCharacterProgress(CharacterId characterId)  //캐릭터 레벨 가져오기
    {
        CharacterProgressData progress = Data.characterProgress.Find(x => x.characterId == characterId);

        if (progress == null)
        {
            progress = new CharacterProgressData{characterId = characterId, level = 1};

            Data.characterProgress.Add(progress);
            Save();
        }

        return progress;
    }
    public void LevelUpCharacter(CharacterId characterId)  //레벨업
    {
        CharacterProgressData progress = GetCharacterProgress(characterId);

        progress.level++;

        Save();

        OnCharacterLevelChanged?.Invoke(characterId);  //홈 캐릭터 정보 등 레벨 표시 갱신

        Debug.Log($"[Character Level] ID:{characterId} / Lv.{progress.level}");
    }
    public void SetNickname(string nickname)
    {
        Data.nickname = nickname;
        Save();

        Debug.Log($"[Save] 닉네임 : {nickname}");
    }

    public string GetNickname()
    {
        return Data.nickname;
    }
    public bool IsCharacterUnlocked(CharacterId characterId)
    {
        return Data.unlockedCharacters.Contains(characterId);
    }

    public void UnlockCharacter(CharacterId characterId)
    {
        if (Data.unlockedCharacters.Contains(characterId)) return;

        Data.unlockedCharacters.Add(characterId);
        Save();

        Debug.Log($"[Character Unlock] {characterId}");
    }

    public int GetAscensionUnlocked()
    {
        return Data.ascensionUnlocked;
    }

    public void UnlockAscension(int level)  //해당 단계까지 해금 (낮아지지 않음)
    {
        if (level <= Data.ascensionUnlocked) return;

        Data.ascensionUnlocked = level;
        Save();

        Debug.Log($"[Ascension] {level}단계 해금");
    }
}