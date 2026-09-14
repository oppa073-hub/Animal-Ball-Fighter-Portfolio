using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class LanguageSelectionUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject languagePanel;

    [Header("Language Buttons")]
    [SerializeField] private Button englishButton;
    [SerializeField] private Button koreanButton;

    [Header("Code Driven UI")]
    [SerializeField] private HomeCharacterInfoUI homeCharacterInfoUI;

    [SerializeField] private CharacterPreviewController characterPreviewController;

    [SerializeField] private CharacterLevelShopUI characterLevelShopUI;

    [SerializeField] private StageSelectPopupUI stageSelectPopupUI;

    private bool isChangingLanguage;

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
    }

    private IEnumerator Start()
    {
        yield return LocalizationSettings.InitializationOperation;

        RefreshButtonState(LocalizationSettings.SelectedLocale);
    }

    public void OpenLanguagePanel()
    {
        languagePanel.SetActive(true);

        RefreshButtonState(LocalizationSettings.SelectedLocale);
    }

    public void CloseLanguagePanel()
    {
        languagePanel.SetActive(false);
    }

    public void SelectEnglish()
    {
        ChangeLanguage("en");
    }

    public void SelectKorean()
    {
        ChangeLanguage("ko");
    }

    private void ChangeLanguage(string localeCode)
    {
        if (isChangingLanguage) return;

        StartCoroutine(ChangeLanguageRoutine(localeCode));
    }

    private IEnumerator ChangeLanguageRoutine(string localeCode)
    {
        isChangingLanguage = true;

        yield return LocalizationSettings.InitializationOperation;

        Locale locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);

        if (locale == null)
        {
            Debug.LogError(
                $"[Localization] Locale을 찾을 수 없습니다: " +
                $"{localeCode}"
            );

            isChangingLanguage = false;
            yield break;
        }

        LocalizationSettings.SelectedLocale = locale;

        RefreshCodeDrivenUI();
        RefreshButtonState(locale);

        isChangingLanguage = false;
    }

    private void HandleLocaleChanged(Locale locale)
    {
        RefreshButtonState(locale);
    }

    private void RefreshButtonState(Locale locale)
    {
        if (locale == null) return;

        string currentCode = locale.Identifier.Code;

        if (englishButton != null)
        {
            englishButton.interactable = currentCode != "en";
        }

        if (koreanButton != null)
        {
            koreanButton.interactable = currentCode != "ko";
        }
    }

    private void RefreshCodeDrivenUI()
    {
        homeCharacterInfoUI?.Refresh();

        characterPreviewController?.RefreshLocalizedUI();

        stageSelectPopupUI?.RefreshLocalizedUI();
    }
}