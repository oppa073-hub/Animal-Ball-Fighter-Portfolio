using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GoldPurchasePopupUI : MonoBehaviour
{
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button purchaseButton;

    private void Awake()
    {
        popupRoot.SetActive(false);
    }

    public void Show()
    {
        popupRoot.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        popupRoot.SetActive(false);
    }

    private void Refresh()
    {
        bool isReady = IAPManager.Instance != null && IAPManager.Instance.IsGold5000Ready;

        purchaseButton.interactable = isReady;

        priceText.text = IAPManager.Instance != null
            ? IAPManager.Instance.Gold5000PriceText
            : "불러오는 중...";
    }

    public void Purchase()
    {
        if (IAPManager.Instance == null || !IAPManager.Instance.IsGold5000Ready)
        {
            ToastMessageManager.Instance?.Show("상품 준비 중");
            Refresh();
            return;
        }

        IAPManager.Instance.PurchaseGold5000();

        popupRoot.SetActive(false);
    }
}