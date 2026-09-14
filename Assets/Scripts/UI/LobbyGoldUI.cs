using TMPro;
using UnityEngine;

public class LobbyGoldUI : MonoBehaviour
{
    [SerializeField] private TMP_Text goldText;

    private void Start()
    {
        Refresh(SaveManager.Instance.GetGold());

        SaveManager.Instance.OnGoldChanged += Refresh;
    }

    private void OnDestroy()
    {
        if (SaveManager.Instance != null) SaveManager.Instance.OnGoldChanged -= Refresh;
    }

    private void Refresh(int gold)
    {
        goldText.text = gold.ToString();
    }
}