using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class AugmentPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private AugmentCardUI[] cards;

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float showDuration = 0.2f;

    public void Show(List<AugmentData> choices)
    {
        if (choices == null || choices.Count < 3)
        {
            Debug.LogError("[AugmentPanel] 증강 선택지가 3개 미만입니다.");
            return;
        }

        if (cards == null || cards.Length < 3)
        {
            Debug.LogError("[AugmentPanel] 카드가 3개 연결되지 않았습니다.");
            return;
        }

        root.SetActive(true);

        canvasGroup.DOKill();
        canvasGroup.alpha = 0f;

        root.transform.DOKill();
        root.transform.localScale = Vector3.one * 0.9f;

        root.SetActive(true);

        cards[0].Initialize(choices[0]);
        cards[1].Initialize(choices[1]);
        cards[2].Initialize(choices[2]);

        canvasGroup.DOFade(1f, showDuration);

        root.transform
            .DOScale(Vector3.one, showDuration)
            .SetEase(Ease.OutBack);
    }
    public void Hide(System.Action onComplete)
    {
        canvasGroup.DOKill();
        root.transform.DOKill();

        canvasGroup.DOFade(0f, 0.12f);

        root.transform
            .DOScale(Vector3.one * 0.95f, 0.12f)
            .OnComplete(() =>
            {
                root.SetActive(false);
                onComplete?.Invoke();
            });
    }
}

