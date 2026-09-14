using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Localization;
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

    public IReadOnlyList<AugmentData> CurrentChoices => currentChoices;
    public bool HasAugmentSkill => augmentSkillController != null && augmentSkillController.HasSkill;

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

    public void ShowAugmentChoices()  
    {
        if (!isAugmentsLoaded)
        {
            Debug.LogError(
                   "[Augment] 데이터가 준비되지 않아 " +
                   "다음 방으로 이동합니다."
               );

            RoomManager.Instance.StartNextRoom();
            return;
        }

        List<AugmentData> availableAugments = GetAvailableAugments();

        currentChoices = GetRandomChoices(availableAugments, 3);

        if (currentChoices.Count < 3)
        {
            Debug.LogError("[Augment] 일반 증강이 3개 미만입니다.");

            RoomManager.Instance.StartNextRoom();
            return;
        }

        GameManager.Instance.currentState = GameState.AugmentSelect;

        UIManager.Instance.ShowAugmentChoices(currentChoices);
    }
    private List<AugmentData> GetAvailableAugments()
    {
        List<AugmentData> result = new List<AugmentData>();

        foreach (AugmentData augment in allAugments)
        {
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

            RoomManager.Instance.StartNextRoom();
            return;
        }

        List<AugmentData> skillAugments = allAugments.FindAll(augment => augment.augmentType == AugmentType.AugmentSkill);

        currentChoices = GetRandomChoices(skillAugments, 3);

        if (currentChoices.Count < 3)
        {
            Debug.LogError("[Augment] 스킬 증강이 3개 미만입니다.");

            RoomManager.Instance.StartNextRoom();
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

        if (augment.augmentType != AugmentType.AugmentSkill) return localizedDescription;

        if (!augmentSkillController.HasSkill) return localizedDescription;

        if (augment.augmentSkill == augmentSkillController.CurrentSkillData)
        {
            return augmentSkillController.GetNextLevelDescription();
        }

        return localizedDescription;
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

        // 선택 즉시 중복 입력 잠금
        GameManager.Instance.currentState = GameState.Ready;

        SoundManager.Instance?.PlaySFX(SoundId.AugmentSelect);

        ApplyAugment(augment);

        UIManager.Instance.HideAugmentChoices(() =>
        {
            RoomManager.Instance.StartNextRoom();
        });
    }

    public void ApplyAugment(AugmentData augment)
    {
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
