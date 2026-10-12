using UnityEngine;

public static class CharacterLevelCalculator
{
    private const float HpIncreasePerLevel = 0.05f;
    private const float AttackIncreasePerLevel = 0.05f;

    public static int GetMaxHp(int baseHp, int level)
    {
        return Mathf.RoundToInt(baseHp * (1f + (level - 1) * HpIncreasePerLevel));
    }

    public static float GetAttack(float baseAttack, int level)
    {
        return baseAttack * (1f + (level - 1) * AttackIncreasePerLevel);
    }
}