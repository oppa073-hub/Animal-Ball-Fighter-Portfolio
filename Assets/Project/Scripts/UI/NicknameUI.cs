using TMPro;
using UnityEngine;

public class NicknameUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nicknameText;

    private void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (SaveManager.Instance == null) return;

        string nickname = SaveManager.Instance.GetNickname();

        nicknameText.text = string.IsNullOrEmpty(nickname)
            ? "PLAYER"
            : nickname;
    }
}