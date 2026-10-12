using UnityEngine;
using DG.Tweening;

public class LobbySideMenu : MonoBehaviour
{
    [SerializeField] private GameObject sideMenu;
    [SerializeField] private RectTransform sideMenuRect;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private GameObject menuBlocker;

    [SerializeField] private float duration = 0.18f;

    private bool isOpen; 
    private void OnDestroy()
    {
        sideMenuRect?.DOKill();
        canvasGroup?.DOKill();
    }

    public void ToggleMenu()
    {
        if (isOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }
    private void Open()
    {
        isOpen = true;

        sideMenu.SetActive(true);

        sideMenuRect.DOKill();
        canvasGroup.DOKill();

        sideMenuRect.localScale = new Vector3(1f, 0.8f, 1f);
        canvasGroup.alpha = 0f;

        sideMenuRect
            .DOScaleY(1f, duration)
            .SetEase(Ease.OutBack);

        canvasGroup.DOFade(1f, duration);

        menuBlocker.SetActive(true);
    }
    public void Close()
    {
        isOpen = false;

        sideMenuRect.DOKill();
        canvasGroup.DOKill();

        sideMenuRect
            .DOScaleY(0.8f, duration);

        canvasGroup
            .DOFade(0f, duration)
            .OnComplete(() =>
            {
                sideMenu.SetActive(false);
            });
        menuBlocker.SetActive(false);
    }
}