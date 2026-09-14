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

    public void Refresh()
    {
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