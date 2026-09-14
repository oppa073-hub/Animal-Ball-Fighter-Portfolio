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

    public void UseSkill()
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, playerStats.SkillRange, enemyLayer);

        Vector3 attackDirection = GetAttackDirection(targets);

        SoundManager.Instance?.PlaySFX(SoundId.CatSkill);

        PlayVfx(attackDirection);

        if (targets.Length == 0)
        {
            Debug.Log("범위 안 적 없음");
            return;
        }

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
                    int dealtDamage = DamageManager.Instance.ApplyFixedDamage(target.gameObject, playerStats.SkillDamage, DamageTextType.Normal);
                    playerStats.ApplyLifeSteal(dealtDamage, playerHealth);
                    Debug.Log("적 스킬 명중");
                }
            }
        }
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
