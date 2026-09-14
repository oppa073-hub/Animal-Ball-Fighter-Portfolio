using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
public class ShopItemCardUI : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text levelText;

    [Header("Localization")]
    [SerializeField] private LocalizedString notEnoughGoldText;
    [SerializeField] private LocalizedString purchaseCompleteText;

    private ShopItemData itemData;

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnGoldChanged += HandleGoldChanged;
        }

        RefreshLocalizedUI();
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnGoldChanged -= HandleGoldChanged;
        }
    }

    public void Initialize(ShopItemData data)
    {
        itemData = data;

        itemIcon.sprite = data.icon;

        itemNameText.text = data.localizedItemName.GetLocalizedString();

        buyButton.onClick.RemoveListener(Buy);
        buyButton.onClick.AddListener(Buy);

        RefreshUI();
    }

    private void Buy()
    {
        if (itemData == null) return;

        if (IsMaxLevel())
        {
            RefreshUI();
            return;
        }

        int currentPrice = GetCurrentPrice();

        bool success =
            SaveManager.Instance.SpendGold(currentPrice);

        if (!success)
        {
            ToastMessageManager.Instance?.Show(notEnoughGoldText.GetLocalizedString());

            return;
        }

        SaveManager.Instance.AddUpgradeLevel(itemData.itemType);

        RefreshUI();

        string localizedItemName = itemData.localizedItemName.GetLocalizedString();

        ToastMessageManager.Instance?.Show(purchaseCompleteText.GetLocalizedString(localizedItemName));
    }

    private void RefreshUI()
    {
        if (itemData == null) return;

        int level = GetCurrentLevel();

        string localizedEffectLabel = itemData.localizedEffectLabel.GetLocalizedString();

        levelText.text = $"Lv. {level}";

        if (IsMaxLevel())
        {
            descriptionText.text = $"{localizedEffectLabel} MAX";
            priceText.text = "MAX";
            buyButton.interactable = false;
            return;
        }

        float currentUpgradeValue = GetNextUpgradeValue();
        int currentPrice = GetCurrentPrice();

        descriptionText.text = $"{localizedEffectLabel} +{currentUpgradeValue:0.#}";

        priceText.text = $"{currentPrice:N0} G";

        buyButton.interactable = SaveManager.Instance.GetGold() >= currentPrice;
    }
    private bool IsMaxLevel()
    {
        if (itemData == null) return false;

        return itemData.itemType == ShopItemType.MoveSpeed && GetCurrentLevel() >= SaveManager.MoveSpeedUpgradeMaxLevel;
    }

    private int GetCurrentLevel()
    {
        PlayerSaveData save = SaveManager.Instance.Data;

        switch (itemData.itemType)
        {
            case ShopItemType.MaxHp:
                return save.maxHpUpgradeLevel;

            case ShopItemType.Attack:
                return save.attackUpgradeLevel;

            case ShopItemType.MoveSpeed:
                return save.moveSpeedUpgradeLevel;
        }

        return 0;
    }

    private int GetCurrentPrice()
    {
        int level = GetCurrentLevel();

        return itemData.basePrice + level * itemData.priceIncrease;
    }

    private float GetNextUpgradeValue()
    {
        int level = GetCurrentLevel();

        return itemData.baseValue + level * itemData.valueIncrease;
    }

    private void HandleGoldChanged(int gold)
    {
        if (itemData == null) return;

        RefreshUI();
    }
    public void RefreshLocalizedUI()
    {
        if (itemData == null) return;

        itemNameText.text = itemData.localizedItemName.GetLocalizedString();

        RefreshUI();
    }
    private void HandleLocaleChanged(Locale locale)
    {
        RefreshLocalizedUI();
    }
}