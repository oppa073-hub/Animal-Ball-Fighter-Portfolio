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

    [Header("Skill Tree")]
    [SerializeField] private float baseDamageBonus = 0.3f;       //기본 강화
    [SerializeField] private float wideRangeBonus = 0.4f;        //A 2단계
    [SerializeField] private float ultimateRangeBonus = 1f;      //A 궁극: 범위 2배
    [SerializeField] private float ultimateDamageMultiplier = 2f;
    [SerializeField] private float ultimateKnockback = 14f;
    [SerializeField] private float burstInterval = 0.35f;        //B: 연속 폭발 간격
    [SerializeField] private Material featherMaterial;           //B 궁극: 깃털
    [SerializeField] private Material featherTrailMaterial;
    [SerializeField] private Color featherColor = new Color(1f, 0.92f, 0.45f);
    [SerializeField] private float featherSpeed = 12f;
    [SerializeField] private float featherLifeTime = 1f;
    [SerializeField] private float featherDamageRatio = 0.6f;

    public void UseSkill()
    {
        int burstCount = SkillTreeCatalog.HasNode(SkillTreeNode.B3) ? 3 : SkillTreeCatalog.HasNode(SkillTreeNode.B2) ? 2 : 1;
        StartCoroutine(BurstSequence(burstCount));
    }

    private System.Collections.IEnumerator BurstSequence(int burstCount)
    {
        for (int i = 0; i < burstCount; i++)
        {
            Burst();
            if (i < burstCount - 1) yield return new WaitForSeconds(burstInterval);
        }
    }

    private void Burst()
    {
        SoundManager.Instance?.PlaySFX(SoundId.ChickSkill);

        bool ultimateA = SkillTreeCatalog.HasNode(SkillTreeNode.A3);
        float rangeMultiplier = ultimateA ? 1f + ultimateRangeBonus : SkillTreeCatalog.HasNode(SkillTreeNode.A2) ? 1f + wideRangeBonus : 1f;

        PlayVfx(rangeMultiplier);
        float damage = playerStats.SkillDamage * (SkillTreeCatalog.HasNode(SkillTreeNode.Base) ? 1f + baseDamageBonus : 1f) * (ultimateA ? ultimateDamageMultiplier : 1f);

        Collider[] targets = Physics.OverlapSphere(transform.position, playerStats.SkillRange * rangeMultiplier, enemyLayer);

        foreach (Collider target in targets)
        {
            int dealtDamage = DamageManager.Instance.ApplyFixedDamage(target.gameObject, damage, DamageTextType.Normal);

            playerStats.ApplyLifeSteal(dealtDamage, playerHealth);

            if (ultimateA && target.attachedRigidbody != null)  //A 궁극: 적을 밀쳐냄
            {
                Vector3 away = target.transform.position - transform.position;
                away.y = 0f;
                if (away.sqrMagnitude > 0.01f) target.attachedRigidbody.AddForce(away.normalized * ultimateKnockback, ForceMode.Impulse);
            }
        }

        if (SkillTreeCatalog.HasNode(SkillTreeNode.B3) && featherMaterial != null)  //B 궁극: 깃털 8방향
        {
            float startAngle = Random.Range(0f, 45f);
            for (int i = 0; i < 8; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, startAngle + 45f * i, 0f) * Vector3.forward;
                SkillProjectile.Spawn(transform.position + Vector3.up * 0.4f, direction, featherSpeed, damage * featherDamageRatio, featherLifeTime, 2,
                    enemyLayer, featherMaterial, featherTrailMaterial, featherColor, 0.55f, playerStats, playerHealth);
            }
        }
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
    private void PlayVfx(float scale)
    {
        if (wingVfxPrefab == null) return;

        Transform point = vfxPoint != null ? vfxPoint : transform;

        GameObject vfx = VFXManager.Instance.Play(wingVfxPrefab, point.position, Quaternion.Euler(vfxRotationOffset), vfxDuration);
        if (vfx == null) return;

        // 범위 강화가 눈에 보이도록 이펙트도 같이 키움. 풀 오브젝트라 매번 크기를 다시 지정한다(이 이펙트는 병아리 스킬만 씀)
        foreach (ParticleSystem particle in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particle.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        vfx.transform.localScale = wingVfxPrefab.transform.localScale * scale;
    }
}