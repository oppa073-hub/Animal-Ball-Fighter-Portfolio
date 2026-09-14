using TMPro;
using UnityEngine;

public class NicknameSetupUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private GameObject nicknamePanel;
    [SerializeField] private NicknameUI nicknameUI;

    private void Start()
    {
        if (SaveManager.Instance == null) return;

        string savedNickname = SaveManager.Instance.GetNickname();

        // 이미 닉네임이 있으면 입력창 안 띄움
        if (!string.IsNullOrEmpty(savedNickname))
        {
            nicknamePanel.SetActive(false);
        }
    }

    public void ConfirmNickname()
    {
        string nickname = nicknameInput.text.Trim();

        if (string.IsNullOrEmpty(nickname)) return;

        SaveManager.Instance.SetNickname(nickname);

        nicknameUI.Refresh();

        nicknamePanel.SetActive(false);
    }
}