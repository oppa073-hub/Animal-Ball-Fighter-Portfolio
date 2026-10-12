using DG.Tweening;
using TMPro;
using UnityEngine;

public class ToastMessageManager : MonoBehaviour
{
    public static ToastMessageManager Instance { get; private set; }

    [SerializeField] private GameObject toastPanel;
    [SerializeField] private RectTransform toastRect;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text messageText;

    private Sequence sequence;
    private Vector2 originalPosition;

    private void Awake()
    {
        Instance = this;

        originalPosition = toastRect.anchoredPosition;

        toastPanel.SetActive(false);
    }

    public void Show(string message)
    {
        sequence?.Kill();

        toastPanel.SetActive(true);

        messageText.text = message;

        toastRect.anchoredPosition = originalPosition + Vector2.down * 30f;

        toastRect.localScale = Vector3.one * 0.9f;
        canvasGroup.alpha = 0f;

        sequence = DOTween.Sequence();

        sequence.Append(canvasGroup.DOFade(1f, 0.15f));

        sequence.Join(toastRect.DOAnchorPos(originalPosition, 0.25f).SetEase(Ease.OutBack));

        sequence.Join(toastRect.DOScale(1f, 0.25f).SetEase(Ease.OutBack));

        sequence.AppendInterval(1.2f);

        sequence.Append(canvasGroup.DOFade(0f, 0.25f));

        sequence.OnComplete(() =>
        {
            toastPanel.SetActive(false);
        });
    }
}