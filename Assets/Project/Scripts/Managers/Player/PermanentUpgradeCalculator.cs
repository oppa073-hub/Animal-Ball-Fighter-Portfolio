using UnityEngine;

public static class PermanentUpgradeCalculator
{
    public const float MaxMoveSpeed = 20f;

    public static int GetMaxHp(int baseHp, PlayerSaveData save, ShopItemData hpItem)
    {
        float bonus = hpItem.GetTotalUpgradeValue(save.maxHpUpgradeLevel);

        return Mathf.RoundToInt(baseHp + bonus);
    }

    public static float GetAttack(float baseAttack, PlayerSaveData save, ShopItemData attackItem)
    {
        float bonus = attackItem.GetTotalUpgradeValue(save.attackUpgradeLevel);

        return baseAttack + bonus;
    }

    public static float GetMoveSpeed(float baseSpeed, PlayerSaveData save, ShopItemData speedItem)
    {
        float bonus = speedItem.GetTotalUpgradeValue(save.moveSpeedUpgradeLevel);

        return Mathf.Min(baseSpeed + bonus, MaxMoveSpeed);
    }
}