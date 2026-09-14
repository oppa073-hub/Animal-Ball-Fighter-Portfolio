using UnityEngine;

public class PlayerSkill : MonoBehaviour, ISkillUIData
{
    private PlayerStats playerStats;
    public Sprite SkillIcon { get; private set; }

    private KeyCode skillKey;  //원래 모바일이지만 임시테스트용
    private float skillCooldown;

    private ISkill currentSkill;

    [SerializeField] private float skillTimer;
    public bool IsSkillReady => skillTimer >= skillCooldown;

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
        SkillIcon = skillIcon;
    }
    public void TryUseSkill()
    {
        if (currentSkill == null) return;
        if (!IsSkillReady) return;
        if (GameManager.Instance.currentState != GameState.Playing) return;

        currentSkill.UseSkill();

        skillTimer = 0f;

        Debug.Log("스킬 사용");
    }

    public float CooldownRatio  //UI 용
    {
        get
        {
            if (skillCooldown <= 0f) return 1f;
            return Mathf.Clamp01(skillTimer / skillCooldown);
        }
    }
}
