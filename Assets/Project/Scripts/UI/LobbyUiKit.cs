using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 코드로 만드는 로비 팝업(출석/미션, 랭킹, 도감 등)이 같은 모양을 쓰도록 모아 둔 그림/글꼴 (Resources/LobbyUiKit 에셋)
// 골드 상점 팝업과 같은 Layer Lab 스프라이트를 쓴다.
[CreateAssetMenu(fileName = "LobbyUiKit", menuName = "BounceSquad/Lobby UI Kit")]
public class LobbyUiKit : ScriptableObject
{
    public const string ResourcePath = "LobbyUiKit";

    [Header("Frames")]
    public Sprite popupFrame;
    public Sprite cellFrame;

    [Header("Buttons")]
    public Sprite greenButton;
    public Sprite grayButton;
    public Sprite skyButton;
    public Sprite redButton;
    public Sprite yellowButton;
    public Sprite circleButton;  //흰 원 (색을 입혀 씀)

    [Header("Icons")]
    public Sprite closeIcon;
    public Sprite checkIcon;
    public Sprite coinIcon;
    public Sprite calendarIcon;
    public Sprite missionIcon;
    public Sprite rankingIcon;
    public Sprite bookIcon;
    public Sprite giftIcon;
    public Sprite lockIcon;
    public Sprite crownIcon;
    public Sprite settingIcon;  //로비 사이드 메뉴 '기타' 버튼

    [Header("Font")]
    public TMP_FontAsset boldFont;
    public Material outlineThickMaterial;
    public Material outlineMaterial;
    public Material shadowMaterial;

    private static LobbyUiKit cached;

    public static LobbyUiKit Load()
    {
        if (cached == null) cached = Resources.Load<LobbyUiKit>(ResourcePath);
        return cached;
    }

    // ── 만들기 도우미 ──

    public Image Image(Transform parent, string name, Sprite sprite, Color color, bool sliced = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        image.preserveAspect = !sliced && sprite != null;
        image.raycastTarget = false;
        return image;
    }

    public TMP_Text Text(Transform parent, string name, string text, float size, Material material = null, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        if (boldFont != null) label.font = boldFont;
        if (material != null) label.fontSharedMaterial = material;
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = Color.white;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    public Button Button(Transform parent, string name, Sprite sprite, Vector2 size, Action onClick)
    {
        Image image = Image(parent, name, sprite, Color.white, true);
        image.raycastTarget = true;
        image.rectTransform.sizeDelta = size;

        Button button = image.gameObject.AddComponent<Button>();
        if (onClick != null)
        {
            button.onClick.AddListener(() =>
            {
                SoundManager.Instance?.PlaySFX(SoundId.UI_Click);
                onClick();
            });
        }

        return button;
    }

    // 전체 화면 어둡게 + 가운데 창 + 제목 + 닫기 버튼. content에 내용을 채운다.
    public RectTransform Popup(Transform parent, string name, string title, Vector2 size, Action onClose, out RectTransform content)
    {
        RectTransform root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        Stretch(root);

        Image dim = Image(root, "Dim", null, new Color(0.01f, 0.04f, 0.12f, 0.78f));
        dim.raycastTarget = true;
        RectTransform dimRect = dim.rectTransform;
        Stretch(dimRect);
        dimRect.offsetMin = new Vector2(-800f, -500f);  //안전 영역 밖(노치, 태블릿 양옆)까지
        dimRect.offsetMax = new Vector2(800f, 500f);

        Image window = Image(root, "Window", popupFrame, Color.white, true);
        window.raycastTarget = true;
        RectTransform windowRect = window.rectTransform;
        windowRect.sizeDelta = size;

        TMP_Text titleText = Text(windowRect, "Title", title, 60f, outlineThickMaterial);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -40f);
        titleRect.sizeDelta = new Vector2(size.x - 220f, 100f);
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 36f;
        titleText.fontSizeMax = 60f;

        Button close = Button(windowRect, "CloseButton", redButton, new Vector2(96f, 96f), onClose);
        RectTransform closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-62f, -62f);
        Image closeIconImage = Image(closeRect, "Icon", closeIcon, Color.white);
        Stretch(closeIconImage.rectTransform);
        closeIconImage.rectTransform.offsetMin = new Vector2(18f, 18f);
        closeIconImage.rectTransform.offsetMax = new Vector2(-18f, -18f);

        content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(windowRect, false);
        Stretch(content);
        content.offsetMin = new Vector2(40f, 40f);
        content.offsetMax = new Vector2(-40f, -160f);

        return root;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static string Loc(string key, string fallback)
    {
        try
        {
            // 표에 없는 키는 '번역 없음' 안내 문구 대신 fallback을 쓴다
            UnityEngine.Localization.Tables.StringTable table = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.GetTable("UI");
            UnityEngine.Localization.Tables.StringTableEntry entry = table != null ? table.GetEntry(key) : null;
            if (entry == null) return fallback;

            string value = entry.GetLocalizedString();
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
        catch (Exception)
        {
            return fallback;
        }
    }
}
