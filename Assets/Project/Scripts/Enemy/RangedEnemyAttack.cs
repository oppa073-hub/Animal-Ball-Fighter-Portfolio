using UnityEngine;

public class RangedEnemyAttack : MonoBehaviour
{
    [SerializeField] private Transform firePoint;
    private EnemyData enemyData;
    private Transform player;
    private float attackTimer;
    private EnemyController enemyController;
    private EnemyAnimationController animationController;
    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
        player = GameObject.FindGameObjectWithTag("Player").transform;
        animationController = GetComponent<EnemyAnimationController>();
    }

    public void Initialize(EnemyData Data)
    {
        enemyData = Data;
        attackTimer = 0f;

        // 씬 풀 목록에 없는 투사체(예: 마녀 초록 총알)도 쓸 수 있도록 등록 (이미 있으면 바로 반환)
        if (enemyData != null && enemyData.projectilePrefab != null && ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.RegisterPool(enemyData.projectilePrefab, 20);
        }
    }
    public bool Suppressed { get; set; }  //보스 패턴 중 등 기본 원거리 공격을 잠시 멈춤

    private void Update()
    {
        if (Suppressed) return;
        if (enemyController.IsDead) return;
        if (enemyData == null) return;
        if (GameManager.Instance.currentState != GameState.Playing) return; 

        if (enemyData.attackType != EnemyAttackType.Ranged &&
            enemyData.attackType != EnemyAttackType.Boss)
            return;

        float distance = Vector3.Distance(transform.position, player.position);  

        if (distance > enemyData.attackRange) return;

        attackTimer += Time.deltaTime;

        if (attackTimer >= enemyData.attackInterval)
        {
            FireProjectile();
            attackTimer = 0f;
        }
    }
    private void FireProjectile()  //발사체 발사
    {
        animationController?.PlayRangedAttack();

        SoundManager.Instance?.PlaySFX(SoundId.EnemyShoot);

        Vector3 direction = (player.position - firePoint.position).normalized;  //플레이어 방향 계산

        GameObject projectileObj =ObjectPoolManager.Instance.GetObject(enemyData.projectilePrefab, firePoint.position, Quaternion.LookRotation(direction));

        if (projectileObj == null)
        {
            Debug.LogWarning("[RangedEnemyAttack] 투사체 풀이 부족합니다.");

            return;
        }

        EnemyProjectile projectile = projectileObj.GetComponent<EnemyProjectile>();

        if (projectile == null)
        {
            Debug.LogError("[RangedEnemyAttack] EnemyProjectile가 없습니다.");

            projectileObj.GetComponent<PooledObject>() ?.ReturnToPool();

            return;
        }

        if (projectile == null)
        {
            Debug.LogError("발사체에 EnemyProjectile 컴포넌트가 없습니다.");
            return;
        }

        projectile.Initialize(  //발사체 초기화
            direction,
            enemyController.ProjectileDamage,
            enemyData.projectileSpeed,
            enemyData.projectileLifeTime,
            enemyData.projectileStatusEffect
        );
    }
}
