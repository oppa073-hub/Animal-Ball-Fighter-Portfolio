using UnityEngine;

public class CatScratchSkill : MonoBehaviour, ISkill
{
    private PlayerStats playerStats;
    private Health playerHealth;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask obstacleLayer;

    [SerializeField] private GameObject scratchVfxPrefab;
    [SerializeField] private Transform vfxPoint;
    [SerializeField] private float vfxDuration = 1f;
    [SerializeField] private Vector3 vfxRotationOffset;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponentInParent<Rigidbody>();
        playerStats = GetComponentInParent<PlayerStats>();
        playerHealth = GetComponentInParent<Health>();
    }

    [SerializeField] private int vfxPoolSize = 6;  //5연타(A3) 동안 이펙트가 동시에 떠 있으므로 씬 풀(3개)보다 넉넉히

    private void Start()
    {
        if (scratchVfxPrefab != null && ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.EnsureCapacity(scratchVfxPrefab, vfxPoolSize);
        }
    }

    [Header("Skill Tree")]
    [SerializeField] private float baseDamageBonus = 0.3f;     //기본 강화: 피해 +30%
    [SerializeField] private float comboInterval = 0.12f;      //A: 연타 간격
    [SerializeField] private float ultimateRangeBonus = 0.2f;  //A 궁극: 범위 +20%
    [SerializeField] private Material clawMaterial;            //B: 발톱 투사체
    [SerializeField] private Material clawTrailMaterial;
    [SerializeField] private Color clawColor = new Color(1f, 0.85f, 0.95f);
    [SerializeField] private float clawSpeed = 14f;
    [SerializeField] private float clawLifeTime = 0.9f;
    [SerializeField] private float clawDamageRatio = 0.8f;     //스킬 피해 대비

    public void UseSkill()
    {
        float damageMultiplier = SkillTreeCatalog.HasNode(SkillTreeNode.Base) ? 1f + baseDamageBonus : 1f;
        int hitCount = SkillTreeCatalog.HasNode(SkillTreeNode.A3) ? 5 : SkillTreeCatalog.HasNode(SkillTreeNode.A2) ? 2 : 1;
        float rangeMultiplier = SkillTreeCatalog.HasNode(SkillTreeNode.A3) ? 1f + ultimateRangeBonus : 1f;

        StartCoroutine(ScratchCombo(hitCount, damageMultiplier, rangeMultiplier));
    }

    private System.Collections.IEnumerator ScratchCombo(int hitCount, float damageMultiplier, float rangeMultiplier)
    {
        for (int i = 0; i < hitCount; i++)
        {
            Vector3 direction = Scratch(damageMultiplier, rangeMultiplier);
            if (i == 0) FireClaws(direction, damageMultiplier);
            if (i < hitCount - 1) yield return new WaitForSeconds(comboInterval);
        }
    }

    private void FireClaws(Vector3 attackDirection, float damageMultiplier)  //B: 원거리 발톱
    {
        int count = SkillTreeCatalog.HasNode(SkillTreeNode.B3) ? 8 : SkillTreeCatalog.HasNode(SkillTreeNode.B2) ? 1 : 0;
        if (count == 0 || clawMaterial == null) return;

        float damage = playerStats.SkillDamage * damageMultiplier * clawDamageRatio;
        int pierce = count > 1 ? 3 : 2;

        for (int i = 0; i < count; i++)
        {
            Vector3 direction = count == 1 ? attackDirection : Quaternion.Euler(0f, 360f / count * i, 0f) * attackDirection;
            SkillProjectile.Spawn(transform.position + Vector3.up * 0.4f, direction, clawSpeed, damage, clawLifeTime, pierce,
                enemyLayer, clawMaterial, clawTrailMaterial, clawColor, 0.7f, playerStats, playerHealth);
        }
    }

    private Vector3 Scratch(float damageMultiplier, float rangeMultiplier)  //공격 방향을 돌려줌
    {
        float range = playerStats.SkillRange * rangeMultiplier;
        Collider[] targets = Physics.OverlapSphere(transform.position, range, enemyLayer);

        Vector3 attackDirection = GetAttackDirection(targets);

        SoundManager.Instance?.PlaySFX(SoundId.CatSkill);

        PlayVfx(attackDirection);

        if (targets.Length == 0) return attackDirection;

        foreach (var target in targets)
        {
            Transform targetTr = target.transform;
            Vector3 dirToTarget = (targetTr.position - transform.position).normalized;

            float angle = Vector3.Angle(attackDirection, dirToTarget);

            if (angle <= playerStats.SkillAngle * 0.5f)
            {
                float distance = Vector3.Distance(transform.position, targetTr.position);

                if (!Physics.Raycast(transform.position, dirToTarget, distance, obstacleLayer))
                {
                    int dealtDamage = DamageManager.Instance.ApplyFixedDamage(target.gameObject, playerStats.SkillDamage * damageMultiplier, DamageTextType.Normal);
                    playerStats.ApplyLifeSteal(dealtDamage, playerHealth);
                }
            }
        }

        return attackDirection;
    }

    private Vector3 GetAttackDirection(Collider[] targets)  //적 가까우면 적보고 아니면 걍 앞보기
    {
        Transform closetTarget = null;
        float closetDistance = float.MaxValue;

        foreach (Collider target in targets)
        {
            float distance = Vector3.Distance(transform.position, target.transform.position);

            if (distance < closetDistance)
            {
                closetDistance = distance;
                closetTarget = target.transform;
            }
        }

        if (closetTarget != null)
        {
            Vector3 direction = closetTarget.position - transform.position;
            direction.y = 0;
            return direction.normalized;
        }

        Vector3 velocityDirection = rb.linearVelocity;
        velocityDirection.y = 0f;

        if (velocityDirection.sqrMagnitude > 0.01f)
        {
            return velocityDirection.normalized;
        }

        return transform.forward;
    }
    private void OnDrawGizmosSelected()
    {
        float range = playerStats != null ? playerStats.SkillRange : 3;
        float angle = playerStats != null ? playerStats.SkillAngle : 120;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, range);

        Collider[] targets = Physics.OverlapSphere(transform.position, range, enemyLayer);

        Vector3 attackDirection = transform.forward;

        if (targets.Length > 0)
        {
            Transform closestTarget = null;
            float closestDistance = float.MaxValue;

            foreach (Collider target in targets)
            {
                float distance = Vector3.Distance(transform.position, target.transform.position);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = target.transform;
                }
            }

            if (closestTarget != null)
            {
                attackDirection = closestTarget.position - transform.position;
                attackDirection.y = 0f;
                attackDirection.Normalize();

                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, closestTarget.position);
            }
        }

        Vector3 leftDir = Quaternion.Euler(0f, -angle * 0.5f, 0f) * attackDirection;
        Vector3 rightDir = Quaternion.Euler(0f, angle * 0.5f, 0f) * attackDirection;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + leftDir * range);
        Gizmos.DrawLine(transform.position, transform.position + rightDir * range);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + attackDirection * range);
    }
    private void PlayVfx(Vector3 attackDirection)
    {
        if (scratchVfxPrefab == null) return;

        Transform spawnPoint = vfxPoint != null ? vfxPoint : transform;

        // 실제 할퀴기 공격 방향
        Quaternion directionRotation = Quaternion.LookRotation(attackDirection, Vector3.up);

        // VFX 원본 축 보정
        Quaternion offsetRotation = Quaternion.Euler(vfxRotationOffset);

        Quaternion finalRotation = directionRotation * offsetRotation;

        VFXManager.Instance.Play(scratchVfxPrefab, spawnPoint.position, finalRotation, vfxDuration);
    }
}
