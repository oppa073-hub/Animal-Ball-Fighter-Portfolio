using UnityEngine;
using System;
using UnityEngine.Localization;
public class LightningAugmentSkill : AugmentSkillRuntime
{
    [Header("Stat")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float attackRange = 10f;
    [SerializeField] private float baseDamage = 20f;
    [SerializeField] private float damageIncreaseAfterLevel5 = 10f;

    [Header("VFX")]
    [SerializeField] private GameObject lightningVfxPrefab;
    [SerializeField] private float vfxDuration = 1f;
    [SerializeField] private Vector3 vfxOffset;
    [SerializeField] private Vector3 vfxRotationOffset;

    [Header("Localization")]
    [SerializeField] private LocalizedString damageUpgradeText;
    [SerializeField] private LocalizedString targetUpgradeText;
    [SerializeField] private LocalizedString cooldownUpgradeText;
    [SerializeField] private LocalizedString damageTargetUpgradeText;

    private float currentDamage;
    private int targetCount = 1;
    private float cooldownMultiplier = 1f;
    public override float CurrentCooldown => Cooldown * cooldownMultiplier;
    public override void Initialize(AugmentSkillData skillData, Sprite icon)
    {
        base.Initialize(skillData, icon);

        currentDamage = baseDamage;
        targetCount = 1;
        cooldownMultiplier = 1f;
    }
    protected override void OnLevelUp()
    {
        switch (Level)
        {
            case 2:
                currentDamage = 30f;
                break;

            case 3:
                targetCount = 2;
                break;

            case 4:
                cooldownMultiplier = 0.8f;
                break;

            case 5:
                currentDamage = 50f;
                targetCount = 3;
                break;

            default:
                if (Level > data.specialUpgradeLevel)
                {
                    currentDamage += damageIncreaseAfterLevel5;
                }
                break;
        }
    }
    protected override bool ActivateSkill()
    {
        return TryAttack();
    }

    private bool TryAttack()
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, attackRange, enemyLayer);

        if (targets.Length == 0) return false;

        // 플레이어와 가까운 순서대로 정렬
        Array.Sort(targets, (a, b) =>
        {
            float distanceA = (a.transform.position - transform.position).sqrMagnitude;

            float distanceB = (b.transform.position - transform.position).sqrMagnitude;

            return distanceA.CompareTo(distanceB);
        });

        int attackCount = Mathf.Min(targetCount, targets.Length);

        for (int i = 0; i < attackCount; i++)
        {
            Transform target = targets[i].transform;

            Vector3 vfxPosition = target.position + vfxOffset;

            SoundManager.Instance?.PlaySFX(SoundId.LightningSkill);

            VFXManager.Instance.Play(lightningVfxPrefab, vfxPosition, Quaternion.Euler(vfxRotationOffset), vfxDuration);

            DamageManager.Instance.ApplyFixedDamage(targets[i].gameObject, currentDamage, DamageTextType.Normal);

            Debug.Log(
                $"[Lightning] {targets[i].name} 공격 / " +
                $"Damage:{currentDamage} / Lv.{Level}"
            );
        }

        return true;
    }
    public override string GetNextLevelDescription()
    {
        int nextLevel = Level + 1;

        switch (nextLevel)
        {
            case 2:
                return damageUpgradeText.GetLocalizedString();

            case 3:
                return targetUpgradeText.GetLocalizedString();

            case 4:
                return cooldownUpgradeText.GetLocalizedString();

            case 5:
                return damageTargetUpgradeText.GetLocalizedString();

            default:
                return damageUpgradeText.GetLocalizedString();
        }
    }
}