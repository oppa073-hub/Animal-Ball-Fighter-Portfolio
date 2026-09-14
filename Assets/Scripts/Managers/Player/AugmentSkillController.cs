using System.Collections.Generic;
using UnityEngine;

public class AugmentSkillController : MonoBehaviour
{
    private AugmentSkillData currentSkillData;
    private AugmentSkillRuntime currentSkill;

    public bool HasSkill => currentSkill != null;
    public AugmentSkillData CurrentSkillData => currentSkillData;
    public int CurrentSkillLevel => currentSkill != null ? currentSkill.Level : 0;

    public void AcquireSkill(AugmentData augment)
    {
        if (augment == null || augment.augmentSkill == null) return;

        AugmentSkillData data = augment.augmentSkill;

        // 이미 증강 스킬을 가지고 있음
        if (currentSkill != null)
        {
            // 같은 스킬이면 레벨업
            if (currentSkillData == data)
            {
                currentSkill.LevelUp();
            }
            else
            {
                Debug.LogWarning($"[AugmentSkill] 이미 {currentSkillData.skillName}을 보유 중입니다.");
            }

            return;
        }

        // 최초 스킬 획득
        GameObject skillObject = Instantiate(data.skillPrefab, transform);

        AugmentSkillRuntime runtime = skillObject.GetComponent<AugmentSkillRuntime>();

        if (runtime == null)
        {
            Debug.LogError($"[AugmentSkill] {data.skillName} 프리팹에 AugmentSkillRuntime이 없습니다.");

            Destroy(skillObject);
            return;
        }

        runtime.Initialize(data, augment.icon);

        currentSkillData = data;
        currentSkill = runtime;

        UIManager.Instance.RegisterAugmentSkill(runtime);

        Debug.Log($"[AugmentSkill] {data.skillName} 획득");
    }
    public string GetNextLevelDescription()
    {
        if (currentSkill == null) return "";

        return currentSkill.GetNextLevelDescription();
    }
}