using UnityEngine;
using UnityEngine.Localization;

public enum AugmentSkillType
{
    Active,
    Auto
}

[CreateAssetMenu(menuName = "Game/Augment Skill Data")]
public class AugmentSkillData : ScriptableObject
{
    public string skillName;
    public AugmentSkillType skillType;

    public GameObject skillPrefab;

    public float cooldown = 5f;
    public int specialUpgradeLevel = 5;

    [Header("Localization")]
    public LocalizedString localizedUpgradeDescription;
}