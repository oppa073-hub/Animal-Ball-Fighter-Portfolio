using UnityEngine;

// 세로 게임 카메라를 기기 화면 비율에 맞춘다 (레터박스 없이 화면 전체 사용).
// 카메라에 설정된 시야각은 기준 비율(9:16) 화면에서 보이는 범위다.
// - 9:16보다 길쭉한 폰(9:19.5, 9:21 등): 가로 범위를 기준과 같게 유지하고 위아래를 더 보여준다
// - 9:16보다 넓은 화면(태블릿 등): 세로 범위를 유지하고 좌우를 더 보여준다
[RequireComponent(typeof(Camera))]
public class PortraitCameraFit : MonoBehaviour
{
    public const float ReferenceAspect = 9f / 16f;

    private Camera targetCamera;
    private float referenceFov;
    private float referenceOrthoSize;
    private int previousWidth;
    private int previousHeight;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        referenceFov = targetCamera.fieldOfView;
        referenceOrthoSize = targetCamera.orthographicSize;
        targetCamera.rect = new Rect(0f, 0f, 1f, 1f);
    }

    private void OnEnable()
    {
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
        if (targetCamera == null || Screen.width <= 0 || Screen.height <= 0) return;

        previousWidth = Screen.width;
        previousHeight = Screen.height;

        float screenAspect = (float)Screen.width / Screen.height;
        float heightScale = screenAspect < ReferenceAspect ? ReferenceAspect / screenAspect : 1f;

        if (targetCamera.orthographic)
        {
            targetCamera.orthographicSize = referenceOrthoSize * heightScale;
        }
        else
        {
            float halfTan = Mathf.Tan(referenceFov * 0.5f * Mathf.Deg2Rad) * heightScale;
            targetCamera.fieldOfView = 2f * Mathf.Atan(halfTan) * Mathf.Rad2Deg;
        }
    }
}
