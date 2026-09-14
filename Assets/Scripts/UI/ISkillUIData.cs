using UnityEngine;

public interface ISkillUIData
{
    Sprite SkillIcon { get; }
    float CooldownRatio { get; }

    void TryUseSkill();
}