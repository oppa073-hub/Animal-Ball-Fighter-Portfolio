using System.Collections;
using UnityEngine;

public class PigChargeSkill : MonoBehaviour, ISkill
{
    [Header("Charge")]
    [SerializeField] private float duration = 1.5f;
    [SerializeField] private float speedMultiplier = 1.8f;
    [SerializeField] private float collisionDamageMultiplier = 1.5f;

    [Header("VFX")]
    [SerializeField] private GameObject chargeVfxPrefab;
    [SerializeField] private Transform vfxPoint;
    [SerializeField] private Vector3 vfxRotationOffset;
    private GameObject activeChargeVfx;
    private Rigidbody rb;

    private PlayerStats playerStats;
    private PlayerBallController playerController;

    private Coroutine chargeCoroutine;
    private Health playerHealth;
    private bool isCharging;

    [Header("Skill Tree")]
    [SerializeField] private float baseDurationBonus = 0.7f;        //기본 강화
    [SerializeField] private float armoredDamageMultiplier = 2.2f;  //A 2단계
    [SerializeField] private float ultimateDamageMultiplier = 3f;   //A 궁극 (+무적)
    [SerializeField] private LayerMask enemyLayer;                  //B: 충격파
    [SerializeField] private GameObject shockwaveVfxPrefab;
    [SerializeField] private float shockwaveRadius = 2.3f;
    [SerializeField] private float shockwaveInterval = 0.35f;       //B 궁극: 돌진 중 주기 폭발

    private void Awake()
    {
        playerStats = GetComponentInParent<PlayerStats>();
        playerController = GetComponentInParent<PlayerBallController>();
        rb = GetComponentInParent<Rigidbody>();
        playerHealth = GetComponentInParent<Health>();
    }

    private void OnEnable()
    {
        PlayerBallController.OnPlayerHitEnemy += HandleHitEnemy;
    }

    private void HandleHitEnemy(GameObject enemy)  //B: 돌진 중 부딪친 자리에서 폭발
    {
        if (!isCharging || !SkillTreeCatalog.HasNode(SkillTreeNode.B2)) return;

        Shockwave(enemy.transform.position);
    }

    private void Shockwave(Vector3 center)
    {
        SkillAreaDamage.Explode(center, shockwaveRadius, playerStats.SkillDamage, enemyLayer, shockwaveVfxPrefab, playerStats, playerHealth);
        CombatFeedbackManager.Instance?.RequestImpact(SoundId.FireBurstSkill, 0.1f, 0.15f, priority: 2);
    }
    private void LateUpdate()
    {
        UpdateChargeVfxDirection();
    }

    private void UpdateChargeVfxDirection()
    {
        if (activeChargeVfx == null || !activeChargeVfx.activeSelf)
            return;

        Vector3 direction = rb.linearVelocity;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        Quaternion directionRotation =
            Quaternion.LookRotation(direction.normalized, Vector3.up);

        activeChargeVfx.transform.rotation =
            directionRotation * Quaternion.Euler(vfxRotationOffset);
    }

    public void UseSkill()
    {
        if (chargeCoroutine != null) StopCoroutine(chargeCoroutine);

        chargeCoroutine = StartCoroutine(Charge());
    }

    private IEnumerator Charge()
    {
        SoundManager.Instance?.PlaySFX(SoundId.PigSkill);

        float chargeDuration = duration + (SkillTreeCatalog.HasNode(SkillTreeNode.Base) ? baseDurationBonus : 0f);
        float damageMultiplier = SkillTreeCatalog.HasNode(SkillTreeNode.A3) ? ultimateDamageMultiplier
            : SkillTreeCatalog.HasNode(SkillTreeNode.A2) ? armoredDamageMultiplier : collisionDamageMultiplier;
        bool invulnerable = SkillTreeCatalog.HasNode(SkillTreeNode.A3);
        bool periodicShockwave = SkillTreeCatalog.HasNode(SkillTreeNode.B3);

        Transform point = vfxPoint != null ? vfxPoint : playerController.transform;

        activeChargeVfx = VFXManager.Instance.PlayAttached(chargeVfxPrefab, point, Vector3.zero, Quaternion.Euler(vfxRotationOffset), chargeDuration);

        // 이동속도 일시 증가
        playerController.StartSpeedBoost(speedMultiplier, chargeDuration);

        // 충돌 데미지 일시 증가
        playerStats.SetTemporaryCollisionMultiplier(damageMultiplier);

        isCharging = true;
        if (invulnerable && playerHealth != null) playerHealth.IsInvulnerable = true;  //A 궁극: 폭주 기관차

        float timer = 0f;
        float nextShockwave = shockwaveInterval;
        while (timer < chargeDuration)
        {
            timer += Time.deltaTime;

            if (periodicShockwave && timer >= nextShockwave)  //B 궁극: 대지 진동
            {
                nextShockwave += shockwaveInterval;
                Shockwave(playerController.transform.position);
            }

            yield return null;
        }

        EndCharge();
    }

    private void EndCharge()
    {
        // 충돌 데미지 원상복구
        if (playerStats != null) playerStats.ResetTemporaryCollisionMultiplier();
        if (isCharging && playerHealth != null) playerHealth.IsInvulnerable = false;
        isCharging = false;

        activeChargeVfx = null;
        chargeCoroutine = null;
    }

    private void OnDisable()
    {
        PlayerBallController.OnPlayerHitEnemy -= HandleHitEnemy;

        // 스킬 도중 오브젝트가 꺼질 경우 버프/무적이 남지 않도록 초기화
        EndCharge();
    }
}