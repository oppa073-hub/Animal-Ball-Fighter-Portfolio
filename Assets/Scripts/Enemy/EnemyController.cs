using UnityEngine;
using System.Collections;
using UnityEngine.Localization;
public class EnemyController : MonoBehaviour
{
    private Health health;

    private string enemyName;
    private int maxHp;
    private float attack;
    private float attackInterval;
    private float moveSpeed;
    private float projectileDamage;
    private EnemyAttackType attackType;
    private StatusEffectData statusEffectOnHit;
    private EnemyVisualController visualController;
    private EnemyAnimationController animationController;
    private EnemyBallController ballController;
    private bool isDead;
    private LocalizedString localizedEnemyName;

    [SerializeField] private float deathAnimationDuration = 1f;

    public string EnemyName
    {
        get
        {
            if (localizedEnemyName != null && !localizedEnemyName.IsEmpty)
            {
                return localizedEnemyName.GetLocalizedString();
            }

            return enemyName;
        }
    }
    public int MaxHp => maxHp;
    public float Attack => attack;
    public float MoveSpeed => moveSpeed;
    public float AttackInterval => attackInterval;
    public float ProjectileDamage => projectileDamage;
    public StatusEffectData StatusEffectOnHit => statusEffectOnHit;
    public bool IsDead => isDead;

    private void Awake()
    {
        health = GetComponent<Health>();
        visualController = GetComponent<EnemyVisualController>();
        animationController = GetComponent<EnemyAnimationController>();
        ballController = GetComponent<EnemyBallController>();
    }
    private void OnEnable()
    {
        if (health != null) health.OnDeath += HandleEnemyDeath;
    }

    private void OnDisable()
    {
        if (health != null) health.OnDeath -= HandleEnemyDeath;
    }

    public void Initialize(EnemyData enemyData, int roomNumber)
    {
        if (enemyData != null)
        {
            enemyName = enemyData.enemyName;
            localizedEnemyName = enemyData.localizedEnemyName;
            maxHp = enemyData.maxHp;
            attack = enemyData.attack;
            attackInterval = enemyData.attackInterval;
            moveSpeed = enemyData.moveSpeed;
            projectileDamage = enemyData.projectileDamage;
            attackType = enemyData.attackType;
            statusEffectOnHit = enemyData.statusEffectOnHit;
        }
        isDead = false;
        visualController?.SetRandomModel();

        float hpBonus;
        float attackBonus;

        if (GameSessionData.IsEndless)
        {
            GetEndlessBonus(roomNumber, out hpBonus, out attackBonus);
        }
        else
        {
            hpBonus = (roomNumber - 1) * 0.10f;
            attackBonus = (roomNumber - 1) * 0.07f;
        }

        maxHp = Mathf.RoundToInt(maxHp * (1 + hpBonus));
        attack = attack * (1 + attackBonus);
        projectileDamage = enemyData.projectileDamage * (1 + attackBonus);

        if (health != null) health.Initialize(maxHp);
        RoomManager.Instance.RegisterEnemy(this);

        if (gameObject.TryGetComponent<RangedEnemyAttack>(out RangedEnemyAttack result))
        {
            result.Initialize(enemyData);
        }
    }
    private void GetEndlessBonus(int roomNumber, out float hpBonus, out float attackBonus)
    {
        int clearedRooms = roomNumber - 1;

        int earlyRooms = Mathf.Min(clearedRooms, 10);
        int midRooms = Mathf.Clamp(clearedRooms - 10, 0, 20);
        int lateRooms = Mathf.Max(clearedRooms - 30, 0);

        hpBonus =
            earlyRooms * 0.15f +
            midRooms * 0.10f +
            lateRooms * 0.05f;

        attackBonus =
            earlyRooms * 0.10f +
            midRooms * 0.07f +
            lateRooms * 0.03f;
    }
    public void PrepareForPlayerRevive()
    {
        if (isDead) return;

        ballController?.PrepareForPlayerRevive();
    }

    public void HandleEnemyDeath()
    {
        RoomManager.Instance.OnEnemyDead(this);

        if (attackType == EnemyAttackType.Boss) UIManager.Instance.HideBossHealth();

        isDead = true;
        ballController?.SetDead(true);

        animationController?.PlayDie();

        SoundManager.Instance?.PlaySFX(SoundId.EnemyDeath);

        StartCoroutine(ReturnAfterDeathAnimation());
    }
    private IEnumerator ReturnAfterDeathAnimation()
    {
        yield return new WaitForSeconds(deathAnimationDuration);

        GetComponent<PooledObject>().ReturnToPool();
    }
}

