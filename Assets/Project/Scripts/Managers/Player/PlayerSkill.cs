using UnityEngine;

public class PlayerSkill : MonoBehaviour, ISkillUIData, ISkillChargeUIData, ISkillDetailUIData
{
    // 스킬 버튼 부가 표시 (충전형은 횟수 숫자로 보여 주므로 남은 초는 표시하지 않음)
    float ISkillDetailUIData.RemainCooldown => chargeSkill != null ? 0f : RemainCooldown;
    int ISkillDetailUIData.SkillLevel => 0;
    bool ISkillDetailUIData.IsAutoSkill => false;

    private PlayerStats playerStats;
    public Sprite SkillIcon { get; private set; }

    private KeyCode skillKey;  //원래 모바일이지만 임시테스트용
    private float skillCooldown;

    private ISkill currentSkill;
    private IChargeSkill chargeSkill;  //토끼 점프처럼 사용 횟수가 여러 개인 스킬 (없으면 기존 단일 쿨타임)

    [SerializeField] private float skillTimer;
    public bool IsSkillReady => chargeSkill != null ? chargeSkill.CanUseSkill : skillTimer >= skillCooldown;

    public int MaxCharges => chargeSkill != null ? chargeSkill.MaxCharges : 1;
    public int CurrentCharges => chargeSkill != null ? chargeSkill.CurrentCharges : (IsSkillReady ? 1 : 0);

    public float RemainCooldown => Mathf.Max(0f, skillCooldown - skillTimer);


    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
    }

    private void Start()
    {
        skillKey = playerStats.SkillKey;
        skillCooldown = playerStats.SkillCooldown;
        UIManager.Instance.ShowPlayerSkillUI(this);
    }

    private void Update()
    {
        skillTimer += Time.deltaTime;

        if (currentSkill == null) return;

        if (Input.GetKeyDown(skillKey))
        {
            TryUseSkill();
        }
    }
    public void SetSkill(ISkill skill, Sprite skillIcon)
    {
        currentSkill = skill;
        chargeSkill = skill as IChargeSkill;
        SkillIcon = skillIcon;
    }
    public void TryUseSkill()
    {
        if (currentSkill == null) return;
        if (!IsSkillReady) return;
        if (GameManager.Instance.currentState != GameState.Playing) return;
        if (TimeFreeze.IsActive) return;  //대쉬 조준 중에는 스킬 사용 불가

        currentSkill.UseSkill();
        GameEvents.RaiseSkillUsed();

        if (chargeSkill == null) skillTimer = 0f;  //충전형은 스킬이 직접 횟수/쿨타임을 관리

        Debug.Log("스킬 사용");
    }

    public void ReduceCooldown(float seconds)  //남은 쿨타임 감소 (고양이 전용 증강 등). 충전형 스킬은 스킬이 직접 관리하므로 제외
    {
        if (chargeSkill != null || seconds <= 0f) return;
        skillTimer = Mathf.Min(skillTimer + seconds, skillCooldown);
    }

    public float CooldownRatio  //UI 용
    {
        get
        {
            if (chargeSkill != null) return chargeSkill.CurrentCharges > 0 ? 1f : chargeSkill.NextChargeRatio;
            if (skillCooldown <= 0f) return 1f;
            return Mathf.Clamp01(skillTimer / skillCooldown);
        }
    }
}
