using UnityEngine;

public class SkillUIContainer : MonoBehaviour
{
    [SerializeField] SkillSlotUI skillSlotUI1;
    [SerializeField] SkillSlotUI skillSlotUI2;
    public void RegisterSkill(ISkillUIData skill)
    {
        if (skillSlotUI1.IsEmpty)
        {
            skillSlotUI1.Initialize(skill);
        }
        else if (skillSlotUI2.IsEmpty)
        {
            skillSlotUI2.gameObject.SetActive(true);
            skillSlotUI2.Initialize(skill);
        }
        else
        {
            Debug.Log("스킬 UI 다사용중");
        }
    }
}
