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

    AugmentSkill,

    // 에셋에 정수로 저장되므로 새 타입은 항상 끝에 추가
    WallReboundCharge,

    // 전설 증강: 수치가 아니라 공의 규칙을 바꿈 (LegendaryEffects). 엘리트/보스 보상, 상점, 이벤트에서만 등장, 판당 1회 획득
    ExplosiveRebound,
    SplitShot,
    Magnet,
    PerfectThunder,
    ChainBlast,

    // 캐릭터 전용 증강 (CharacterAugmentEffects): 해당 캐릭터일 때만 등장, 판당 1회 획득
    CatReflex,
    PigShockwave,
    ChickFeathers,
    RabbitLuck
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

    // 에셋에 저장되므로 새 필드는 항상 끝에 추가
    [Header("Character Exclusive")]
    public bool characterExclusive;          //체크하면 아래 캐릭터일 때만 선택지에 나옴
    public CharacterId exclusiveCharacter;
}
