using UnityEngine;
using UnityEngine.UI;

// 기준 해상도(1080x1920) UI가 어떤 비율의 화면에서도 잘리지 않게 CanvasScaler 기준 축을 바꾼다.
// - 9:16보다 길쭉한 화면: 가로 폭에 맞춤 (캔버스 1080 x 1920 이상)
// - 9:16보다 넓은 화면: 세로 높이에 맞춤 (캔버스 1080 이상 x 1920)
// 결과적으로 기준 1080x1920 영역은 항상 화면 안에 다 들어간다.
[RequireComponent(typeof(CanvasScaler))]
public class AdaptiveCanvasScaler : MonoBehaviour
{
    private CanvasScaler scaler;
    private int previousWidth;
    private int previousHeight;

    private void OnEnable()
    {
        scaler = GetComponent<CanvasScaler>();
        Apply();
    }

    private void Update()
    {
        if (Screen.width != previousWidth || Screen.height != previousHeight)
        {
            Apply();
        }
    }

    private void Apply()
    {
        if (scaler == null || Screen.width <= 0 || Screen.height <= 0) return;

        previousWidth = Screen.width;
        previousHeight = Screen.height;

        Vector2 reference = scaler.referenceResolution;
        float referenceAspect = reference.x / reference.y;
        float screenAspect = (float)Screen.width / Screen.height;

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = screenAspect < referenceAspect ? 0f : 1f;
    }
}
