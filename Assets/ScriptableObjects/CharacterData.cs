using UnityEngine.Localization;
using UnityEngine;
public enum CharacterId
{
    Cat,
    Pig,
    Chick
}

[CreateAssetMenu(menuName = "Game/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Base Stats")]
    public int maxHp = 100;
    public float attack = 10f;
    public float skillDamage = 15f;
    public float moveSpeed = 12f;

    [Header("Critical")]
    [Range(0f, 1f)] public float criticalChance = 0.05f;
    public float criticalDamage = 1.5f;

    [Header("Skill")]
    public KeyCode skillKey = KeyCode.Space;  //원래 모바일이지만 일단 테스트용
    public float skillCooldown = 5f;
    public float skillRange = 3f;
    public float skillAngle = 120f;

    [Header("Character")]
    public GameObject characterPrefab;
    public GameObject skillPrefab;
    public string characterName;
    public CharacterId characterId;

    [Header("Unlock")]
    public int unlockPrice = 0;

    [Header("Skill UI")]
    public string skillName;
    public Sprite skillIcon;
    [TextArea]
    public string skillDescription;

    [Header("Lobby Animation")]
    public RuntimeAnimatorController lobbyAnimatorController;

    public Sprite characterIcon;

    [Header("Localization")]
    public LocalizedString localizedCharacterName;
    public LocalizedString localizedSkillName;
    public LocalizedString localizedSkillDescription;
}