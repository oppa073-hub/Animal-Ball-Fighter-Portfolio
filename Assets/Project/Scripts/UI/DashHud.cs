using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

// 대쉬 HUD: 화면 아래 칸 게이지 + 조준 중 화면 위쪽의 "대쉬!" 글자(유예 시간만큼 왼쪽→오른쪽으로 색이 차오름).
// 스테이지 HUD 캔버스에 실행 중에 만든다 (CombatUiStyle의 스프라이트/폰트 사용).
public class DashHud : MonoBehaviour
{
    private const float SegmentWidth = 104f;
    private const float SegmentHeight = 26f;
    private const float SegmentSpacing = 10f;
    private const float AimTextWidth = 620f;
    private const float AimTextHeight = 150f;

    private static readonly Color FullColor = new Color(0.45f, 0.85f, 1f);
    private static readonly Color ChargingColor = new Color(0.45f, 0.85f, 1f, 0.45f);
    private static readonly Color AimFillColor = new Color(1f, 0.84f, 0.28f);

    private RectTransform gaugeRoot;
    private readonly List<Image> segmentFills = new();
    private readonly List<RectTransform> segments = new();

    private RectTransform aimRoot;
    private RectTransform aimFillMask;
    private TMP_FontAsset font;
    private Sprite badge;

    public static DashHud Create()
    {
        Canvas hud = HudBanner.FindHudCanvas();
        if (hud == null) return null;

        var go = new GameObject("DashHud", typeof(RectTransform));
        go.transform.SetParent(SafeAreaFitter.GetContentRoot(hud), false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        DashHud dashHud = go.AddComponent<DashHud>();
        dashHud.Build();
        return dashHud;
    }

    private void Build()
    {
        CombatUiStyle style = CombatUiStyle.Get();
        font = style != null ? style.boldFont : null;
        badge = style != null ? style.badge : null;

        // 게이지: 화면 아래 가운데
        gaugeRoot = NewRect("DashGauge", transform);
        gaugeRoot.anchorMin = gaugeRoot.anchorMax = new Vector2(0.5f, 0f);
        gaugeRoot.pivot = new Vector2(0.5f, 0f);
        gaugeRoot.anchoredPosition = new Vector2(0f, 110f);

        // 조준 글자: 보스 체력바 아래, 화면 가운데보다 위
        aimRoot = NewRect("DashAimText", transform);
        aimRoot.anchorMin = aimRoot.anchorMax = new Vector2(0.5f, 0.7f);
        aimRoot.sizeDelta = new Vector2(AimTextWidth, AimTextHeight);

        string label = LocalizationSettings.StringDatabase.GetLocalizedString("UI", "dash_aim_text");
        if (string.IsNullOrEmpty(label)) label = "DASH!";

        TMP_Text baseText = NewText("Base", aimRoot, label, new Color(1f, 1f, 1f, 0.35f));
        Stretch((RectTransform)baseText.transform);

        aimFillMask = NewRect("FillMask", aimRoot);
        aimFillMask.gameObject.AddComponent<RectMask2D>();
        aimFillMask.anchorMin = new Vector2(0f, 0f);
        aimFillMask.anchorMax = new Vector2(0f, 1f);
        aimFillMask.pivot = new Vector2(0f, 0.5f);
        aimFillMask.anchoredPosition = Vector2.zero;
        aimFillMask.sizeDelta = new Vector2(0f, 0f);

        TMP_Text fillText = NewText("Fill", aimFillMask, label, AimFillColor);
        var fillRect = (RectTransform)fillText.transform;
        fillRect.anchorMin = fillRect.anchorMax = new Vector2(0f, 0.5f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(AimTextWidth, AimTextHeight);

        aimRoot.gameObject.SetActive(false);
    }

    public void SetGauge(float charges, int maxCharges)
    {
        if (segments.Count != maxCharges) RebuildSegments(maxCharges);

        for (int i = 0; i < segmentFills.Count; i++)
        {
            float fill = Mathf.Clamp01(charges - i);
            segmentFills[i].fillAmount = fill;
            segmentFills[i].color = fill >= 1f ? FullColor : ChargingColor;
        }
    }

    public void PunchGauge()  //게이지가 모자랄 때 살짝 흔들어 알림
    {
        gaugeRoot.DOKill(true);
        gaugeRoot.DOShakeAnchorPos(0.25f, new Vector2(10f, 0f), 20).SetUpdate(true);
    }

    public void ShowAim()
    {
        aimRoot.gameObject.SetActive(true);
        aimRoot.DOKill();
        aimRoot.localScale = Vector3.one * 0.7f;
        aimRoot.DOScale(1f, 0.18f).SetEase(Ease.OutBack).SetUpdate(true);  //시간이 멈춘 동안에도 재생
        SetAimProgress(0f);
    }

    public void SetAimProgress(float progress)
    {
        aimFillMask.sizeDelta = new Vector2(AimTextWidth * Mathf.Clamp01(progress), 0f);
    }

    public void HideAim()
    {
        aimRoot.DOKill();
        aimRoot.gameObject.SetActive(false);
    }

    private void RebuildSegments(int count)
    {
        foreach (RectTransform segment in segments) Destroy(segment.gameObject);
        segments.Clear();
        segmentFills.Clear();

        float totalWidth = count * SegmentWidth + (count - 1) * SegmentSpacing;
        gaugeRoot.sizeDelta = new Vector2(totalWidth, SegmentHeight);

        for (int i = 0; i < count; i++)
        {
            RectTransform segment = NewRect("Segment" + i, gaugeRoot);
            segment.anchorMin = segment.anchorMax = new Vector2(0f, 0.5f);
            segment.pivot = new Vector2(0f, 0.5f);
            segment.anchoredPosition = new Vector2(i * (SegmentWidth + SegmentSpacing), 0f);
            segment.sizeDelta = new Vector2(SegmentWidth, SegmentHeight);

            Image background = segment.gameObject.AddComponent<Image>();
            background.sprite = badge;
            background.type = badge != null ? Image.Type.Sliced : Image.Type.Simple;
            background.color = new Color(0.04f, 0.11f, 0.25f, 0.75f);
            background.raycastTarget = false;

            RectTransform fillRect = NewRect("Fill", segment);
            Stretch(fillRect);
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);

            Image fill = fillRect.gameObject.AddComponent<Image>();
            fill.sprite = badge;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.raycastTarget = false;

            segments.Add(segment);
            segmentFills.Add(fill);
        }
    }

    private TMP_Text NewText(string objectName, Transform parent, string text, Color color)
    {
        RectTransform rect = NewRect(objectName, parent);
        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.color = color;
        tmp.fontSize = 110f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        tmp.outlineWidth = 0.22f;
        tmp.outlineColor = new Color32(20, 30, 60, 255);
        return tmp;
    }

    private static RectTransform NewRect(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
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
        gaugeRoot?.DOKill();
        aimRoot?.DOKill();
    }
}
