using System.Collections;
using UnityEngine;

// 토끼 고유 스킬: 높이 점프(수평 이동 유지) 후 착지하면 잠깐 공격력/이동속도 버프.
// 사용 횟수 2회, 횟수마다 쿨타임이 따로 돈다. 공중에서는 다시 쓸 수 없다.
public class RabbitJumpSkill : MonoBehaviour, IChargeSkill
{
    [Header("Jump")]
    [SerializeField] private float jumpHeight = 3f;  //보스 콜라이더(높이 약 2) 위로 넘어가는 높이
    [SerializeField] private float jumpDuration = 0.9f;

    [Header("Charges")]
    [SerializeField] private int maxCharges = 2;  //쿨타임은 CharacterData.skillCooldown (횟수마다 따로)

    [Header("Landing Buff")]
    [SerializeField] private float buffDuration = 2.5f;
    [SerializeField] private float attackMultiplier = 1.3f;  //충돌 피해 배율
    [SerializeField] private float speedMultiplier = 1.4f;

    [Header("Feedback")]
    [SerializeField] private GameObject landingVfxPrefab;  //선택 (비어 있으면 생략)
    [SerializeField] private float landingVfxDuration = 1f;
    [SerializeField] private float landingShakeDuration = 0.12f;
    [SerializeField] private float landingShakeStrength = 0.15f;

    [Header("Skill Tree")]
    [SerializeField] private int baseExtraCharges = 1;            //기본 강화: 점프 +1
    [SerializeField] private LayerMask enemyLayer;                //A: 착지 충격파
    [SerializeField] private GameObject shockwaveVfxPrefab;
    [SerializeField] private float shockwaveRadius = 2.5f;
    [SerializeField] private float ultimateShockwaveMultiplier = 2f;  //A 궁극: 범위/피해 2배 + 버프 시간 2배
    [SerializeField] private float ultimateKnockback = 12f;
    [SerializeField] private float boostedAttackMultiplier = 1.6f;    //B 2단계
    [SerializeField] private float boostedSpeedMultiplier = 1.6f;
    [SerializeField] private float boostedExtraDuration = 1f;
    [SerializeField] private int ultimateCharges = 5;                 //B 궁극
    [SerializeField] private float ultimateCooldownMultiplier = 0.6f;

    private PlayerStats playerStats;
    private Health playerHealth;
    private PlayerBallController playerController;
    private float cooldownMultiplier = 1f;

    private float[] chargeCooldowns;
    private Coroutine buffCoroutine;

    public int MaxCharges => maxCharges;
    public bool CanUseSkill => CurrentCharges > 0 && playerController != null && !playerController.IsJumping;

    public int CurrentCharges
    {
        get
        {
            int count = 0;
            for (int i = 0; i < chargeCooldowns.Length; i++)
            {
                if (chargeCooldowns[i] <= 0f) count++;
            }
            return count;
        }
    }

    public float NextChargeRatio
    {
        get
        {
            float cooldown = playerStats != null ? playerStats.SkillCooldown : 0f;
            if (cooldown <= 0f) return 1f;

            float shortestRemain = float.MaxValue;
            for (int i = 0; i < chargeCooldowns.Length; i++)
            {
                if (chargeCooldowns[i] <= 0f) return 1f;
                shortestRemain = Mathf.Min(shortestRemain, chargeCooldowns[i]);
            }

            return Mathf.Clamp01(1f - shortestRemain / cooldown);
        }
    }

    private void Awake()
    {
        playerStats = GetComponentInParent<PlayerStats>();
        playerHealth = GetComponentInParent<Health>();
        playerController = GetComponentInParent<PlayerBallController>();

        // 스킬 트리: 점프 횟수/쿨타임 (스킬은 스테이지 시작 때 한 번 생성되므로 여기서 확정)
        if (SkillTreeCatalog.HasNode(SkillTreeNode.B3)) maxCharges = ultimateCharges;
        else if (SkillTreeCatalog.HasNode(SkillTreeNode.Base)) maxCharges += baseExtraCharges;
        cooldownMultiplier = SkillTreeCatalog.HasNode(SkillTreeNode.B3) ? ultimateCooldownMultiplier : 1f;

        chargeCooldowns = new float[Mathf.Max(1, maxCharges)];
    }

    private void Update()
    {
        for (int i = 0; i < chargeCooldowns.Length; i++)
        {
            if (chargeCooldowns[i] > 0f) chargeCooldowns[i] -= Time.deltaTime;
        }
    }

    public void UseSkill()
    {
        if (!CanUseSkill) return;
        if (!playerController.StartJump(jumpHeight, jumpDuration, HandleLanded)) return;

        for (int i = 0; i < chargeCooldowns.Length; i++)
        {
            if (chargeCooldowns[i] > 0f) continue;

            chargeCooldowns[i] = playerStats.SkillCooldown * cooldownMultiplier;
            break;
        }

        SoundManager.Instance?.PlaySFX(SoundId.RabbitSkill);
    }

    private void HandleLanded()
    {
        if (landingVfxPrefab != null) VFXManager.Instance?.Play(landingVfxPrefab, playerController.transform.position, Quaternion.identity, landingVfxDuration);

        if (CombatFeedbackManager.Instance != null)
        {
            CombatFeedbackManager.Instance.RequestImpact(SoundId.EnemyHit, landingShakeDuration, landingShakeStrength, priority: 3);
        }
        else
        {
            SoundManager.Instance?.PlaySFX(SoundId.EnemyHit);
        }

        if (SkillTreeCatalog.HasNode(SkillTreeNode.A2))  //A: 착지 충격파
        {
            bool ultimate = SkillTreeCatalog.HasNode(SkillTreeNode.A3);
            float multiplier = ultimate ? ultimateShockwaveMultiplier : 1f;
            SkillAreaDamage.Explode(playerController.transform.position, shockwaveRadius * multiplier, playerStats.SkillDamage * multiplier,
                enemyLayer, shockwaveVfxPrefab, playerStats, playerHealth, ultimate ? ultimateKnockback : 0f);
        }

        if (buffCoroutine != null) StopCoroutine(buffCoroutine);
        buffCoroutine = StartCoroutine(LandingBuff());
    }

    private IEnumerator LandingBuff()
    {
        bool boosted = SkillTreeCatalog.HasNode(SkillTreeNode.B2);
        float duration = buffDuration + (boosted ? boostedExtraDuration : 0f);
        if (SkillTreeCatalog.HasNode(SkillTreeNode.A3)) duration *= 2f;

        playerController.StartSpeedBoost(boosted ? boostedSpeedMultiplier : speedMultiplier, duration);
        playerStats.SetTemporaryCollisionMultiplier(boosted ? boostedAttackMultiplier : attackMultiplier);

        yield return new WaitForSeconds(duration);

        playerStats.ResetTemporaryCollisionMultiplier();
        buffCoroutine = null;
    }

    private void OnDisable()
    {
        // 버프 도중 오브젝트가 꺼질 경우 배율이 남지 않도록 초기화
        if (playerStats != null) playerStats.ResetTemporaryCollisionMultiplier();
        buffCoroutine = null;
    }
}
