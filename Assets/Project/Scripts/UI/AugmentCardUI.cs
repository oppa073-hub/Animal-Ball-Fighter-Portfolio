using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.EventSystems;
using UnityEngine.Localization;

public class AugmentCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image gradeImage;
    [SerializeField] private TMP_Text gradeText;
    [SerializeField] private Button button;

    [SerializeField] private Color commonColor;
    [SerializeField] private Color rareColor;
    [SerializeField] private Color epicColor;
    [SerializeField] private Color legendaryColor;

    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float tweenDuration = 0.15f;

    [Header("Grade Localization")]
    [SerializeField] private LocalizedString commonGradeText;
    [SerializeField] private LocalizedString rareGradeText;
    [SerializeField] private LocalizedString epicGradeText;
    [SerializeField] private LocalizedString legendaryGradeText;
    private AugmentData currentData;

    public void Initialize(AugmentData data)
    {
        button.interactable = true;
        transform.DOKill();
        transform.localScale = Vector3.one;

        currentData = data;
        icon.sprite = data.icon;
        nameText.text = AugmentManager.Instance.GetAugmentDisplayName(data);
        descriptionText.text = AugmentManager.Instance.GetAugmentDisplayDescription(data);
        switch (data.grade)
        {
            case AugmentGrade.Common:
                gradeImage.color = commonColor;
                gradeText.text = commonGradeText.GetLocalizedString();
                break;

            case AugmentGrade.Rare:
                gradeImage.color = rareColor;
                gradeText.text = rareGradeText.GetLocalizedString();
                break;

            case AugmentGrade.Epic:
                gradeImage.color = epicColor;
                gradeText.text = epicGradeText.GetLocalizedString();
                break;

            case AugmentGrade.Legendary:
                gradeImage.color = legendaryColor;
                gradeText.text = legendaryGradeText.GetLocalizedString();
                break;
        }

        gradeText.color = gradeImage.color;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            button.interactable = false;
            AugmentManager.Instance.SelectAugment(currentData);
        });
    }
    public bool SuppressHover { get; set; }  //버리기 뒤집기 연출 중에는 호버 확대가 연출을 끊지 않도록

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (SuppressHover) return;
        transform.DOKill();
        transform.DOScale(hoverScale, tweenDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (SuppressHover) return;
        transform.DOKill();
        transform.DOScale(1f, tweenDuration);
    }
}
