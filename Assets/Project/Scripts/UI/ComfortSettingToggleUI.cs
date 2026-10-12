using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

// 사운드 설정 창의 편의 설정 켜기/끄기 (화면 흔들림, 섬광 줄이기). VibrationSettingUI와 같은 모양으로 쓴다.
public class ComfortSettingToggleUI : MonoBehaviour
{
    public enum Option
    {
        ScreenShake,
        ReduceFlash,
    }

    [SerializeField] private Option option;
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

    private bool IsOn
    {
        get => option == Option.ScreenShake ? ComfortSettings.ScreenShakeEnabled : ComfortSettings.ReduceFlash;
        set
        {
            if (option == Option.ScreenShake) ComfortSettings.ScreenShakeEnabled = value;
            else ComfortSettings.ReduceFlash = value;
        }
    }

    private void Toggle()
    {
        IsOn = !IsOn;
        if (option == Option.ScreenShake && IsOn) CameraShake.Instance?.Shake(0.15f, 0.1f);  //켰을 때 바로 느껴지도록 (로비에 카메라 흔들림이 있으면)

        Refresh();
    }

    private void Refresh()
    {
        bool isOn = IsOn;

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
