using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LobbyPageController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private RectTransform pageContainer;
    [SerializeField] private float pageWidth = 1080f;
    [SerializeField] private float moveDuration = 0.25f;
    [SerializeField] private int pageCount = 3;
    [SerializeField] private float swipeThreshold = 150f;
    [SerializeField] private float swipeVelocityThreshold = 800f;

    [SerializeField] private Image[] navigationIcons;
    [SerializeField] private TMP_Text[] navigationTexts;

    [SerializeField] private Color selectedColor;
    [SerializeField] private Color normalColor;

    [SerializeField] private RectTransform[] navigationItems;

    [Header("선택 탭 배경 (비워두면 색/크기만 변경)")]
    [SerializeField] private Image[] navigationBackgrounds;
    [SerializeField] private Sprite selectedBackground;
    [SerializeField] private Sprite normalBackground;
    [SerializeField] private float selectedScale = 1.12f;
    [SerializeField] private float navTweenDuration = 0.15f;

    [SerializeField] private CharacterPreviewController characterPreviewController;

    [SerializeField] private GameObject[] pagePopups;

    private float dragStartTime;
    private Canvas rootCanvas;

    private int currentPage = 1;
    private float dragStartX;

    private void Start()
    {
        MoveToPage(1);
    }
    private void OnDestroy()
    {
        pageContainer?.DOKill();
    }

    public void MoveToPage(int index)
    {
        ClosePagePopups();

        currentPage = Mathf.Clamp(index, 0, pageCount - 1);
        RefreshNavigation();
        float targetX = -pageWidth * currentPage;

        pageContainer.DOKill();

        pageContainer
            .DOAnchorPosX(targetX, moveDuration)
            .SetEase(Ease.OutCubic);

        if (currentPage == 0)
        {
            characterPreviewController.RefreshCurrentInfo();
        }
    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        pageContainer.DOKill();
        dragStartTime = Time.unscaledTime;
        dragStartX = eventData.position.x;
    }
    public void OnDrag(PointerEventData eventData)
    {
        float newX = pageContainer.anchoredPosition.x + eventData.delta.x / GetCanvasScale();  //화면 픽셀 → 캔버스 단위

        float minX = -pageWidth * (pageCount - 1);
        float maxX = 0f;

        newX = Mathf.Clamp(newX, minX, maxX);

        pageContainer.anchoredPosition = new Vector2(newX, pageContainer.anchoredPosition.y);
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        float dragDistance = (eventData.position.x - dragStartX) / GetCanvasScale();
        float dragTime = Time.unscaledTime - dragStartTime;

        float velocity = dragDistance / Mathf.Max(dragTime, 0.01f);

        bool enoughDistance = Mathf.Abs(dragDistance) >= swipeThreshold;
        bool fastSwipe = Mathf.Abs(velocity) >= swipeVelocityThreshold;

        if (enoughDistance || fastSwipe)
        {
            if (dragDistance < 0) MoveToPage(currentPage + 1);
            else MoveToPage(currentPage - 1);
        }
        else
        {
            MoveToPage(currentPage);
        }
    }
    private float GetCanvasScale()
    {
        if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        return rootCanvas != null && rootCanvas.scaleFactor > 0f ? rootCanvas.scaleFactor : 1f;
    }
    private void RefreshNavigation()
    {
        for (int i = 0; i < navigationIcons.Length; i++)
        {
            bool isSelected = i == currentPage;

            navigationIcons[i].color = isSelected ? selectedColor : normalColor;

            navigationTexts[i].color = isSelected ? selectedColor : normalColor;

            if (navigationBackgrounds != null && i < navigationBackgrounds.Length && navigationBackgrounds[i] != null)
            {
                Sprite background = isSelected ? selectedBackground : normalBackground;
                if (background != null) navigationBackgrounds[i].sprite = background;
            }

            navigationItems[i].DOKill();
            navigationItems[i]
                .DOScale(isSelected ? selectedScale : 1f, navTweenDuration)
                .SetEase(Ease.OutBack);
        }
    }
    private void ClosePagePopups()
    {
        for (int i = 0; i < pagePopups.Length; i++)
        {
            if (pagePopups[i] != null) pagePopups[i].SetActive(false);
        }
    }
}
