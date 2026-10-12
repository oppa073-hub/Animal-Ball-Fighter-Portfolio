using UnityEngine;

public abstract class AugmentSkillRuntime : MonoBehaviour, ISkillUIData, ISkillDetailUIData
{
    // 스킬 버튼 부가 표시
    public float RemainCooldown => Mathf.Max(0f, cooldownTimer);
    public int SkillLevel => level;
    public bool IsAutoSkill => data != null && data.skillType == AugmentSkillType.Auto;

    protected AugmentSkillData data;

    protected int level = 1;
    protected float cooldownTimer;
    protected Sprite skillIcon;

    public Sprite SkillIcon => skillIcon;
    public int Level => level;
    public float Cooldown => data != null ? data.cooldown : 0f;
    public virtual float CurrentCooldown => Cooldown;
    public virtual bool IsBusy => false;  //스킬 동작이 이어지는 중 (그동안 대쉬 불가)
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
        if (TimeFreeze.IsActive) return;  //대쉬 조준 중에는 스킬 사용 불가 (산데비스탄 시간 감속과 겹치지 않게)

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