using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

// 사운드 설정 창의 진동 켜기/끄기 (HapticFeedback.Enabled)
public class VibrationSettingUI : MonoBehaviour
{
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private LocalizedString labelString;
    [SerializeField] private Button toggleButton;
    [SerializeField] private Image toggleImage;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private Color onColor = new Color(0.3f, 0.8f, 0.45f);
    [SerializeField] private Color offColor = new Color(0.55f, 0.55f, 0.55f);

    private void OnEnable()
    {
        if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
        LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;

        RefreshLabel();
        Refresh();
    }

    private void OnDisable()
    {
        if (toggleButton != null) toggleButton.onClick.RemoveListener(Toggle);
        LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
    }

    private void Toggle()
    {
        HapticFeedback.Enabled = !HapticFeedback.Enabled;
        if (HapticFeedback.Enabled) HapticFeedback.Medium();  //켰을 때 바로 느껴지도록

        Refresh();
    }

    private void Refresh()
    {
        bool isOn = HapticFeedback.Enabled;

        if (stateText != null) stateText.text = isOn ? "ON" : "OFF";
        if (toggleImage != null) toggleImage.color = isOn ? onColor : offColor;
    }

    private void HandleLocaleChanged(Locale locale)
    {
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (labelText == null || labelString == null || labelString.IsEmpty) return;

        labelText.text = labelString.GetLocalizedString();
    }
}
