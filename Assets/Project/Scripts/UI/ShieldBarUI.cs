using UnityEngine;
using UnityEngine.UI;

public class ShieldBarUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Image fillImage;

    private PlayerShield playerShield;

    public void Initialize(PlayerShield shield)
    {
        if (playerShield != null) playerShield.OnShieldChanged -= Refresh;
        playerShield = shield;
        playerShield.OnShieldChanged += Refresh;

        // 실드는 이제 PlayerHealthUI의 가로 체력바 위에 표시하므로 예전 원형 표시는 화면에서만 숨김
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        Refresh(playerShield.CurrentShield, playerShield.MaxShield);
    }

    private void OnDisable()
    {
        if (playerShield != null) playerShield.OnShieldChanged -= Refresh;
    }
    private void OnEnable()
    {
        if (playerShield == null) return;

        playerShield.OnShieldChanged -= Refresh;
        playerShield.OnShieldChanged += Refresh;

        Refresh(playerShield.CurrentShield, playerShield.MaxShield);
    }

    private void Refresh(float current, float max)
    {
        root.SetActive(current > 0f);
        fillImage.fillAmount = max > 0f ? current / max : 0f;
    }
}
