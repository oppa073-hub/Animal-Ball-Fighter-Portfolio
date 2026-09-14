using UnityEngine;

public class PlayerCharacterSetup : MonoBehaviour
{
    [SerializeField] private Transform modelRoot;

    private GameObject currentModel;
    private GameObject currentSkillObject;

    private void Awake()
    {
        SetupSelectedCharacter();
    }

    private void SetupSelectedCharacter()
    {
        CharacterId selectedId = SaveManager.Instance.Data.selectedCharacterId;

        CharacterData data = CharacterDatabase.Instance.GetCharacter(selectedId);

        if (data == null)
        {
            Debug.LogError($"[PlayerCharacterSetup] {selectedId} 데이터를 찾을 수 없습니다.");
            return;
        }

        GetComponent<PlayerStats>().Initialize(data);

        SetupModel(data);
        SetupSkill(data);


    }

    private void SetupModel(CharacterData data)
    {
        currentModel = Instantiate(
            data.characterPrefab,
            modelRoot
        );

        currentModel.transform.localPosition = Vector3.zero;
        currentModel.transform.localRotation = Quaternion.identity;
    }

    private void SetupSkill(CharacterData data)
    {
        if (data.skillPrefab == null) return;

        currentSkillObject = Instantiate(
            data.skillPrefab,
            transform
        );

        ISkill skill = currentSkillObject.GetComponent<ISkill>();

        if (skill == null)
        {
            Debug.LogError("[Player] SkillPrefab에 ISkill이 없습니다.");
            return;
        }

        GetComponent<PlayerSkill>().SetSkill(skill, data.skillIcon);
    }
}