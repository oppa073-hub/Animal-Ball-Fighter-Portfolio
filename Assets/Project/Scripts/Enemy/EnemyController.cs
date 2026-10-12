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
    public bool IsBoss => attackType == EnemyAttackType.Boss;

    public void ApplyMoveSpeedMultiplier(float multiplier)  //엘리트 특성 등 (Initialize 때마다 기본값으로 돌아감)
    {
        moveSpeed *= multiplier;
    }

    public void ApplyStatMultiplier(float hpMultiplier, float attackMultiplier)  //소환/분열 적 보정 등. Initialize 직후 호출 (다음 Initialize 때 기본값으로 돌아감)
    {
        maxHp = Mathf.RoundToInt(maxHp * hpMultiplier);
        attack *= attackMultiplier;
        projectileDamage *= attackMultiplier;

        if (health != null) health.Initialize(maxHp);
    }
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

    public EnemyData Data { get; private set; }  //도감/보스 첫 처치 보상 등에서 종류 구분용

    public void Initialize(EnemyData enemyData, int roomNumber)
    {
        Data = enemyData;

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

        // 특수 역할(방패/폭탄/치유/가시): 색과 부착물. 풀에서 다른 종류로 다시 쓰일 수 있으므로 항상 갱신
        EnemyRole role = enemyData != null ? enemyData.role : EnemyRole.None;
        if (visualController != null)
        {
            if (role != EnemyRole.None) visualController.SetTint(enemyData.tint);
            else visualController.ResetTint();
        }
        EnemyRoleController.Apply(this, role);

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

        bool isBossEnemy = attackType == EnemyAttackType.Boss;
        float runHpMultiplier = RunState.EnemyHpMultiplier * RunState.AscensionEnemyHp * (isBossEnemy ? RunState.AscensionBossHp : 1f);  //이벤트 저주, 승천

        maxHp = (int)Mathf.Min(Mathf.Round(maxHp * (1 + hpBonus) * runHpMultiplier * RemoteTuning.GetEnemyHpMultiplier(isBossEnemy)), MaxEnemyHp);  //원격 밸런스 조정 (기본 1)
        attack = attack * (1 + attackBonus) * RunState.AscensionEnemyAttack;
        projectileDamage = enemyData.projectileDamage * (1 + attackBonus) * RunState.AscensionEnemyAttack;

        if (health != null) health.Initialize(maxHp);
        RoomManager.Instance.RegisterEnemy(this);
        GameEvents.RaiseEnemySpawned(this);

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

        // 30방 이후 복리 성장: 선형만으로는 증강 누적을 못 따라가 후반이 오히려 쉬워짐
        if (lateRooms > 0)
        {
            hpBonus = (1f + hpBonus) * Mathf.Pow(EndlessLateHpGrowth, lateRooms) - 1f;
            attackBonus = (1f + attackBonus) * Mathf.Pow(EndlessLateAttackGrowth, lateRooms) - 1f;
        }

        hpBonus = Mathf.Min(hpBonus, MaxEndlessBonus);
        attackBonus = Mathf.Min(attackBonus, MaxEndlessBonus);
    }
    private const float EndlessLateHpGrowth = 1.05f;  //30방 이후 방마다 체력 +5% 복리
    private const float EndlessLateAttackGrowth = 1.03f;  //30방 이후 방마다 공격력 +3% 복리
    private const float MaxEndlessBonus = 100000f;  //오버플로 방지
    private const float MaxEnemyHp = 1000000000f;
    public void PrepareForPlayerRevive()
    {
        if (isDead) return;

        ballController?.PrepareForPlayerRevive();
    }

    public void HandleEnemyDeath()
    {
        bool isBoss = attackType == EnemyAttackType.Boss;
        if (isBoss) HapticFeedback.Heavy();
        else HapticFeedback.Medium();

        GameEvents.RaiseEnemyKilled(this, isBoss);  //마지막 적이면 OnEnemyDead에서 방 클리어 이벤트가 나가므로 그보다 먼저

        foreach (IEnemyDeathHook hook in GetComponents<IEnemyDeathHook>()) hook.OnBeforeEnemyDeath(this);  //분열 소환 등은 방 클리어 판정 전에

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

    public void ReturnToPoolNow()  //다음 방 시작 시 사망 연출 중인 적을 즉시 회수
    {
        StopAllCoroutines();

        GetComponent<PooledObject>().ReturnToPool();
    }
}

