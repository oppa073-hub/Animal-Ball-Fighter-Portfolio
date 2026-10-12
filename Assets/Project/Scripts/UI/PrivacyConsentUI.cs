using UnityEngine;

public class PrivacyConsentUI : MonoBehaviour
{
    [SerializeField] private GameObject consentPanel;

    [Header("Privacy Policy")]
    [SerializeField] private string privacyPolicyUrl;

    private void Start()
    {
        if (consentPanel == null)
        {
            Debug.LogError("[Privacy] 개인정보 패널이 연결되지 않았습니다.");

            return;
        }

        if (AdManager.Instance == null)
        {
            Debug.LogError("[Privacy] AdManager가 없습니다.");

            consentPanel.SetActive(false);
            return;
        }

        consentPanel.SetActive(!AdManager.Instance.HasSavedPrivacyConsent);
    }

    public void AcceptPersonalizedAds()
    {
        ApplyConsent(true);
    }

    public void RejectPersonalizedAds()
    {
        ApplyConsent(false);
    }

    private void ApplyConsent(bool consent)
    {
        if (AdManager.Instance == null) return;

        AdManager.Instance.ApplyPrivacyConsent(consent);

        consentPanel.SetActive(false);

        Debug.Log($"[Privacy] 광고 개인정보 선택 완료: {consent}");
    }

    public void OpenPrivacySettings()
    {
        if (consentPanel == null) return;

        consentPanel.SetActive(true);
    }

    public void ClosePrivacySettings()
    {
        // 최초 선택 전에는 선택하지 않고 닫을 수 없음
        if (AdManager.Instance == null || !AdManager.Instance.HasSavedPrivacyConsent)
        {
            return;
        }

        consentPanel.SetActive(false);
    }

    public void OpenPrivacyPolicy()
    {
        if (string.IsNullOrWhiteSpace(privacyPolicyUrl))
        {
            Debug.LogWarning("[Privacy] 개인정보처리방침 URL이 입력되지 않았습니다.");

            return;
        }

        Application.OpenURL(privacyPolicyUrl);
    }
}