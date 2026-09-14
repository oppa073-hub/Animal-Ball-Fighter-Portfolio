using UnityEngine;

public abstract class AugmentSkillRuntime : MonoBehaviour, ISkillUIData
{
    protected AugmentSkillData data;

    protected int level = 1;
    protected float cooldownTimer;
    protected Sprite skillIcon;

    public Sprite SkillIcon => skillIcon;
    public int Level => level;
    public float Cooldown => data != null ? data.cooldown : 0f;
    public virtual float CurrentCooldown => Cooldown;
    public virtual float CooldownRatio
    {
        get
        {
            if (CurrentCooldown <= 0f) return 1f;

            return 1f -
                Mathf.Clamp01(cooldownTimer / CurrentCooldown);
        }
    }
    protected virtual void Update()
    {
        if (data == null || !CanProgressSkill()) return;

        cooldownTimer = Mathf.Max(0f, cooldownTimer - Time.deltaTime);

        if (data.skillType == AugmentSkillType.Auto &&cooldownTimer <= 0f && ActivateSkill())
        {
            cooldownTimer = CurrentCooldown;
        }
    }

    public virtual void Initialize(AugmentSkillData skillData, Sprite icon)
    {
        data = skillData;
        skillIcon = icon;

        level = 1;
        cooldownTimer = 0f;
    }

    public virtual string GetNextLevelDescription()
    {
        if (data == null) return "";

        return data.localizedUpgradeDescription.GetLocalizedString();
    }

    public virtual void LevelUp()
    {
        level++;

        OnLevelUp();

        Debug.Log($"[AugmentSkill] {data.skillName} Lv.{level}");
    }

    protected virtual void OnLevelUp()
    {
    }
    public virtual void TryUseSkill()
    {
        if (data == null) return;

        // 수동 스킬만 버튼 사용 가능
        if (data.skillType != AugmentSkillType.Active) return;

        if (!CanProgressSkill()) return;

        if (cooldownTimer > 0f) return;

        if (ActivateSkill())
        {
            cooldownTimer = CurrentCooldown;
        }
    }

    protected abstract bool ActivateSkill();

    protected bool CanProgressSkill()
    {
        if (data == null) return false;
        if (GameManager.Instance == null) return false;
        if (RoomManager.Instance == null) return false;

        return GameManager.Instance.currentState == GameState.Playing && RoomManager.Instance.HasActiveEnemies;
    }
}