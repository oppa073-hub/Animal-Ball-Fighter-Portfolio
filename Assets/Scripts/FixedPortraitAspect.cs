using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class FixedPortraitAspect : MonoBehaviour
{
    private const float TargetAspect = 9f / 16f;

    private Camera targetCamera;
    private int previousWidth;
    private int previousHeight;

    private void OnEnable()
    {
        targetCamera = GetComponent<Camera>();
        ApplyAspectRatio();
    }

    private void Update()
    {
        if (Screen.width != previousWidth || Screen.height != previousHeight)
        {
            ApplyAspectRatio();
        }
    }

    private void ApplyAspectRatio()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;

        previousWidth = Screen.width;
        previousHeight = Screen.height;

        float screenAspect = (float)Screen.width / Screen.height;
        Rect viewport = new Rect(0f, 0f, 1f, 1f);

        if (screenAspect > TargetAspect)
        {
            // 화면이 목표 비율보다 넓음: 좌우에 검은 여백
            float width = TargetAspect / screenAspect;

            viewport.width = width;
            viewport.x = (1f - width) * 0.5f;
        }
        else
        {
            // 화면이 목표 비율보다 길쭉함: 위아래에 검은 여백
            float height = screenAspect / TargetAspect;

            viewport.height = height;
            viewport.y = (1f - height) * 0.5f;
        }

        targetCamera.rect = viewport;
    }

    private void OnDisable()
    {
        if (targetCamera != null) targetCamera.rect = new Rect(0f, 0f, 1f, 1f);
    }
}