using System.Collections.Generic;
using UnityEngine.Localization.Settings;

// 증강 시너지: 두 종류의 증강을 모두 가지면 한 번 추가 보너스. 이름은 Localization UI 테이블 키로 표시.
public class AugmentSynergy
{
    public readonly string nameKey;
    public readonly AugmentType first;
    public readonly AugmentType second;
    public readonly System.Action<PlayerStats, PlayerShield> applyBonus;

    private AugmentSynergy(string nameKey, AugmentType first, AugmentType second, System.Action<PlayerStats, PlayerShield> applyBonus)
    {
        this.nameKey = nameKey;
        this.first = first;
        this.second = second;
        this.applyBonus = applyBonus;
    }

    public static readonly IReadOnlyList<AugmentSynergy> All = new List<AugmentSynergy>
    {
        new AugmentSynergy("synergy_rebound_smash", AugmentType.WallReboundCharge, AugmentType.CollisionDamageUp, (stats, shield) => stats.AddWallReboundBonus(0.1f)),
        new AugmentSynergy("synergy_blood_strike", AugmentType.LifeSteal, AugmentType.CriticalDamageUp, (stats, shield) => stats.AddLifeStealPercent(0.05f)),
        new AugmentSynergy("synergy_iron_wall", AugmentType.Shield, AugmentType.MaxHpUp, (stats, shield) => shield?.AddMaxShield(30)),
        new AugmentSynergy("synergy_gale", AugmentType.MoveSpeedUp, AugmentType.AttackUp, (stats, shield) => stats.AddCollisionDamagePercent(0.15f)),
        new AugmentSynergy("synergy_precision", AugmentType.CriticalChanceUp, AugmentType.CriticalDamageUp, (stats, shield) => stats.AddCriticalChance(0.05f)),
    };

    public bool Involves(AugmentType type) => type == first || type == second;

    public string LocalizedName
    {
        get
        {
            string localized = LocalizationSettings.StringDatabase.GetLocalizedString("UI", nameKey);
            return string.IsNullOrEmpty(localized) ? nameKey : localized;
        }
    }
}
