using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
public class AugmentManager : MonoBehaviour
{
    public static AugmentManager Instance { get; private set; }

    private List<AugmentData> allAugments = new List<AugmentData>();
    private AsyncOperationHandle<IList<AugmentData>> augmentLoadHandle;
    private bool isAugmentsLoaded;

    private List<AugmentData> currentChoices = new List<AugmentData>();

    private AugmentSkillController augmentSkillController;

    private PlayerStats playerStats;
    private PlayerShield playerShield;

    // 시너지: 이번 판에 고른 증강 종류와 이미 발동한 시너지
    private readonly HashSet<AugmentType> ownedAugmentTypes = new HashSet<AugmentType>();
    private readonly HashSet<AugmentSynergy> activeSynergies = new HashSet<AugmentSynergy>();

    // 리롤: 판당 1회 무료, 이후 보상형 광고
    private bool freeRerollUsed;
    private bool isSkillChoice;
    private bool isRerollAdPending;

    // 로그라이크: 선택지 종류(리롤 때 같은 규칙으로 다시 뽑음), 전설 증강 효과
    private enum ChoiceMode { Normal, WithLegendary, Legendary, Skill }
    private ChoiceMode choiceMode;
    private LegendaryEffects legendaryEffects;
    private readonly HashSet<AugmentType> ownedLegendaries = new HashSet<AugmentType>();

    // 캐릭터 전용 증강: 지금 캐릭터 것만, 판당 1회
    private CharacterAugmentEffects characterEffects;
    private CharacterId currentCharacter;
    private readonly HashSet<AugmentType> ownedCharacterAugments = new HashSet<AugmentType>();

    public bool CanBanish => !isSkillChoice && RunState.BanishesLeft > 0;
    public bool IsUniqueOwned(AugmentType type) => ownedLegendaries.Contains(type) || ownedCharacterAugments.Contains(type);  //판당 1회 증강을 이미 가짐
    public bool IsReady => isAugmentsLoaded && allAugments.Count >= 3;  //이벤트에서 증강 보상을 줄 수 있는지

    public bool HasFreeReroll => !freeRerollUsed;
    public bool CanRerollWithAd => AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady;

    public IReadOnlyList<AugmentData> CurrentChoices => currentChoices;
    public bool HasAugmentSkill => augmentSkillController != null && augmentSkillController.HasSkill;
    public int AugmentSkillLevel => augmentSkillController != null ? augmentSkillController.CurrentSkillLevel : 0;

    [Header("Localization")]
    [SerializeField] private LocalizedString augmentSkillLevelFormat;

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
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        playerStats = player.GetComponent<PlayerStats>();
        playerShield = player.GetComponent<PlayerShield>();
        augmentSkillController = player.GetComponent<AugmentSkillController>();

        legendaryEffects = player.GetComponent<LegendaryEffects>();
        if (legendaryEffects == null) legendaryEffects = player.AddComponent<LegendaryEffects>();  //씬/프리팹 수정 없이 추가

        characterEffects = player.GetComponent<CharacterAugmentEffects>();
        if (characterEffects == null) characterEffects = player.AddComponent<CharacterAugmentEffects>();
        currentCharacter = SaveManager.Instance != null ? SaveManager.Instance.Data.selectedCharacterId : CharacterId.Cat;

