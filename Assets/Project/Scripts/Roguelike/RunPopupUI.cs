using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

// 로그라이크 진행용 공용 팝업 (갈림길 문, 상점, 휴식, 이벤트, 시작 축복, 런 요약). 실행 중에 HUD 캔버스에 만든다.
// 제목 + 설명 + 세로로 쌓이는 선택지 카드 + (선택) 하단 버튼. 상단에 런 골드 표시.
public class RunPopupUI : MonoBehaviour
{
    public class Option
    {
        public string title;
        public string description;
        public string tag;            //오른쪽 위 작은 글자 (가격, 등급 등)
        public Color color = new Color(0.45f, 0.8f, 1f);
        public Sprite icon;
        public bool enabled = true;
        public System.Action onClick;
    }

    private const float WindowWidth = 940f;
    private const float OptionHeight = 210f;
    private const float CompactOptionHeight = 165f;
    private float optionHeight = OptionHeight;
    private const float OptionSpacing = 22f;

    private static RunPopupUI instance;

    private CombatUiStyle style;
    private RectTransform window;
    private Image windowFrame;
    private TMP_Text titleText;
    private TMP_Text subtitleText;
    private TMP_Text goldText;
    private RectTransform optionRoot;
    private Button footerButton;
    private TMP_Text footerLabel;
    private System.Action footerAction;
    private bool inputLocked;

    public static bool IsOpen => instance != null && instance.gameObject.activeSelf;

    public static void Show(string title, string subtitle, List<Option> options, string footer = null, System.Action onFooter = null)
    {
        if (instance == null)
        {
            Canvas hud = ComboSystem.FindHudCanvas();
            if (hud == null)
            {
                Debug.LogError("[RunPopup] HUD 캔버스를 찾지 못했습니다. 팝업 없이 진행합니다.");

                // 진행이 멈추지 않도록: 하단 버튼이 있으면 그것, 없으면 첫 번째로 고를 수 있는 선택지를 자동 실행
                if (onFooter != null) { onFooter.Invoke(); return; }
                Option fallback = options?.Find(o => o.enabled && o.onClick != null);
                fallback?.onClick.Invoke();
                return;
            }

            GameObject go = new GameObject("RunPopup", typeof(RectTransform));
            go.transform.SetParent(hud.transform, false);
            instance = go.AddComponent<RunPopupUI>();
            instance.Build();
        }

        instance.transform.SetAsLastSibling();
        instance.ShowInternal(title, subtitle, options, footer, onFooter);
    }

    public static void Hide()
    {
        if (instance == null) return;

        instance.window.DOKill();
        instance.gameObject.SetActive(false);
    }

    public static string Loc(string key, string fallback)
    {
        string value = LocalizationSettings.StringDatabase.GetLocalizedString("UI", key);
        return string.IsNullOrEmpty(value) || value.StartsWith("No translation") ? fallback : value;
    }

    private void OnEnable() => RunState.GoldChanged += HandleGoldChanged;
    private void OnDisable() => RunState.GoldChanged -= HandleGoldChanged;
    private void HandleGoldChanged(int gold) { if (goldText != null) goldText.text = $"{gold:N0} G"; }

