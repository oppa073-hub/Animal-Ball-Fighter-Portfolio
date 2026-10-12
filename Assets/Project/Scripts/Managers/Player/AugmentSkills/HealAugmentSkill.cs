using UnityEngine;
using UnityEngine.Localization;
public class HealAugmentSkill : AugmentSkillRuntime
{
    [Header("VFX")]
    [SerializeField] private GameObject healVfxPrefab;
    [SerializeField] private float vfxDuration = 1.5f;
    [SerializeField] private Vector3 vfxOffset;

    [Header("Stat")]
    [SerializeField] private float baseHealPercent = 0.25f;
    [SerializeField] private float healIncreaseAfterLevel5 = 0.05f;

    [Header("Localization")]
    [SerializeField] private LocalizedString healUpgradeText;
    [SerializeField] private LocalizedString cooldownUpgradeText;
    [SerializeField] private LocalizedString largeHealUpgradeText;

    private Health playerHealth;

    private float currentHealPercent;
    private float cooldownMultiplier = 1f;

    public override float CurrentCooldown => Cooldown * cooldownMultiplier;

    public override void Initialize(AugmentSkillData skillData, Sprite icon)
    {
        base.Initialize(skillData, icon);

        playerHealth = GetComponentInParent<Health>();

        currentHealPercent = baseHealPercent;
        cooldownMultiplier = 1f;
    }
    protected override bool ActivateSkill()
    {
        if (playerHealth == null) return false;

        // 이미 풀피면 사용하지 않음
        if (playerHealth.CurrentHp >= playerHealth.MaxHp) return false;
        
        float healAmount = playerHealth.MaxHp * currentHealPercent;

        playerHealth.Heal(healAmount);

        SoundManager.Instance?.PlaySFX(SoundId.HealSkill);

        VFXManager.Instance.PlayAttached(healVfxPrefab, transform, vfxOffset, Quaternion.identity, vfxDuration);
        Debug.Log($"[HealSkill] {healAmount:0} 회복 / Lv.{Level}");

        return true;
    }
    protected override void OnLevelUp()
    {
        switch (Level)
        {
            case 2:
                currentHealPercent = 0.30f;
                break;

            case 3:
                cooldownMultiplier = 0.8f;
                break;

            case 4:
                currentHealPercent = 0.40f;
                break;

            case 5:
                currentHealPercent = 0.50f;
                break;

            default:
                if (Level > data.specialUpgradeLevel)
                {
                    currentHealPercent += healIncreaseAfterLevel5;
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
                return healUpgradeText.GetLocalizedString();

            case 3:
                return cooldownUpgradeText.GetLocalizedString();

            case 4:
                return healUpgradeText.GetLocalizedString();

            case 5:
                return largeHealUpgradeText.GetLocalizedString();

            default:
                return healUpgradeText.GetLocalizedString();
        }
    }
}