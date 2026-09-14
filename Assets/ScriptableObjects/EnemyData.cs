using UnityEngine;
using UnityEngine.Localization;

public enum EnemyAttackType
{
    Melee, Ranged, Boss
}
public enum BossId
{
    Robot,
    Hero
}

[CreateAssetMenu(menuName = "Game/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Base Stats")]
    public string enemyName = "Enemy";
    public int maxHp = 100;
    public float attack = 10f;
    public float attackInterval = 1.5f;
    public float moveSpeed = 12f;
    public StatusEffectData statusEffectOnHit;

    public EnemyAttackType attackType = EnemyAttackType.Melee;
    public GameObject enemyPrefab;

    [Header("Boss")]
    public BossId bossId;

    [Header("Projectile")]  //원거리 공격만 씀
    public float projectileDamage = 10f;
    public float attackRange = 6f;
    public float projectileSpeed = 14f;
    public float projectileLifeTime = 3f;
    public GameObject projectilePrefab;

    [Header("Localization")]
    public LocalizedString localizedEnemyName;
}
