using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 스테이지 HUD에 잠깐 띄우는 리본형 알림 배너 (시너지 발동 등). 스테이지 씬에서 처음 쓸 때 생성된다.
// 구성: 작은 제목 + 노란 리본 위의 큰 이름 + 양옆 별. 스프라이트는 CombatUiStyle에서 읽는다.
public class HudBanner : MonoBehaviour
{
    private const float RibbonWidth = 720f;
    private const float RibbonHeight = 160f;

    private static HudBanner instance;

    private RectTransform container;
    private CanvasGroup group;
    private RectTransform ribbon;
    private TMP_Text titleText;
    private TMP_Text nameText;
    private RectTransform leftStar;
    private RectTransform rightStar;
    private Sequence sequence;
    private bool searchFailed;

    public static void Show(string title, string name, float duration = 1.6f)
    {
        if (string.IsNullOrEmpty(name)) return;

        if (instance == null) instance = new GameObject("HudBanner").AddComponent<HudBanner>();
        instance.ShowInternal(title, name, duration);
    }

    private void ShowInternal(string title, string name, float duration)
    {
        if (!EnsureBanner()) return;

        titleText.text = title;
        titleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
        nameText.text = name;

        sequence?.Kill();
        container.anchoredPosition = Vector2.zero;
        group.alpha = 0f;
        ribbon.localScale = new Vector3(0.2f, 1f, 1f);
        nameText.transform.localScale = Vector3.zero;
        titleText.rectTransform.anchoredPosition = new Vector2(0f, RibbonHeight * 0.5f + 70f);
        leftStar.localScale = rightStar.localScale = Vector3.zero;
        leftStar.localRotation = Quaternion.Euler(0f, 0f, 180f);
        rightStar.localRotation = Quaternion.Euler(0f, 0f, -180f);

        sequence = DOTween.Sequence().SetUpdate(true)
            .Append(group.DOFade(1f, 0.12f))
            .Join(ribbon.DOScaleX(1f, 0.3f).SetEase(Ease.OutBack))  //리본이 펼쳐짐
            .Join(titleText.rectTransform.DOAnchorPosY(RibbonHeight * 0.5f + 40f, 0.3f).SetEase(Ease.OutCubic))
            .Insert(0.15f, nameText.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 2.5f))
            .Insert(0.22f, leftStar.DOScale(1f, 0.35f).SetEase(Ease.OutBack))
            .Insert(0.22f, leftStar.DORotate(new Vector3(0f, 0f, 15f), 0.35f).SetEase(Ease.OutCubic))
            .Insert(0.28f, rightStar.DOScale(1f, 0.35f).SetEase(Ease.OutBack))
            .Insert(0.28f, rightStar.DORotate(new Vector3(0f, 0f, -15f), 0.35f).SetEase(Ease.OutCubic))
            .Insert(0.5f, nameText.transform.DOPunchScale(Vector3.one * 0.08f, 0.3f, 6, 0.5f))
            .AppendInterval(duration)
            .Append(group.DOFade(0f, 0.3f))
            .Join(container.DOAnchorPosY(50f, 0.3f).SetEase(Ease.InCubic));
    }

    private bool EnsureBanner()
    {
        if (container != null) return true;
        if (searchFailed) return false;

        Canvas hud = FindHudCanvas();
        if (hud == null)
        {
            searchFailed = true;
            return false;
        }

        CombatUiStyle style = CombatUiStyle.Get();
        TMP_FontAsset font = style != null && style.boldFont != null ? style.boldFont : hud.GetComponentInChildren<TMP_Text>(true)?.font;

        // 전체를 담는 부모 (페이드/이동용)
        GameObject root = new GameObject("HudBanner", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(SafeAreaFitter.GetContentRoot(hud), false);

        container = (RectTransform)root.transform;
        container.anchorMin = container.anchorMax = new Vector2(0.5f, 0.66f);
        container.sizeDelta = new Vector2(RibbonWidth, RibbonHeight);

        group = root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0f;

        // 리본 배경
        ribbon = CreateRect("Ribbon", container, new Vector2(RibbonWidth, RibbonHeight));
        Image ribbonImage = ribbon.gameObject.AddComponent<Image>();
        ribbonImage.raycastTarget = false;
        if (style != null && style.ribbon != null)
        {
            ribbonImage.sprite = style.ribbon;
            ribbonImage.type = Image.Type.Sliced;
        }
        else
        {
            ribbonImage.color = new Color(1f, 0.75f, 0.1f);
        }

        // 리본 위 이름 (리본 가운데 판 영역)
        nameText = CreateText("Name", ribbon, new Vector2(RibbonWidth - 300f, RibbonHeight - 50f), font, 40f, 64f);
        nameText.rectTransform.anchoredPosition = new Vector2(0f, 14f);
        nameText.color = Color.white;
        nameText.outlineWidth = 0.32f;
        nameText.outlineColor = new Color32(150, 70, 0, 255);
        ApplyShadow(nameText, new Color(0.45f, 0.18f, 0f, 0.8f));

        // 리본 위쪽 작은 제목
        titleText = CreateText("Title", container, new Vector2(RibbonWidth, 70f), font, 34f, 50f);
        titleText.color = Color.white;
        titleText.outlineWidth = 0.2f;
        titleText.outlineColor = new Color32(200, 90, 0, 255);
        titleText.characterSpacing = 6f;
        ApplyShadow(titleText, new Color(0f, 0f, 0f, 0.55f));

        // 양옆 별
        leftStar = CreateStar("LeftStar", style, new Vector2(-RibbonWidth * 0.5f + 40f, 40f));
        rightStar = CreateStar("RightStar", style, new Vector2(RibbonWidth * 0.5f - 40f, 40f));

        return true;
    }

    private RectTransform CreateStar(string objectName, CombatUiStyle style, Vector2 position)
    {
        RectTransform star = CreateRect(objectName, container, new Vector2(96f, 96f));
        star.anchoredPosition = position;

        Image image = star.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.sprite = style != null ? style.star : null;
        star.gameObject.SetActive(image.sprite != null);
        return star;
    }

    private static RectTransform CreateRect(string objectName, Transform parent, Vector2 size)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)child.transform;
        rect.sizeDelta = size;
        return rect;
    }

    private static TMP_Text CreateText(string objectName, Transform parent, Vector2 size, TMP_FontAsset font, float minSize, float maxSize)
    {
        RectTransform rect = CreateRect(objectName, parent, size);

        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = maxSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    public static void ApplyShadow(TMP_Text text, Color color)  //TMP 언더레이로 아래쪽 그림자 (밝은 배경에서도 글자가 뜨도록)
    {
        Material material = text.fontMaterial;  //인스턴스 머티리얼
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, color);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.4f);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.2f);
    }

    public static Canvas FindHudCanvas()  //스테이지 HUD (메인 카메라를 쓰는 Screen Space Camera 캔버스, 정렬 0)
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || !canvas.gameObject.activeInHierarchy) continue;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == Camera.main && canvas.sortingOrder == 0) return canvas;
        }

        return null;
    }

    private void OnDestroy()
    {
        sequence?.Kill();
        if (instance == this) instance = null;
    }
}
