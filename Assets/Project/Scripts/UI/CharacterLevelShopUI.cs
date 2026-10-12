using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
public class CharacterLevelShopUI : MonoBehaviour
{
    [SerializeField] private Image characterIcon;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text characterLevelText;
    [SerializeField] private TMP_Text statPreviewText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button levelUpButton;

    [SerializeField] private ShopItemData hpUpgradeItem;
    [SerializeField] private ShopItemData attackUpgradeItem;

    [SerializeField] private int basePrice = 300;
    [SerializeField] private float priceIncreaseRate = 0.5f;
    [SerializeField] private int maxLevel = 20;

    [Header("Localization")]
    [SerializeField] private LocalizedString maxLevelText;
    [SerializeField] private LocalizedString maxLevelToastText;
    [SerializeField] private LocalizedString notEnoughGoldText;
    [SerializeField] private LocalizedString purchaseCompleteText;

    private CharacterId selectedId;

    private void Start()
    {
        TryRefresh();
    }
    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnGoldChanged += HandleGoldChanged;
            SaveManager.Instance.OnSelectedCharacterChanged += HandleCharacterChanged;
            SaveManager.Instance.OnUpgradeChanged += HandleUpgradeChanged;
        }

        TryRefresh();
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnGoldChanged -= HandleGoldChanged;
            SaveManager.Instance.OnSelectedCharacterChanged -= HandleCharacterChanged;
            SaveManager.Instance.OnUpgradeChanged -= HandleUpgradeChanged;
        }
    }

    public void Refresh()
    {
        selectedId = SaveManager.Instance.Data.selectedCharacterId;

        CharacterData data = CharacterDatabase.Instance.GetCharacter(selectedId);

        if (data == null)
        {
            Debug.LogError($"[CharacterLevelShopUI] {selectedId} 데이터를 찾을 수 없습니다.");
            return;
        }

        if (characterIcon != null)
        {
            characterIcon.sprite = data.characterIcon;
        }

        characterNameText.text = data.localizedCharacterName.GetLocalizedString();

        CharacterProgressData progress = SaveManager.Instance.GetCharacterProgress(selectedId);
        PlayerSaveData save = SaveManager.Instance.Data;

        // 현재 레벨 기본 성장
        int currentLevelHp = CharacterLevelCalculator.GetMaxHp(data.maxHp, progress.level);

        float currentLevelAttack = CharacterLevelCalculator.GetAttack(data.attack, progress.level);

        // 다음 레벨 기본 성장
        int nextLevelHp = CharacterLevelCalculator.GetMaxHp(data.maxHp, progress.level + 1);

        float nextLevelAttack = CharacterLevelCalculator.GetAttack(data.attack, progress.level + 1);

        // 공용 상점 강화까지 반영
        int currentHp = PermanentUpgradeCalculator.GetMaxHp(currentLevelHp, save, hpUpgradeItem);

        int nextHp = PermanentUpgradeCalculator.GetMaxHp(nextLevelHp, save, hpUpgradeItem);

        float currentAttack = PermanentUpgradeCalculator.GetAttack(currentLevelAttack, save, attackUpgradeItem);

        float nextAttack = PermanentUpgradeCalculator.GetAttack( nextLevelAttack, save, attackUpgradeItem);

        statPreviewText.text = $"HP  {currentHp} → {nextHp}\n" + $"ATK  {currentAttack:F1} → {nextAttack:F1}";

        int price = GetCurrentPrice(progress.level);

        if (progress.level >= maxLevel)
        {
            characterLevelText.text = $"Lv. {progress.level}";
            priceText.text = "MAX";
            statPreviewText.text = maxLevelText.GetLocalizedString();
            levelUpButton.interactable = false;
            return;
        }

        characterLevelText.text = $"Lv. {progress.level} → Lv. {progress.level + 1}";
        priceText.text = $"{price} G";

        levelUpButton.interactable = SaveManager.Instance.GetGold() >= price;
    }

    public void LevelUp()
    {
        CharacterProgressData progress = SaveManager.Instance.GetCharacterProgress(selectedId);

        if (progress.level >= maxLevel)
        {
            ToastMessageManager.Instance?.Show(maxLevelToastText.GetLocalizedString());
            return;
        }

        int price = GetCurrentPrice(progress.level);

        if (!SaveManager.Instance.SpendGold(price))
        {
            ToastMessageManager.Instance?.Show(notEnoughGoldText.GetLocalizedString());
            return;
        }
        SaveManager.Instance.LevelUpCharacter(selectedId);

        Refresh();
        ToastMessageManager.Instance?.Show(purchaseCompleteText.GetLocalizedString());
    }

    private int GetCurrentPrice(int level)
    {
        return Mathf.RoundToInt(basePrice * (1f + (level - 1) * priceIncreaseRate));
    }

    private void HandleGoldChanged(int gold)
    {
        Refresh();
    }
    private void HandleCharacterChanged(CharacterId characterId)
    {
        Refresh();
    }
    private void HandleUpgradeChanged(ShopItemType type)
    {
        Refresh();
    }
    private void HandleLocaleChanged(Locale locale)
    {
        TryRefresh();
    }

    private void TryRefresh()
    {
        if (SaveManager.Instance == null) return;
        if (CharacterDatabase.Instance == null) return;

        Refresh();
    }
}