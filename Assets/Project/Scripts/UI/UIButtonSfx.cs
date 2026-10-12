using UnityEngine;
using UnityEngine.UI;

public class UIButtonSfx : MonoBehaviour
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (button != null) button.onClick.AddListener(PlayClickSound);
    }

    private void PlayClickSound()
    {
        SoundManager.Instance?.PlaySFX(SoundId.UI_Click);
    }
}