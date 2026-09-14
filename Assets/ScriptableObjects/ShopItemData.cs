using UnityEngine;
using UnityEngine.Localization;

public enum ShopItemType
{
    MaxHp,
    Attack,
    MoveSpeed
}

[CreateAssetMenu(fileName = "ShopItemData", menuName = "Game/Shop Item Data")]
public class ShopItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    public string description;
    public string effectLabel;

    public ShopItemType itemType;

    [Header("Upgrade")]
    public float baseValue;       // 첫 구매 증가량
    public float valueIncrease;   // 구매할 때마다 증가량

    public int basePrice;         // 첫 구매 가격
    public int priceIncrease;     // 구매할 때마다 가격 증가

    [Header("Localization")]
    public LocalizedString localizedItemName;
    public LocalizedString localizedEffectLabel;

    public float GetTotalUpgradeValue(int level)
    {
        float total = 0f;

        for (int i = 0; i < level; i++)
        {
            total += baseValue + i * valueIncrease;
        }

        return total;
    }
}