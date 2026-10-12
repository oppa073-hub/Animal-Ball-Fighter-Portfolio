using TMPro;
using UnityEngine;

public class HomeCharacterInfoUI : MonoBehaviour
{
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text characterLevelText;

    private void Start()
    {
        Refresh();
    }

    private void OnEnable()
    {
        if (SaveManager.Instance == null) return;

        SaveManager.Instance.OnCharacterLevelChanged += HandleCharacterChanged;
        SaveManager.Instance.OnSelectedCharacterChanged += HandleCharacterChanged;

        Refresh();  //꺼져 있던 동안 바뀐 레벨/캐릭터도 반영
    }

    private void OnDisable()
    {
        if (SaveManager.Instance == null) return;

        SaveManager.Instance.OnCharacterLevelChanged -= HandleCharacterChanged;
        SaveManager.Instance.OnSelectedCharacterChanged -= HandleCharacterChanged;
    }

    private void HandleCharacterChanged(CharacterId characterId)  //상점 레벨업, 캐릭터 변경 시 바로 갱신
    {
        Refresh();
    }

    public void Refresh()
    {
        if (SaveManager.Instance == null || CharacterDatabase.Instance == null) return;

        CharacterId selectedId = SaveManager.Instance.Data.selectedCharacterId;
        CharacterData data = CharacterDatabase.Instance.GetCharacter(selectedId);

        if (data == null)
        {
            Debug.LogError($"[HomeCharacterInfoUI] {selectedId} 캐릭터 데이터를 찾을 수 없습니다.");
            return;
        }

        CharacterProgressData progress = SaveManager.Instance.GetCharacterProgress(selectedId);

        characterNameText.text = data.localizedCharacterName.GetLocalizedString();
        characterLevelText.text = $"Lv. {progress.level}";
    }
}