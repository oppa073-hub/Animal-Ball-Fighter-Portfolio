using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using TMPro;
using DG.Tweening;

public class AugmentPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private AugmentCardUI[] cards;

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float showDuration = 0.2f;

    [Header("Reroll (비워두면 런타임에 생성)")]
    [SerializeField] private Button rerollButton;
    [SerializeField] private TMP_Text rerollLabel;

    private const float RerollButtonTopOffset = 340f;  //패널 중앙 기준, 카드 절반 높이(300) + 여백

    private const float CardHalfHeight = 300f;  //카드 높이 600의 절반 (패널 중앙 기준)
    private OwnedAugmentBar ownedBar;

    private Image rerollIcon;
    private bool rerollFree;
    private bool rerollAd;

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

        foreach (AugmentCardUI card in cards) card.SuppressHover = false;  //버리기 연출 중 닫혔던 경우 복구

        cards[0].Initialize(choices[0]);
        cards[1].Initialize(choices[1]);
        cards[2].Initialize(choices[2]);

        EnsureRerollButton();
        RefreshRerollButton(true);
        PlayRerollButtonIntro();
        EnsureBanishButtons();
        RefreshBanishButtons();

        if (ownedBar == null) ownedBar = new OwnedAugmentBar(root.transform, CardHalfHeight);  //카드 위 보유 증강 줄
        ownedBar.Refresh();

        canvasGroup.DOFade(1f, showDuration);

        root.transform
            .DOScale(Vector3.one, showDuration)
            .SetEase(Ease.OutBack);
    }
    public void Hide(System.Action onComplete)
    {
        canvasGroup.DOKill();
        root.transform.DOKill();

        if (rerollButton != null) rerollButton.interactable = false;
        if (banishButtons != null) foreach (Button button in banishButtons) if (button != null) button.interactable = false;

        canvasGroup.DOFade(0f, 0.12f);

        root.transform
            .DOScale(Vector3.one * 0.95f, 0.12f)
            .OnComplete(() =>
            {
                root.SetActive(false);
                onComplete?.Invoke();
            });
    }

    private void Update()  //광고 로드 완료 등으로 리롤 가능 상태가 바뀔 수 있으므로 열려 있는 동안 갱신
    {
        if (root != null && root.activeSelf) RefreshRerollButton(false);
    }

    private void OnRerollClicked()
    {
        if (AugmentManager.Instance == null) return;

        AugmentManager.Instance.RequestReroll();
    }

    private void RefreshRerollButton(bool force)
    {
        if (rerollButton == null) return;

        AugmentManager manager = AugmentManager.Instance;
        bool free = manager != null && manager.HasFreeReroll;
        bool ad = manager != null && !free && manager.CanRerollWithAd;

        if (!force && free == rerollFree && ad == rerollAd) return;

        rerollFree = free;
        rerollAd = ad;

        rerollButton.gameObject.SetActive(free || ad);
        rerollButton.interactable = free || ad;

        if (rerollLabel != null)
        {
            rerollLabel.text = free
                ? GetLocalized("augment_reroll_free", "Free Reroll")
                : GetLocalized("augment_reroll_ad", "Watch Ad to Reroll");
        }

        UpdateRerollIcon(free);
    }

    private static string GetLocalized(string key, string fallback)
    {
        string localized = LocalizationSettings.StringDatabase.GetLocalizedString("UI", key);
        return string.IsNullOrEmpty(localized) ? fallback : localized;
    }

    private void EnsureRerollButton()
    {
        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveListener(OnRerollClicked);
            rerollButton.onClick.AddListener(OnRerollClicked);
            return;
        }

        CombatUiStyle style = CombatUiStyle.Get();

        GameObject buttonObject = new GameObject("RerollButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(root.transform, false);
        buttonObject.GetComponent<LayoutElement>().ignoreLayout = true;  //카드 가로 배치(HorizontalLayoutGroup)에서 제외

        // 카드(높이 600) 바로 아래 중앙
        RectTransform rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -RerollButtonTopOffset);
        rect.sizeDelta = new Vector2(460f, 130f);

        Image background = buttonObject.GetComponent<Image>();
        if (style != null && style.skyButton != null)
        {
            background.sprite = style.skyButton;
            background.type = Image.Type.Sliced;
        }
        else
        {
            background.color = new Color(0.45f, 0.8f, 1f);
        }

        rerollButton = buttonObject.GetComponent<Button>();
        rerollButton.targetGraphic = background;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(buttonObject.transform, false);

        RectTransform iconRect = (RectTransform)iconObject.transform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(70f, 4f);
        iconRect.sizeDelta = new Vector2(72f, 72f);

        rerollIcon = iconObject.GetComponent<Image>();
        rerollIcon.raycastTarget = false;
        rerollIcon.preserveAspect = true;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(115f, 8f);
        labelRect.offsetMax = new Vector2(-30f, 0f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        if (style != null && style.boldFont != null) label.font = style.boldFont;
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 26f;
        label.fontSizeMax = 46f;
        label.color = Color.black;  //메인 로비 하늘색 버튼과 동일

        rerollLabel = label;
        rerollButton.onClick.AddListener(OnRerollClicked);
    }

    private void UpdateRerollIcon(bool free)
    {
        if (rerollIcon == null) return;

        CombatUiStyle style = CombatUiStyle.Get();
        Sprite icon = style == null ? null : (free ? style.refreshIcon : style.adIcon);

        rerollIcon.sprite = icon;
        rerollIcon.color = free ? new Color(0.1f, 0.1f, 0.1f) : Color.white;  //새로고침은 검은 픽토그램, 광고는 원본 색
        rerollIcon.gameObject.SetActive(icon != null);
    }

    // ───────── 버리기 (카드 오른쪽 위 X): 이번 판에서 그 증강을 빼고 새 카드로 교체 ─────────

    private Button[] banishButtons;
    private TMP_Text banishInfo;

    private void EnsureBanishButtons()
    {
        if (banishButtons != null) return;

        CombatUiStyle style = CombatUiStyle.Get();
        banishButtons = new Button[cards.Length];

        for (int i = 0; i < cards.Length; i++)
        {
            int index = i;

            GameObject buttonObject = new GameObject("BanishButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(cards[i].transform, false);
            buttonObject.GetComponent<LayoutElement>().ignoreLayout = true;

            RectTransform rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-14f, -14f);
            rect.sizeDelta = new Vector2(84f, 84f);

            Image background = buttonObject.GetComponent<Image>();
            background.sprite = style != null ? style.redButton : null;
            background.type = Image.Type.Sliced;
            if (background.sprite == null) background.color = new Color(0.9f, 0.3f, 0.3f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => AugmentManager.Instance?.RequestBanish(index));
            banishButtons[i] = button;

            Image icon = null;
            if (style != null && style.closeIcon != null)
            {
                GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(buttonObject.transform, false);
                RectTransform iconRect = (RectTransform)iconObject.transform;
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(16f, 18f);
                iconRect.offsetMax = new Vector2(-16f, -12f);
                icon = iconObject.GetComponent<Image>();
                icon.sprite = style.closeIcon;
                icon.raycastTarget = false;
            }

            if (icon == null)
            {
                TMP_Text label = CreateLabel(buttonObject.transform, 44f, Color.white);
                label.text = "X";
            }
        }

        GameObject infoObject = new GameObject("BanishInfo", typeof(RectTransform), typeof(LayoutElement));
        infoObject.transform.SetParent(root.transform, false);
        infoObject.GetComponent<LayoutElement>().ignoreLayout = true;

        RectTransform infoRect = (RectTransform)infoObject.transform;
        infoRect.anchorMin = infoRect.anchorMax = new Vector2(0.5f, 0.5f);
        infoRect.pivot = new Vector2(0.5f, 1f);
        infoRect.anchoredPosition = new Vector2(0f, -RerollButtonTopOffset - 145f);  //리롤 버튼 아래
        infoRect.sizeDelta = new Vector2(700f, 60f);

        banishInfo = CreateLabel(infoObject.transform, 30f, new Color(1f, 0.75f, 0.75f));
    }

    private TMP_Text CreateLabel(Transform parent, float size, Color color)
    {
        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);

        RectTransform labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        CombatUiStyle style = CombatUiStyle.Get();
        if (style != null && style.boldFont != null) label.font = style.boldFont;
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = size;
        label.fontStyle = FontStyles.Bold;
        label.color = color;
        label.outlineWidth = 0.2f;
        label.outlineColor = new Color32(30, 10, 10, 255);
        return label;
    }

    public void ReplaceCard(int index, AugmentData data)  //버리기: 해당 카드만 옆으로 뒤집히며 새 증강으로 (다른 카드는 그대로)
    {
        if (cards == null || index < 0 || index >= cards.Length || data == null) return;

        AugmentCardUI card = cards[index];
        Transform cardTransform = card.transform;

        if (banishButtons != null) foreach (Button button in banishButtons) if (button != null) button.interactable = false;  //뒤집는 동안 중복 입력 방지

        card.SuppressHover = true;  //접히는 동안 포인터가 드나들며 호버 연출이 이 애니메이션을 끊지 않도록
        cardTransform.DOKill();
        cardTransform.DOScaleX(0f, 0.12f).SetEase(Ease.InQuad).SetUpdate(true).OnComplete(() =>
        {
            card.Initialize(data);  //내부에서 크기를 1로 되돌리므로 다시 접힌 상태에서 펼침
            cardTransform.localScale = new Vector3(0f, 1f, 1f);
            cardTransform.DOScaleX(1f, 0.2f).SetEase(Ease.OutBack).SetUpdate(true).OnComplete(() =>
            {
                card.SuppressHover = false;
                RefreshBanishButtons();
            });
        });
    }

    private void RefreshBanishButtons()
    {
        if (banishButtons == null) return;

        bool canBanish = AugmentManager.Instance != null && AugmentManager.Instance.CanBanish;

        foreach (Button button in banishButtons)
        {
            if (button == null) continue;
            button.gameObject.SetActive(canBanish);
            button.interactable = canBanish;
        }

        if (banishInfo != null)
        {
            banishInfo.gameObject.SetActive(canBanish);
            banishInfo.text = string.Format(GetLocalized("augment_banish_info_format", "Banish left: {0}"), RunState.BanishesLeft);
        }
    }

    private void PlayRerollButtonIntro()
    {
        if (rerollButton == null || !rerollButton.gameObject.activeSelf) return;

        Transform buttonTransform = rerollButton.transform;
        buttonTransform.DOKill();
        buttonTransform.localScale = Vector3.zero;
        buttonTransform.DOScale(1f, 0.25f).SetDelay(showDuration).SetEase(Ease.OutBack);
    }
}