    private void ShowInternal(string title, string subtitle, List<Option> options, string footer, System.Action onFooter)
    {
        gameObject.SetActive(true);
        inputLocked = false;

        titleText.text = title;
        subtitleText.text = subtitle ?? "";
        subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
        goldText.text = $"{RunState.Gold:N0} G";

        for (int i = optionRoot.childCount - 1; i >= 0; i--) Destroy(optionRoot.GetChild(i).gameObject);

        int count = options != null ? options.Count : 0;
        optionHeight = count > 4 ? CompactOptionHeight : OptionHeight;  //선택지가 많으면(승천 6단계 등) 화면에 들어가도록 카드를 낮춤
        float listHeight = count * optionHeight + Mathf.Max(0, count - 1) * OptionSpacing;
        bool hasFooter = !string.IsNullOrEmpty(footer);
        float windowHeight = 300f + listHeight + (hasFooter ? 150f : 40f);
        window.sizeDelta = new Vector2(WindowWidth, windowHeight);

        titleText.rectTransform.anchoredPosition = new Vector2(0f, windowHeight * 0.5f - 85f);
        subtitleText.rectTransform.anchoredPosition = new Vector2(0f, windowHeight * 0.5f - 160f);
        goldText.rectTransform.anchoredPosition = new Vector2(WindowWidth * 0.5f - 130f, windowHeight * 0.5f - 40f);

        float top = windowHeight * 0.5f - 230f;
        optionRoot.anchoredPosition = Vector2.zero;

        for (int i = 0; i < count; i++)
        {
            float y = top - optionHeight * 0.5f - i * (optionHeight + OptionSpacing);
            CreateOption(options[i], new Vector2(0f, y), i);
        }

        footerAction = onFooter;
        footerButton.gameObject.SetActive(hasFooter);
        if (hasFooter)
        {
            footerLabel.text = footer;
            ((RectTransform)footerButton.transform).anchoredPosition = new Vector2(0f, -windowHeight * 0.5f + 85f);
            footerButton.interactable = true;
        }

        window.DOKill();
        window.localScale = Vector3.one * 0.85f;
        window.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private void CreateOption(Option option, Vector2 position, int index)
    {
        // 카드 색은 진하게 눌러서 흰 글자가 잘 읽히도록 (연한 하늘/노랑 위 흰 글자는 안 보임)
        Color cardColor = option.enabled ? Color.Lerp(option.color, new Color(0.05f, 0.08f, 0.2f), 0.45f) : new Color(0.25f, 0.28f, 0.36f);
        Image frame = NewImage("Option" + index, optionRoot, style != null ? style.nodeFrame : null, cardColor);
        frame.type = Image.Type.Sliced;
        frame.rectTransform.anchoredPosition = position;
        frame.rectTransform.sizeDelta = new Vector2(WindowWidth - 90f, optionHeight);

        if (option.onClick != null)  //onClick이 없으면 정보 표시용 (요약 등)
        {
            Button button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.interactable = option.enabled;
            button.onClick.AddListener(() =>
            {
                if (inputLocked) return;
                inputLocked = true;  //같은 프레임 중복 입력 방지 (다음 Show에서 풀림)
                SoundManager.Instance?.PlaySFX(SoundId.UI_Click);
                frame.transform.DOPunchScale(Vector3.one * 0.06f, 0.18f, 6, 0.5f).SetUpdate(true);
                option.onClick.Invoke();
            });
        }
        else frame.raycastTarget = false;

        float textLeft = 40f;
        if (option.icon != null)
        {
            Image icon = NewImage("Icon", frame.transform, option.icon, Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(100f, 0f);
            icon.rectTransform.sizeDelta = new Vector2(130f, 130f);
            textLeft = 185f;
        }

        TMP_Text title = NewText("Title", frame.transform, 42f, Color.white, Vector2.zero, Vector2.zero);
        title.alignment = TextAlignmentOptions.Left;
        title.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.offsetMin = new Vector2(textLeft, 0f);
        title.rectTransform.offsetMax = new Vector2(-170f, -18f);
        title.enableAutoSizing = true;
        title.fontSizeMin = 26f;
        title.fontSizeMax = 42f;
        title.text = option.title;

        TMP_Text desc = NewText("Desc", frame.transform, 28f, new Color(1f, 1f, 1f, 0.92f), Vector2.zero, Vector2.zero);
        desc.alignment = TextAlignmentOptions.TopLeft;
        desc.fontStyle = FontStyles.Normal;
        desc.textWrappingMode = TextWrappingModes.Normal;
        desc.rectTransform.anchorMin = new Vector2(0f, 0f);
        desc.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        desc.rectTransform.offsetMin = new Vector2(textLeft, 14f);
        desc.rectTransform.offsetMax = new Vector2(-30f, 0f);
        desc.enableAutoSizing = true;
        desc.fontSizeMin = 18f;
        desc.fontSizeMax = 28f;
        desc.text = option.description ?? "";

        if (!string.IsNullOrEmpty(option.tag))
        {
            TMP_Text tag = NewText("Tag", frame.transform, 34f, new Color(1f, 0.9f, 0.45f), Vector2.zero, Vector2.zero);
            tag.alignment = TextAlignmentOptions.Right;
            tag.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            tag.rectTransform.anchorMax = new Vector2(1f, 1f);
            tag.rectTransform.offsetMin = new Vector2(-220f, 0f);
            tag.rectTransform.offsetMax = new Vector2(-30f, -18f);
            tag.enableAutoSizing = true;
            tag.fontSizeMin = 20f;
            tag.fontSizeMax = 34f;
            tag.text = option.tag;
        }

        frame.transform.localScale = Vector3.zero;
        frame.transform.DOScale(1f, 0.22f).SetDelay(0.08f + index * 0.06f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private void OnFooterClicked()
    {
        if (inputLocked) return;
        inputLocked = true;
        SoundManager.Instance?.PlaySFX(SoundId.UI_Click);
        footerAction?.Invoke();
    }

    // ───────────── 화면 구성 ─────────────

    private void Build()
    {
        style = CombatUiStyle.Get();
        RectTransform root = (RectTransform)transform;
        Stretch(root);

        Image dim = NewImage("Dim", root, null, new Color(0.01f, 0.04f, 0.12f, 0.78f));
        Stretch(dim.rectTransform);  //뒤쪽 터치 차단

        window = NewRect("Window", root);
        windowFrame = window.gameObject.AddComponent<Image>();
        windowFrame.sprite = style != null ? style.popupFrame : null;
        windowFrame.type = Image.Type.Sliced;
        if (windowFrame.sprite == null) windowFrame.color = new Color(0.05f, 0.15f, 0.35f);

        titleText = NewText("Title", window, 58f, Color.white, Vector2.zero, new Vector2(WindowWidth - 260f, 90f));
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 34f;
        titleText.fontSizeMax = 58f;

        subtitleText = NewText("Subtitle", window, 30f, new Color(0.75f, 0.85f, 1f), Vector2.zero, new Vector2(WindowWidth - 120f, 70f));
        subtitleText.fontStyle = FontStyles.Normal;
        subtitleText.textWrappingMode = TextWrappingModes.Normal;
        subtitleText.enableAutoSizing = true;
        subtitleText.fontSizeMin = 20f;
        subtitleText.fontSizeMax = 30f;

        goldText = NewText("Gold", window, 34f, new Color(1f, 0.85f, 0.3f), Vector2.zero, new Vector2(220f, 50f));
        goldText.alignment = TextAlignmentOptions.Right;

        optionRoot = NewRect("Options", window);

        Image footerImage = NewImage("Footer", window, style != null ? style.yellowButton : null, Color.white);
        footerImage.type = Image.Type.Sliced;
        if (footerImage.sprite == null) footerImage.color = new Color(0.95f, 0.75f, 0.25f);
        footerImage.rectTransform.sizeDelta = new Vector2(440f, 110f);
        footerButton = footerImage.gameObject.AddComponent<Button>();
        footerButton.targetGraphic = footerImage;
        footerButton.onClick.AddListener(OnFooterClicked);

        footerLabel = NewText("Label", footerImage.transform, 38f, Color.white, new Vector2(0f, 4f), new Vector2(400f, 90f));
        footerLabel.enableAutoSizing = true;
        footerLabel.fontSizeMin = 22f;
        footerLabel.fontSizeMax = 38f;
    }

    private TMP_Text NewText(string name, Transform parent, float size, Color color, Vector2 position, Vector2 rectSize)
    {
        RectTransform rect = NewRect(name, parent);
        rect.anchoredPosition = position;
        rect.sizeDelta = rectSize;

        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (style != null && style.boldFont != null) text.font = style.boldFont;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.outlineWidth = 0.25f;  //밝은 배경에서도 읽히도록 외곽선을 두껍게
        text.outlineColor = new Color32(5, 12, 35, 255);
        return text;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rect = NewRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        window?.DOKill();
        if (instance == this) instance = null;
    }
}
