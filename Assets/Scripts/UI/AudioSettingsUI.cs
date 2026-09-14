using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsUI : MonoBehaviour
{
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    private void OnEnable()
    {
        if (SoundManager.Instance == null) return;

        bgmSlider.SetValueWithoutNotify(SoundManager.Instance.BGMVolume);

        sfxSlider.SetValueWithoutNotify(SoundManager.Instance.SFXVolume);

        bgmSlider.onValueChanged.AddListener(OnBGMChanged);
        sfxSlider.onValueChanged.AddListener(OnSFXChanged);
    }

    private void OnDisable()
    {
        bgmSlider.onValueChanged.RemoveListener(OnBGMChanged);
        sfxSlider.onValueChanged.RemoveListener(OnSFXChanged);
    }

    private void OnBGMChanged(float value)
    {
        SoundManager.Instance?.SetBGMVolume(value);
    }

    private void OnSFXChanged(float value)
    {
        SoundManager.Instance?.SetSFXVolume(value);
    }
}