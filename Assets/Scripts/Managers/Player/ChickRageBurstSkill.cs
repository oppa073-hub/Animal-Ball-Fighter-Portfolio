using UnityEngine;

public class ChickRageBurstSkill : MonoBehaviour, ISkill
{
    [SerializeField] private LayerMask enemyLayer;

    [Header("VFX")]
    [SerializeField] private GameObject wingVfxPrefab;
    [SerializeField] private Transform vfxPoint;
    [SerializeField] private float vfxDuration = 1f;
    [SerializeField] private Vector3 vfxRotationOffset;

    private PlayerStats playerStats;
    private Health playerHealth;
    private void Awake()
    {
        playerStats = GetComponentInParent<PlayerStats>();
        playerHealth = GetComponentInParent<Health>();
    }

    public void UseSkill()
    {
        SoundManager.Instance?.PlaySFX(SoundId.ChickSkill);

        PlayVfx();

        Collider[] targets = Physics.OverlapSphere(transform.position, playerStats.SkillRange, enemyLayer);

        foreach (Collider target in targets)
        {
            int dealtDamage = DamageManager.Instance.ApplyFixedDamage(target.gameObject, playerStats.SkillDamage, DamageTextType.Normal);

            playerStats.ApplyLifeSteal(dealtDamage, playerHealth);
        }

        Debug.Log($"[ChickSkill] 날개치기 / 명중:{targets.Length}");
    }
    private void OnDrawGizmosSelected()
    {
        float range = playerStats != null? playerStats.SkillRange: 2.5f;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, range);

        Collider[] targets = Physics.OverlapSphere(transform.position,range,enemyLayer);

        Gizmos.color = Color.red;

        foreach (Collider target in targets)
        {
            Gizmos.DrawLine(transform.position,target.transform.position);
        }
    }
    private void PlayVfx()
    {
        if (wingVfxPrefab == null) return;

        Transform point = vfxPoint != null ? vfxPoint : transform;

        VFXManager.Instance.Play(wingVfxPrefab, point.position, Quaternion.Euler(vfxRotationOffset), vfxDuration);
    }
}