        LoadAugments();
    }
    private void OnDestroy()
    {
        if (augmentLoadHandle.IsValid())
        {
            Addressables.Release(augmentLoadHandle);
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.currentState != GameState.AugmentSelect) return;

        if (currentChoices == null || currentChoices.Count < 3) return;

        //모바일이지만 테스트용
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Debug.Log(currentChoices[0].name);
            SelectAugment(currentChoices[0]);
        }

        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Debug.Log(currentChoices[1].name);
            SelectAugment(currentChoices[1]);
        }

        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            Debug.Log(currentChoices[2].name);
            SelectAugment(currentChoices[2]);
        }
    }

    public void ShowAugmentChoices(bool includeLegendary = false)  //includeLegendary: 엘리트/보스 보상은 한 칸을 전설 증강으로
    {
        if (!isAugmentsLoaded)
        {
            Debug.LogError(
                   "[Augment] 데이터가 준비되지 않아 " +
                   "다음 방으로 이동합니다."
               );

            FinishReward();
            return;
        }

        choiceMode = includeLegendary ? ChoiceMode.WithLegendary : ChoiceMode.Normal;
        List<AugmentData> availableAugments = GetAvailableAugments();

        currentChoices = BuildChoices(choiceMode, null);
        isSkillChoice = false;
#if UNITY_EDITOR
        DebugTestSettings.ApplyForcedAugment(currentChoices, availableAugments);  //Tools > BounceSquad Debug
#endif

        if (currentChoices.Count < 3)
        {
            Debug.LogError("[Augment] 일반 증강이 3개 미만입니다.");

            FinishReward();
            return;
        }

        GameManager.Instance.currentState = GameState.AugmentSelect;

        UIManager.Instance.ShowAugmentChoices(currentChoices);
    }

    public void ShowLegendaryChoices()  //이벤트(피의 제단 등): 전설 증강 3택 (부족하면 상위 등급으로 채움)
    {
        if (!isAugmentsLoaded)
        {
            FinishReward();
            return;
        }

        choiceMode = ChoiceMode.Legendary;
        isSkillChoice = false;
        currentChoices = BuildChoices(choiceMode, null);

        if (currentChoices.Count < 3)
        {
            FinishReward();
            return;
        }

        GameManager.Instance.currentState = GameState.AugmentSelect;
        UIManager.Instance.ShowAugmentChoices(currentChoices);
    }

    private List<AugmentData> BuildChoices(ChoiceMode mode, List<AugmentData> avoid)
    {
        List<AugmentData> normalPool = GetAvailableAugments();
        List<AugmentData> legendaryPool = GetLegendaryPool();

        if (avoid != null)  //리롤: 가능하면 지금 보이는 것과 다르게
        {
            List<AugmentData> freshNormal = normalPool.FindAll(a => !avoid.Contains(a));
            List<AugmentData> freshLegendary = legendaryPool.FindAll(a => !avoid.Contains(a));
            if (freshNormal.Count >= 3) normalPool = freshNormal;
            if (freshLegendary.Count >= 1) legendaryPool = freshLegendary;
        }

        if (mode == ChoiceMode.Skill)
        {
            return GetRandomChoices(allAugments.FindAll(augment => augment.augmentType == AugmentType.AugmentSkill), 3);
        }

        if (mode == ChoiceMode.Legendary)
        {
            List<AugmentData> result = GetRandomChoices(legendaryPool, 3);
            List<AugmentData> filler = normalPool.FindAll(a => a.grade >= AugmentGrade.Rare && !result.Contains(a));
            if (filler.Count + result.Count < 3) filler = normalPool.FindAll(a => !result.Contains(a));
            result.AddRange(GetRandomChoices(filler, 3 - result.Count));
            return result;
        }

        List<AugmentData> choices = GetRandomChoices(normalPool, 3);

        if (mode == ChoiceMode.WithLegendary && legendaryPool.Count > 0 && choices.Count == 3)
        {
            choices[Random.Range(0, 3)] = legendaryPool[Random.Range(0, legendaryPool.Count)];
        }

        return choices;
    }

    private List<AugmentData> GetLegendaryPool()  //아직 없는 전설 증강
    {
        return allAugments.FindAll(augment => LegendaryEffects.IsLegendary(augment.augmentType)
            && !ownedLegendaries.Contains(augment.augmentType) && !RunState.BanishedTypes.Contains(augment.augmentType));
    }

    public List<AugmentData> GetShopOffers(int count)  //상점 진열: 일반 증강 + 칸마다 25% 확률로 전설
    {
        List<AugmentData> result = new List<AugmentData>();
        if (!isAugmentsLoaded) return result;

        List<AugmentData> normalPool = GetAvailableAugments();
        List<AugmentData> legendaryPool = GetLegendaryPool();

        for (int i = 0; i < count; i++)
        {
            List<AugmentData> pool = legendaryPool.Count > 0 && Random.value < 0.25f ? legendaryPool : normalPool;
            List<AugmentData> candidates = pool.FindAll(a => !result.Contains(a));
            if (candidates.Count == 0) candidates = normalPool.FindAll(a => !result.Contains(a));
            if (candidates.Count == 0) break;

            result.Add(candidates[Random.Range(0, candidates.Count)]);
        }

        return result;
    }

    public AugmentData GetRandomAugment(AugmentGrade minGrade)  //이벤트 보상용 (전설 효과 제외)
    {
        if (!isAugmentsLoaded) return null;

        List<AugmentData> pool = GetAvailableAugments().FindAll(a => a.grade >= minGrade);
        if (pool.Count == 0) pool = GetAvailableAugments();
        return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
    }

    public void RequestBanish(int index)  //카드 하나를 버리고(이번 판 다시 안 나옴) 그 자리를 새 증강으로
    {
        if (GameManager.Instance == null || GameManager.Instance.currentState != GameState.AugmentSelect) return;
        if (!CanBanish || index < 0 || index >= currentChoices.Count) return;

        AugmentData banished = currentChoices[index];
        bool wasLegendary = LegendaryEffects.IsLegendary(banished.augmentType);

        // 너무 많이 버려 일반 선택지가 3개 미만이 되면 보상이 사라지므로 막음
        if (!wasLegendary && GetAvailableAugments().FindAll(a => a.augmentType != banished.augmentType).Count < 4)
        {
            ToastMessageManager.Instance?.Show(RunPopupUI.Loc("augment_banish_limit", "Can't banish any more."));
            return;
        }

        List<AugmentData> pool = wasLegendary ? GetLegendaryPool() : GetAvailableAugments();
        pool.RemoveAll(a => currentChoices.Contains(a) || a.augmentType == banished.augmentType);
        if (pool.Count == 0) pool = GetAvailableAugments().FindAll(a => !currentChoices.Contains(a) && a.augmentType != banished.augmentType);
        if (pool.Count == 0) return;

        RunState.TryUseBanish();
        RunState.BanishedTypes.Add(banished.augmentType);

        currentChoices[index] = pool[Random.Range(0, pool.Count)];
        UIManager.Instance.ReplaceAugmentCard(index, currentChoices[index]);  //버린 카드 한 장만 뒤집어 교체
    }

    private List<AugmentData> GetAvailableAugments()  //일반 선택지 후보 (전설 효과/버린 종류 제외)
    {
        List<AugmentData> result = new List<AugmentData>();

        foreach (AugmentData augment in allAugments)
        {
            if (LegendaryEffects.IsLegendary(augment.augmentType)) continue;
            if (RunState.BanishedTypes.Contains(augment.augmentType)) continue;
            if (augment.characterExclusive && augment.exclusiveCharacter != currentCharacter) continue;  //다른 캐릭터 전용
            if (ownedCharacterAugments.Contains(augment.augmentType)) continue;  //전용 증강은 한 번만

            // 일반 증강
            if (augment.augmentType != AugmentType.AugmentSkill)
            {
                result.Add(augment);
                continue;
            }

            // 아직 증강 스킬이 없으면
            // 일반 증강 선택에서는 스킬 증강 제외
            if (!augmentSkillController.HasSkill) continue;

            // 가지고 있는 스킬의 레벨업 증강만 허용
            if (augment.augmentSkill == augmentSkillController.CurrentSkillData)
            {
                result.Add(augment);
            }
        }

        return result;
    }
    public void ShowSkillAugmentChoices()
    {
        if (!isAugmentsLoaded)
        {
            Debug.LogError(
                "[Augment] 스킬 증강 데이터가 준비되지 않아 " +
                "다음 방으로 이동합니다."
            );

            FinishReward();
            return;
        }

        List<AugmentData> skillAugments = allAugments.FindAll(augment => augment.augmentType == AugmentType.AugmentSkill);

        choiceMode = ChoiceMode.Skill;
        currentChoices = GetRandomChoices(skillAugments, 3);
        isSkillChoice = true;
#if UNITY_EDITOR
        DebugTestSettings.ApplyForcedAugment(currentChoices, skillAugments);  //Tools > BounceSquad Debug
#endif

        if (currentChoices.Count < 3)
        {
            Debug.LogError("[Augment] 스킬 증강이 3개 미만입니다.");

            FinishReward();
            return;
        }

        GameManager.Instance.currentState = GameState.AugmentSelect;

        UIManager.Instance.ShowAugmentChoices(currentChoices);
    }
    public string GetAugmentDisplayName(AugmentData augment)
    {
        string localizedName =
        augment.localizedAugmentName.GetLocalizedString();

        if (augment.augmentType != AugmentType.AugmentSkill) return localizedName;

        if (!augmentSkillController.HasSkill) return localizedName;

        if (augment.augmentSkill == augmentSkillController.CurrentSkillData)
        {
            int nextLevel = augmentSkillController.CurrentSkillLevel + 1;

            return augmentSkillLevelFormat.GetLocalizedString(localizedName, nextLevel);
        }

        return localizedName;
    }
    public string GetAugmentDisplayDescription(AugmentData augment)
    {
        string localizedDescription = augment.localizedDescription.GetLocalizedString();

        if (augment.augmentType != AugmentType.AugmentSkill) return localizedDescription + GetSynergyHint(augment.augmentType);

        if (!augmentSkillController.HasSkill) return localizedDescription;

        if (augment.augmentSkill == augmentSkillController.CurrentSkillData)
        {
            return augmentSkillController.GetNextLevelDescription();
        }

        return localizedDescription;
    }

    private string GetSynergyHint(AugmentType type)  //이 증강을 고르면 완성되는 시너지는 강조, 재료가 되는 시너지는 진행도(1/2)로 표시
    {
        string partial = "";

        foreach (AugmentSynergy synergy in AugmentSynergy.All)
        {
            if (activeSynergies.Contains(synergy) || !synergy.Involves(type)) continue;

            AugmentType other = synergy.first == type ? synergy.second : synergy.first;

            if (ownedAugmentTypes.Contains(other))
            {
                string format = LocalizationSettings.StringDatabase.GetLocalizedString("UI", "synergy_hint_format");
                if (string.IsNullOrEmpty(format)) format = "Synergy: {0}";

                return "\n<color=#E65100>" + string.Format(format, synergy.LocalizedName) + "</color>";
            }

            if (partial.Length == 0 && !ownedAugmentTypes.Contains(type))  //아직 하나도 없음: 이걸 고르면 1/2
            {
                partial = $"\n<color=#5C6BC0>{synergy.LocalizedName} (1/2)</color>";
            }
        }

        return partial;
    }

    private void CheckSynergies(AugmentType pickedType)  //새로 완성된 시너지 발동
    {
        ownedAugmentTypes.Add(pickedType);

        foreach (AugmentSynergy synergy in AugmentSynergy.All)
        {
            if (activeSynergies.Contains(synergy)) continue;
            if (!ownedAugmentTypes.Contains(synergy.first) || !ownedAugmentTypes.Contains(synergy.second)) continue;

            activeSynergies.Add(synergy);
            synergy.applyBonus?.Invoke(playerStats, playerShield);

            string title = LocalizationSettings.StringDatabase.GetLocalizedString("UI", "synergy_activated_title");
            if (string.IsNullOrEmpty(title)) title = "SYNERGY!";

            HudBanner.Show(title, synergy.LocalizedName);
            Debug.Log($"[Synergy] {synergy.nameKey} 발동");
        }
    }

    public void RequestReroll()  //증강 선택 화면의 새로고침 버튼
    {
        if (GameManager.Instance == null || GameManager.Instance.currentState != GameState.AugmentSelect) return;
        if (isRerollAdPending) return;

        if (!freeRerollUsed)
        {
            freeRerollUsed = true;
            Reroll();
            return;
        }

        if (!CanRerollWithAd) return;

        isRerollAdPending = true;
        AdManager.Instance.ShowRewardedAd(() =>
        {
            isRerollAdPending = false;
            Reroll();
        });

        // 광고가 닫히고 보상이 없을 때를 대비해 일정 시간 뒤 잠금 해제
        Invoke(nameof(ClearRerollAdPending), 3f);
    }

    private void ClearRerollAdPending()
    {
        isRerollAdPending = false;
    }

    private void Reroll()
    {
        if (GameManager.Instance == null || GameManager.Instance.currentState != GameState.AugmentSelect) return;

        List<AugmentData> next;

        if (choiceMode == ChoiceMode.Skill)
        {
            List<AugmentData> pool = allAugments.FindAll(augment => augment.augmentType == AugmentType.AugmentSkill);

            // 가능하면 지금 보이는 3개와 다른 것으로
            List<AugmentData> fresh = pool.FindAll(augment => !currentChoices.Contains(augment));
            next = GetRandomChoices(fresh.Count >= 3 ? fresh : pool, 3);
        }
        else next = BuildChoices(choiceMode, currentChoices);  //전설 칸 등 같은 규칙으로 다시 뽑음

        if (next.Count < 3) return;

        currentChoices = next;
        UIManager.Instance.ShowAugmentChoices(currentChoices);
    }

    public List<AugmentData> GetRandomChoices(List<AugmentData> list, int count)
    {
        // 원본 리스트 훼손 방지를 위해 복사본 생성
        List<AugmentData> tempList = new List<AugmentData>(list);
        List<AugmentData> resultList = new List<AugmentData>();

        count = Mathf.Min(count, tempList.Count);  // 뽑으려는 개수가 리스트 전체 개수보다 많으면 예외 처리 

        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(0, tempList.Count);  // 0부터 현재 임시 리스트의 크기까지의 무작위 인덱스 생성

            resultList.Add(tempList[randomIndex]);  // 결과 리스트에 뽑힌 아이템 추가

            tempList.RemoveAt(randomIndex);  // 중복 방지를 위해 임시 리스트에서 해당 아이템 삭제
        }

        return resultList;
    }

    public void SelectAugment(AugmentData augment)  //증강 선택
    {
        if (GameManager.Instance.currentState != GameState.AugmentSelect)
            return;

        // 선택 즉시 중복 입력 잠금 (갈림길이 뜨기 전 틈에 발사하지 않도록 RoomClear. 다음 방 시작 때 Ready가 됨)
        GameManager.Instance.currentState = GameState.RoomClear;

        SoundManager.Instance?.PlaySFX(SoundId.AugmentSelect);

        if (augment != null && !TutorialMode.IsActive) GameAnalytics.Record("augment_selected", ("augment_id", augment.name), ("room", RoomManager.Instance != null ? RoomManager.Instance.CurrentRoom : 0));

        GrantAugment(augment);

        UIManager.Instance.HideAugmentChoices(FinishReward);
    }

    public void GrantAugment(AugmentData augment)  //선택/상점/이벤트 공용: 적용 + 시너지 + 런 기록
    {
        if (augment == null) return;

        ApplyAugment(augment);

        if (augment.augmentType != AugmentType.AugmentSkill) CheckSynergies(augment.augmentType);

        RunState.RecordAugment(augment);

        if (LegendaryEffects.IsLegendary(augment.augmentType))
        {
            HudBanner.Show(RunPopupUI.Loc("legendary_acquired_title", "LEGENDARY!"), GetAugmentDisplayName(augment));
        }
    }

    private void FinishReward()  //보상 선택이 끝나면 갈림길로 (RunFlow가 없으면 바로 다음 방)
    {
        if (RunFlow.Instance != null) RunFlow.Instance.OnRewardFinished();
        else RoomManager.Instance.StartNextRoom();
    }

    public void ApplyAugment(AugmentData augment)
    {
        if (LegendaryEffects.IsLegendary(augment.augmentType))
        {
            ownedLegendaries.Add(augment.augmentType);
            legendaryEffects?.Enable(augment.augmentType);
            return;
        }

        if (CharacterAugmentEffects.IsCharacterAugment(augment.augmentType))
        {
            ownedCharacterAugments.Add(augment.augmentType);
            characterEffects?.Enable(augment.augmentType);
            return;
        }

        if (augment.augmentType == AugmentType.AttackUp)
        {
            playerStats.AddAttackPercent(augment.value);
        }
        else if (augment.augmentType == AugmentType.MaxHpUp)
        {
            playerStats.AddMaxHpPercent(augment.value);
        }
        else if (augment.augmentType == AugmentType.MoveSpeedUp)
        {
            playerStats.AddMoveSpeedPercent(augment.value);
        }
        else if (augment.augmentType == AugmentType.SkillDamageUp)
        {
            playerStats.AddSkillDamagePercent(augment.value);
        }
        else if (augment.augmentType == AugmentType.CriticalChanceUp)
        {
            playerStats.AddCriticalChance(augment.value);
        }
        else if (augment.augmentType == AugmentType.CriticalDamageUp)
        {
            playerStats.AddCriticalDamagePercent(augment.value);
        }
        else if (augment.augmentType == AugmentType.CollisionDamageUp)
        {
            playerStats.AddCollisionDamagePercent(augment.value);
        }
        else if (augment.augmentType == AugmentType.HealOnRoomClear)
        {
            playerStats.AddHealOnRoomClearPercent(augment.value);
        }
        else if (augment.augmentType == AugmentType.LifeSteal)
        {
            playerStats.AddLifeStealPercent(augment.value);
        }
        else if (augment.augmentType == AugmentType.WallReboundCharge)
        {
            playerStats.AddWallReboundBonus(augment.value);
        }
        else if (augment.augmentType == AugmentType.Shield)
        {
            playerShield.AddMaxShield(Mathf.RoundToInt(augment.value));
        }
        else if (augment.augmentType == AugmentType.AugmentSkill)
        {
            if (augment.augmentSkill == null)
            {
                Debug.LogError("[Augment] AugmentSkillData가 없습니다.");
                return;
            }

            augmentSkillController.AcquireSkill(augment);
        }
    }

    private async void LoadAugments()
    {
        if (augmentLoadHandle.IsValid()) return;

        allAugments.Clear();  //중복방지 위한 리스트 초기화

        augmentLoadHandle =Addressables.LoadAssetsAsync<AugmentData>("Augment",augment => allAugments.Add(augment));

        await augmentLoadHandle.Task;

        if (augmentLoadHandle.Status == AsyncOperationStatus.Succeeded)
        {
            isAugmentsLoaded = true;
            Debug.Log($"[Addressables] 증강 {allAugments.Count}개 로드 완료");
        }
        else
        {
            isAugmentsLoaded = false;

            Debug.LogError("[Addressables] 증강 데이터 로드 실패");
        }
    }
}
