using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Localization;
public class CharacterPreviewController : MonoBehaviour
{
    [SerializeField] private CharacterData[] characters;
    [SerializeField] private Transform spawnPoint;

    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text characterLevelText;

    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TMP_Text moveSpeedText;
    [SerializeField] private TMP_Text selectButtonText;
    [SerializeField] private LobbyCharacterDisplay lobbyCharacterDisplay;
    [SerializeField] private HomeCharacterInfoUI homeCharacterInfoUI;

    [SerializeField] private ShopItemData hpUpgradeItem;
    [SerializeField] private ShopItemData attackUpgradeItem;
    [SerializeField] private ShopItemData moveSpeedUpgradeItem;

    [SerializeField] private Image skillIcon;
    [SerializeField] private GameObject skillInfoPanel;
    [SerializeField] private TMP_Text skillNameText;
    [SerializeField] private TMP_Text skillDescriptionText;

    [SerializeField] private GameObject lockPanel;

    [Header("Localization")]
    [SerializeField] private LocalizedString characterBuyText;
    [SerializeField] private LocalizedString selectedText;
    [SerializeField] private LocalizedString selectText;
    [SerializeField] private LocalizedString notEnoughGoldText;
    [SerializeField] private LocalizedString unlockCompleteText;

    private int currentIndex;
    private GameObject currentPreview;

    private void Start()
    {
        CharacterId selectedId = SaveManager.Instance.Data.selectedCharacterId;

        currentIndex = FindCharacterIndex(selectedId);

        if (currentIndex < 0 || currentIndex >= characters.Length) currentIndex = 0;

        ShowCharacter(currentIndex);
    }

    public void ShowPrevious()
    {
        currentIndex--;

        if (currentIndex < 0) currentIndex = characters.Length - 1;

        ShowCharacter(currentIndex);
    }

    public void ShowNext()
    {
        currentIndex++;

        if (currentIndex >= characters.Length) currentIndex = 0;

        ShowCharacter(currentIndex);
    }

    private void ShowCharacter(int index)
    {
        if (currentPreview != null) Destroy(currentPreview);

        CharacterData data = characters[index];

        currentPreview = Instantiate(
            data.characterPrefab,
            spawnPoint.position,
            spawnPoint.rotation,
            spawnPoint
        );
        
        SetupPreviewAnimation(data);
        
        SetLayerRecursively(currentPreview, LayerMask.NameToLayer("CharacterPreview"));

        skillInfoPanel.SetActive(false);

        RefreshCharacterInfo(data);

        RefreshSelectButton();

        RefreshLockState();
    }
    private void SetupPreviewAnimation(CharacterData data)
    {
        if (currentPreview == null) return;
        if (data.lobbyAnimatorController == null) return;

        Animator previewAnimator = currentPreview.GetComponent<Animator>();

        if (previewAnimator == null)
        {
            previewAnimator = currentPreview.AddComponent<Animator>();
        }

        previewAnimator.runtimeAnimatorController =
            data.lobbyAnimatorController;

        previewAnimator.applyRootMotion = false;
        previewAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        previewAnimator.Rebind();
        previewAnimator.Update(0f);
    }

    public void SelectCurrentCharacter()
    {
        CharacterData data = characters[currentIndex];

        // 잠긴 캐릭터면 구매
        if (!SaveManager.Instance.IsCharacterUnlocked(data.characterId))
        {
            if (!SaveManager.Instance.SpendGold(data.unlockPrice))
            {
                ToastMessageManager.Instance?.Show(notEnoughGoldText.GetLocalizedString());
                return;
            }

            SaveManager.Instance.UnlockCharacter(data.characterId);
            RefreshLockState();
            string localizedName = data.localizedCharacterName.GetLocalizedString();

            ToastMessageManager.Instance?.Show(unlockCompleteText.GetLocalizedString(localizedName));
        }

        // 구매했거나 이미 해금된 캐릭터면 선택
        if (data.characterId == SaveManager.Instance.Data.selectedCharacterId) return;

        SaveManager.Instance.SetSelectedCharacter(data.characterId);

        RefreshSelectButton();
        lobbyCharacterDisplay.Refresh();
        homeCharacterInfoUI.Refresh();
    }
    private void RefreshSelectButton()
    {
        CharacterData data = characters[currentIndex];

        bool isUnlocked = SaveManager.Instance.IsCharacterUnlocked(data.characterId);

        bool isSelected = data.characterId == SaveManager.Instance.Data.selectedCharacterId;

        if (!isUnlocked)
        {
            selectButtonText.text = characterBuyText.GetLocalizedString(data.unlockPrice);
        }
        else if (isSelected)
        {
            selectButtonText.text = selectedText.GetLocalizedString();
        }
        else
        {
            selectButtonText.text = selectText.GetLocalizedString();
        }
    }
    private void RefreshCharacterInfo(CharacterData data)
    {
        PlayerSaveData save = SaveManager.Instance.Data;

        CharacterProgressData progress = SaveManager.Instance.GetCharacterProgress(data.characterId);

        int levelHp = CharacterLevelCalculator.GetMaxHp(data.maxHp, progress.level);   //레벨에 따른 hp 계산

        float levelAttack = CharacterLevelCalculator.GetAttack(data.attack, progress.level);  //레벨에 따른 공격력 계산

        int finalHp = PermanentUpgradeCalculator.GetMaxHp(levelHp, save, hpUpgradeItem);

        float finalAttack = PermanentUpgradeCalculator.GetAttack(levelAttack, save, attackUpgradeItem);

        float finalMoveSpeed = PermanentUpgradeCalculator.GetMoveSpeed(data.moveSpeed, save, moveSpeedUpgradeItem);

        characterNameText.text = data.localizedCharacterName.GetLocalizedString();

        characterLevelText.text = $"Lv. {progress.level}";

        hpText.text = $"HP : {finalHp}";
        attackText.text = $"ATK : {finalAttack:F1}";
        moveSpeedText.text = $"SPD : {finalMoveSpeed:F1}";

        skillIcon.sprite = data.skillIcon;

        skillNameText.text = data.localizedSkillName.GetLocalizedString();

        skillDescriptionText.text = data.localizedSkillDescription.GetLocalizedString();
    }

    private void RefreshLockState()
    {
        CharacterData data = characters[currentIndex];

        bool isUnlocked = SaveManager.Instance.IsCharacterUnlocked(data.characterId);

        lockPanel.SetActive(!isUnlocked);
    }

    public void RefreshCurrentInfo()
    {
        if (currentIndex < 0 || currentIndex >= characters.Length) return;

        RefreshCharacterInfo(characters[currentIndex]);
    }

    public void RefreshCurrentCharacter()
    {
        ShowCharacter(currentIndex);
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
    public void ToggleSkillInfo()  //스킬버튼 누르는거
    {
        skillInfoPanel.SetActive(!skillInfoPanel.activeSelf);
    }
    private int FindCharacterIndex(CharacterId id)
    {
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i].characterId == id) return i;
        }

        return -1;
    }
    public void RefreshLocalizedUI()
    {
        if (currentIndex < 0 || currentIndex >= characters.Length)
        {
            return;
        }

        RefreshCharacterInfo(characters[currentIndex]);
        RefreshSelectButton();
    }
}