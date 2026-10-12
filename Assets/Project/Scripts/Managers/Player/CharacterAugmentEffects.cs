using System.Collections.Generic;
using UnityEngine;

// 캐릭터 전용 증강 효과. AugmentManager가 플레이어에 붙이고 Enable로 켠다.
// - 고양이 반사신경: 퍼펙트 반사 때 스킬 쿨타임 감소
// - 돼지 충격파: 스킬 돌진 중 벽에 부딪히면 충격파
// - 병아리 깃털 폭풍: 콤보 10마다 깃털탄 8방향
// - 토끼 행운의 발: 치명타 확률 증가 + 퍼펙트 반사 다음 타격은 확정 치명타
public class CharacterAugmentEffects : MonoBehaviour
{
    [Header("Cat")]
    [SerializeField] private float catCooldownReduction = 1.5f;

    [Header("Pig")]
    [SerializeField] private float pigShockwaveRadius = 2.2f;
    [SerializeField] private float pigShockwaveDamageRatio = 1f;  //공격력 대비
    [SerializeField] private float pigShockwaveCooldown = 0.2f;

    [Header("Chick")]
    [SerializeField] private int chickComboStep = 10;
    [SerializeField] private int chickFeatherCount = 8;
    [SerializeField] private float chickDamageRatio = 0.6f;  //스킬 피해 대비

    [Header("Rabbit")]
    [SerializeField] private float rabbitCriticalChance = 0.1f;

    private static bool forceNextCritical;

    private readonly HashSet<AugmentType> active = new HashSet<AugmentType>();

    private PlayerBallController controller;
    private PlayerStats stats;
    private PlayerSkill skill;
    private Health health;
    private float nextShockwaveTime;
    private int lastFeatherCombo;

    public static bool IsCharacterAugment(AugmentType type)
    {
        switch (type)
        {
            case AugmentType.CatReflex:
            case AugmentType.PigShockwave:
            case AugmentType.ChickFeathers:
            case AugmentType.RabbitLuck:
                return true;
            default:
                return false;
        }
    }

    public static bool ConsumeForcedCritical()  //PlayerBallController가 적 충돌 때 확인 (토끼)
    {
        bool value = forceNextCritical;
        forceNextCritical = false;
        return value;
    }

    private void Awake()
    {
        controller = GetComponent<PlayerBallController>();
        stats = GetComponent<PlayerStats>();
        skill = GetComponent<PlayerSkill>();
        health = GetComponent<Health>();
        forceNextCritical = false;
    }

    private void OnEnable()
    {
        PlayerPerfectBounce.OnPerfect += HandlePerfect;
        PlayerBallController.OnPlayerHitWall += HandleWallHit;
        GameEvents.ComboChanged += HandleComboChanged;
    }

    private void OnDisable()
    {
        PlayerPerfectBounce.OnPerfect -= HandlePerfect;
        PlayerBallController.OnPlayerHitWall -= HandleWallHit;
        GameEvents.ComboChanged -= HandleComboChanged;
    }

    public void Enable(AugmentType type)
    {
        if (!IsCharacterAugment(type) || !active.Add(type)) return;

        if (type == AugmentType.RabbitLuck) stats?.AddCriticalChance(rabbitCriticalChance);
    }

    private void HandlePerfect(Vector3 position, int chain)
    {
        if (active.Contains(AugmentType.CatReflex)) skill?.ReduceCooldown(catCooldownReduction);
        if (active.Contains(AugmentType.RabbitLuck)) forceNextCritical = true;
    }

    private void HandleWallHit()
    {
        if (!active.Contains(AugmentType.PigShockwave)) return;
        if (controller == null || !controller.IsInSkillMotion || Time.time < nextShockwaveTime) return;

        nextShockwaveTime = Time.time + pigShockwaveCooldown;
        float attack = stats != null ? stats.Attack : 10f;
        AreaDamage.Explode(transform.position, pigShockwaveRadius, attack * pigShockwaveDamageRatio, new Color(1f, 0.55f, 0.75f));
    }

    private void HandleComboChanged(int combo)
    {
        if (combo == 0) lastFeatherCombo = 0;
        if (!active.Contains(AugmentType.ChickFeathers) || combo < chickComboStep) return;

        int step = combo / chickComboStep * chickComboStep;
        if (step <= lastFeatherCombo) return;
        lastFeatherCombo = step;

        CombatUiStyle style = CombatUiStyle.Get();
        Material glow = style != null ? style.trailMaterial : null;
        if (glow == null || stats == null) return;

        int enemyMask = LayerMask.GetMask("Enemy");
        float damage = stats.SkillDamage * chickDamageRatio;

        for (int i = 0; i < chickFeatherCount; i++)
        {
            Vector3 direction = Quaternion.Euler(0f, 360f / chickFeatherCount * i, 0f) * Vector3.forward;
            SkillProjectile.Spawn(transform.position + Vector3.up * 0.4f, direction, 15f, damage, 0.9f, 2,
                enemyMask, glow, glow, new Color(1f, 0.9f, 0.35f), 0.55f, stats, health);
        }

        SoundManager.Instance?.PlaySFX(SoundId.ChickSkill);
    }
}
