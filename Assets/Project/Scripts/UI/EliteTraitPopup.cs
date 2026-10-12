using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 엘리트 특성 이름 배지. 평소엔 숨어 있다가 스폰/특성 발동 순간에 적 머리 위로 튀어나왔다 사라진다.
// 스테이지 HUD 캔버스에 붙어서 매 프레임 월드 위치를 화면 좌표로 옮긴다.
public class EliteTraitPopup : MonoBehaviour
{
    private const float HeadHeight = 2.2f;
    private const float HoldTime = 0.9f;
    private const float RiseDistance = 40f;

    private RectTransform rect;
    private RectTransform canvasRect;
    private Canvas canvas;
    private CanvasGroup group;
    private Image badge;
    private TMP_Text label;
    private Sequence sequence;

    private Transform followTarget;
    private Vector3 worldPosition;
    private float rise;

    public static EliteTraitPopup Create()
    {
        Canvas hud = HudBanner.FindHudCanvas();
        if (hud == null) return null;

        CombatUiStyle style = CombatUiStyle.Get();

        GameObject root = new GameObject("EliteTraitPopup", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        root.transform.SetParent(hud.transform, false);
        root.transform.SetAsFirstSibling();  //다른 HUD(체력바, 버튼)보다 뒤에

        EliteTraitPopup popup = root.AddComponent<EliteTraitPopup>();
        popup.canvas = hud;
        popup.canvasRect = (RectTransform)hud.transform;
        popup.rect = (RectTransform)root.transform;
        popup.rect.anchorMin = popup.rect.anchorMax = new Vector2(0.5f, 0.5f);
        popup.rect.pivot = new Vector2(0.5f, 0f);

        popup.group = root.GetComponent<CanvasGroup>();
        popup.group.blocksRaycasts = false;
        popup.group.interactable = false;
        popup.group.alpha = 0f;

        // 글자 길이에 맞춰 늘어나는 알약형 배지
        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(34, 34, 12, 16);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        popup.badge = root.GetComponent<Image>();
        popup.badge.raycastTarget = false;
        if (style != null && style.badge != null)
        {
            popup.badge.sprite = style.badge;
            popup.badge.type = Image.Type.Sliced;
        }

        // 흰 테두리 + 아래 그림자로 배지가 배경에서 떠 보이게
        Outline rim = root.AddComponent<Outline>();
        rim.effectColor = Color.white;
        rim.effectDistance = new Vector2(4f, -4f);
        Shadow shadow = root.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
        shadow.effectDistance = new Vector2(0f, -8f);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(root.transform, false);

        TextMeshProUGUI text = labelObject.AddComponent<TextMeshProUGUI>();
        if (style != null && style.boldFont != null) text.font = style.boldFont;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 40f;
        text.color = Color.white;
        text.outlineWidth = 0.22f;
        text.outlineColor = new Color32(0, 0, 0, 160);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        HudBanner.ApplyShadow(text, new Color(0f, 0f, 0f, 0.5f));
        popup.label = text;

        root.SetActive(false);
        return popup;
    }

    // follow가 null이면 position에 고정 (죽는 순간 분열처럼 적이 곧 사라지는 경우)
    public void Show(string text, Color color, Transform follow, Vector3 position)
    {
        followTarget = follow;
        worldPosition = position;

        label.text = text;
        badge.color = color;

        gameObject.SetActive(true);
        rect.SetAsFirstSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        sequence?.Kill();
        rise = 0f;
        group.alpha = 0f;
        rect.localScale = Vector3.one * 0.3f;
        UpdatePosition();

        sequence = DOTween.Sequence()
            .Append(group.DOFade(1f, 0.1f))
            .Join(rect.DOScale(1f, 0.28f).SetEase(Ease.OutBack, 3f))
            .Join(DOTween.To(() => rise, value => rise = value, RiseDistance * 0.5f, 0.28f).SetEase(Ease.OutCubic))
            .AppendInterval(HoldTime)
            .Append(group.DOFade(0f, 0.25f))
            .Join(DOTween.To(() => rise, value => rise = value, RiseDistance, 0.25f))
            .OnComplete(() => gameObject.SetActive(false));
    }

    private void LateUpdate()
    {
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (followTarget != null && followTarget.gameObject.activeInHierarchy) worldPosition = followTarget.position;

        Camera worldCamera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        if (worldCamera == null) return;

        Vector3 screen = worldCamera.WorldToScreenPoint(worldPosition + Vector3.up * HeadHeight);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, worldCamera, out Vector2 local))
        {
            rect.anchoredPosition = local + Vector2.up * rise;
        }
    }

    private void OnDestroy()
    {
        sequence?.Kill();
    }
}
