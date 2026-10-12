using System.Collections;
using UnityEngine;

// 로비 기타 창(MiscSettingsMenu)의 '개인정보 설정' 버튼이 부른다. MiscSettingsMenu가 이 컴포넌트를 붙여 쓴다.
// 누르면 처음 실행 때 나오는 동의 창(PrivacyConsentUI.OpenPrivacySettings)을 다시 열어 동의/거부를 바꿀 수 있다.
// 동의 저장은 기존 PrivacyConsentUI → AdManager 흐름 그대로이고, 여기서는 창만 연다.
// 선택이 바뀌면 광고 SDK에는 다음 실행부터 완전히 적용될 수 있어 재시작 안내 토스트를 띄운다.
public class PrivacySettingsButton : MonoBehaviour
{
    private const string LobbySceneName = "MainLobby";

    public void OpenConsent()
    {
        PrivacyConsentUI consentUI = FindAnyObjectByType<PrivacyConsentUI>(FindObjectsInactive.Include);
        if (consentUI == null)
        {
            Debug.LogWarning("[PrivacySettingsButton] PrivacyConsentUI를 찾지 못했습니다.");
            return;
        }

        bool hadChoice = AdManager.Instance != null && AdManager.Instance.HasSavedPrivacyConsent;
        bool before = hadChoice && AdManager.Instance.SavedPrivacyConsent;

        consentUI.OpenPrivacySettings();
        StartCoroutine(WatchConsentChange(hadChoice, before));
    }

    // 동의 창이 닫히면 선택이 바뀌었는지 보고 안내
    private IEnumerator WatchConsentChange(bool hadChoice, bool before)
    {
        GameObject panel = FindRect("PrivacyConsentPanel")?.gameObject;
        if (panel == null) yield break;

        yield return null;
        while (panel != null && panel.activeInHierarchy) yield return null;

        if (AdManager.Instance == null || !AdManager.Instance.HasSavedPrivacyConsent) yield break;
        if (hadChoice && AdManager.Instance.SavedPrivacyConsent == before) yield break;

        ToastMessageManager.Instance?.Show(LobbyUiKit.Loc("settings_privacy_changed", "설정을 저장했어요. 광고 설정은 앱을 다시 시작하면 완전히 적용돼요."));
    }

    private static RectTransform FindRect(string objectName)
    {
        foreach (RectTransform rect in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rect.name == objectName && rect.gameObject.scene.name == LobbySceneName) return rect;
        }

        return null;
    }
}
