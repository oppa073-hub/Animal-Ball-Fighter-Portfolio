using UnityEngine;
using UnityEngine.UI;

public class SkillSlotUI : MonoBehaviour
{
    private ISkillUIData skill;
    [SerializeField] private Image fillImg;
    [SerializeField] private Image backImg;

    public bool IsEmpty => skill == null;

    public void Initialize(ISkillUIData skill)
    {
        this.skill = skill;

        fillImg.sprite = skill.SkillIcon;
        backImg.sprite = skill.SkillIcon;

        fillImg.fillAmount = 1f;
    }

    private void Update()
    {
        if (skill == null) return;

        fillImg.fillAmount = skill.CooldownRatio;
    }

    public void Clear()
    {
        skill = null;
    }
    public void OnSkillButtonClicked()
    {
        if (skill == null) return;

        skill.TryUseSkill();
    }
}
