using UnityEngine;
using UnityEngine.Localization;

public enum EnemyAttackType
{
    Melee, Ranged, Boss
}
public enum BossId
{
    Robot,
    Hero,

    // 에셋에 정수로 저장되므로 새 보스는 항상 끝에 추가
    Witch
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
    public StatusEffectData projectileStatusEffect;  //투사체 적중 시 상태이상 (비어 있으면 데미지만)

    [Header("Localization")]
    public LocalizedString localizedEnemyName;

    // 에셋에 저장되므로 새 필드는 항상 끝에 추가
    [Header("Special Role (일반 특수 적)")]
    public EnemyRole role = EnemyRole.None;  //방패/폭탄/치유/가시 (EnemyRoleController)
    public Color tint = Color.white;         //같은 모델을 색으로 구분 (흰색이면 프리팹 기본 색)
}

public enum EnemyRole
{
    None,
    Shield,   //정면 충돌 피해 감소 (옆/뒤를 노려야 함)
    Bomber,   //죽으면 폭발해 주변 적에게 피해
    Healer,   //주기적으로 주변 적 회복
    Spiky     //몸으로 부딪히면 플레이어도 피해 (퍼펙트 반사 충전 중엔 안전)
}
