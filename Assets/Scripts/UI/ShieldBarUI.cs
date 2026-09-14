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
