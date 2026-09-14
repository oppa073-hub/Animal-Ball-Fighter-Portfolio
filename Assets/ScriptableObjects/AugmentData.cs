using UnityEngine;
using UnityEngine.Localization;

public enum AugmentGrade
{
    Common,Rare,Epic,Legendary
}
public enum AugmentType
{
    AttackUp,
    MaxHpUp,
    MoveSpeedUp,
    SkillDamageUp,
    CriticalChanceUp,
    CriticalDamageUp,
    CollisionDamageUp,
    HealOnRoomClear,
    LifeSteal,
    Shield,

    AugmentSkill
}

[CreateAssetMenu(menuName = "Game/Augment Data")]
public class AugmentData : ScriptableObject
{
    public string augmentName;
    public string description;
    public AugmentGrade grade;
    public AugmentType augmentType;
    public float value;
    public Sprite icon;

    [Header("Skill Augment")]
    public AugmentSkillData augmentSkill;

    [Header("Localization")]
    public LocalizedString localizedAugmentName;
    public LocalizedString localizedDescription;
}
