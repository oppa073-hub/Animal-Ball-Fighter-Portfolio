using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Canvas))]
public class LoadingCanvasCameraBinder : MonoBehaviour
{
    private Canvas loadingCanvas;

    private void Awake()
    {
        loadingCanvas = GetComponent<Canvas>();

        loadingCanvas.renderMode = RenderMode.ScreenSpaceCamera;

        SceneManager.sceneLoaded += HandleSceneLoaded;

        BindMainCamera();
    }

    private void LateUpdate()
    {
        // 씬 전환 중 기존 카메라가 삭제되면 새 카메라로 즉시 교체
        if (loadingCanvas.worldCamera == null)
        {
            BindMainCamera();
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindMainCamera();
    }

    private void BindMainCamera()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null) return;

        loadingCanvas.worldCamera = mainCamera;
        loadingCanvas.planeDistance = 0.5f;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }
}