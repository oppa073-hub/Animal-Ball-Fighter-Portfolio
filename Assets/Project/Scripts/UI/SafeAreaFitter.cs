using UnityEngine;

// 캔버스 바로 아래의 전체 화면 RectTransform에 붙인다.
// 노치/홈 인디케이터를 피한 안전 영역(Screen.safeArea) 안에 UI를 두고,
// 태블릿처럼 9:16보다 넓은 화면에서는 가운데 9:16 세로 기둥 안으로 UI 폭을 제한한다.
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    public const string ContainerName = "SafeArea";

    [SerializeField] private bool limitToReferenceAspect = true;

    private RectTransform rectTransform;
    private Rect previousSafeArea;
    private int previousWidth;
    private int previousHeight;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        if (Screen.safeArea != previousSafeArea || Screen.width != previousWidth || Screen.height != previousHeight)
        {
            Apply();
        }
    }

    private void Apply()
    {
        if (rectTransform == null) rectTransform = (RectTransform)transform;
        if (Screen.width <= 0 || Screen.height <= 0) return;

        previousSafeArea = Screen.safeArea;
        previousWidth = Screen.width;
        previousHeight = Screen.height;

        Rect area = Screen.safeArea;

        if (limitToReferenceAspect)
        {
            float maxWidth = Screen.height * PortraitCameraFit.ReferenceAspect;
            if (area.width > maxWidth)
            {
                area.x += (area.width - maxWidth) * 0.5f;
                area.width = maxWidth;
            }
        }

        rectTransform.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        rectTransform.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    // 런타임에 만드는 HUD를 붙일 부모 (캔버스 아래 SafeArea가 있으면 그것, 없으면 캔버스)
    public static Transform GetContentRoot(Canvas canvas)
    {
        if (canvas == null) return null;

        Transform safeArea = canvas.transform.Find(ContainerName);
        return safeArea != null ? safeArea : canvas.transform;
    }
}
