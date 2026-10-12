using UnityEngine;
using UnityEngine.Localization;

public class FireBurstAugmentSkill : AugmentSkillRuntime
{
    [SerializeField] private LayerMask enemyLayer;

    [Header("Stat")]
    [SerializeField] private float baseDamage = 30f;
    [SerializeField] private float baseRange = 3f;
    [SerializeField] private float damageIncreaseAfterLevel5 = 12f;

    [Header("VFX")]
    [SerializeField] private GameObject explosionVfxPrefab;
    [SerializeField] private float vfxDuration = 1f;
    [SerializeField] private Vector3 vfxOffset;

    [Header("Localization")]
    [SerializeField] private LocalizedString damageUpgradeText;
    [SerializeField] private LocalizedString rangeUpgradeText;
    [SerializeField] private LocalizedString cooldownUpgradeText;
    [SerializeField] private LocalizedString damageRangeUpgradeText;

    private float currentDamage;
    private float currentRange;
    private float cooldownMultiplier = 1f;

    public override float CurrentCooldown => Cooldown * cooldownMultiplier;

    public override void Initialize(AugmentSkillData skillData, Sprite icon)
    {
        base.Initialize(skillData, icon);

        currentDamage = baseDamage;
        currentRange = baseRange;
        cooldownMultiplier = 1f;
    }

    protected override bool ActivateSkill()
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, currentRange, enemyLayer);

        SoundManager.Instance?.PlaySFX(SoundId.FireBurstSkill);

        VFXManager.Instance.Play(explosionVfxPrefab, transform.position + vfxOffset, Quaternion.identity, vfxDuration);

        foreach (Collider target in targets)
        {
            DamageManager.Instance.ApplyFixedDamage(target.gameObject, currentDamage, DamageTextType.Normal);
        }

        return true;
    }

    protected override void OnLevelUp()
    {
        switch (Level)
        {
            case 2:
                currentDamage = 45f;
                break;

            case 3:
                currentRange = 4f;
                break;

            case 4:
                cooldownMultiplier = 0.8f;
                break;

            case 5:
                currentDamage = 70f;
                currentRange = 5f;
                break;

            default:
                if (Level > data.specialUpgradeLevel)
                {
                    currentDamage += damageIncreaseAfterLevel5;
                }
                break;
        }
    }

    public override string GetNextLevelDescription()
    {
        int nextLevel = Level + 1;

        switch (nextLevel)
        {
            case 2:
                return damageUpgradeText.GetLocalizedString();

            case 3:
                return rangeUpgradeText.GetLocalizedString();

            case 4:
                return cooldownUpgradeText.GetLocalizedString();

            case 5:
                return damageRangeUpgradeText.GetLocalizedString();

            default:
                return damageUpgradeText.GetLocalizedString();
        }
    }
}
