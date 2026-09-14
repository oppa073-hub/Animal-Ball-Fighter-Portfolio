using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager Instance { get; private set; }

    [Header("Initial Scene")]
    [SerializeField] private string initialSceneName = "MainLobby";

    [Header("Loading UI")]
    [SerializeField] private GameObject loadingCanvas;
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Transform loadingIcon;

    [Header("Loading Settings")]
    [SerializeField] private float minimumLoadingTime = 0.7f;
    [SerializeField] private float postLoadDelay = 0.15f;
    [SerializeField] private float iconRotationSpeed = 180f;
    [SerializeField] private float sceneReadyTimeout = 10f;
    private bool sceneReady;

    [SerializeField] private CanvasGroup loadingCanvasGroup;
    [SerializeField] private float fadeOutDuration = 0.25f;

    private bool isLoading;
    public bool IsLoading => isLoading;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        loadingCanvas.SetActive(true);
        SetProgress(0f);
    }

    private void Start()
    {
        LoadScene(initialSceneName);
    }

    private void Update()
    {
        if (!isLoading || loadingIcon == null) return;

        loadingIcon.Rotate(
            0f,
            0f,
            -iconRotationSpeed * Time.unscaledDeltaTime
        );
    }

    public void LoadScene(string sceneName, bool waitForSceneReady = false)
    {
        if (isLoading)
        {
            Debug.LogWarning("[Loading] 이미 씬을 로드하고 있습니다.");
            return;
        }

        StartCoroutine(LoadSceneRoutine(sceneName, waitForSceneReady));
    }

    private IEnumerator LoadSceneRoutine(string sceneName, bool waitForSceneReady)
    {
        loadingCanvas.SetActive(true);

        if (loadingCanvasGroup != null)
        {
            loadingCanvasGroup.alpha = 1f;
            loadingCanvasGroup.blocksRaycasts = true;
        }

        isLoading = true;
        loadingCanvas.SetActive(true);
        SetProgress(0f);

        float loadingStartTime = Time.unscaledTime;

        // 로딩 화면이 먼저 한 프레임 그려지도록 대기
        yield return null;

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        if (operation == null)
        {
            Debug.LogError($"[Loading] 씬 로드 실패: {sceneName}");

            loadingCanvas.SetActive(false);
            isLoading = false;
            yield break;
        }

        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);

            float displayedProgress = waitForSceneReady
                    ? progress * 0.9f
                    : progress;

            SetProgress(displayedProgress);

            yield return null;
        }

        SetProgress(waitForSceneReady ? 0.9f : 1f);

        while (Time.unscaledTime - loadingStartTime < minimumLoadingTime)
        {
            yield return null;
        }

        operation.allowSceneActivation = true;

        while (!operation.isDone)
        {
            yield return null;
        }
        if (waitForSceneReady)
        {
            SetProgress(0.95f);

            float readyWaitStartTime = Time.unscaledTime;

            while (!sceneReady && Time.unscaledTime - readyWaitStartTime < sceneReadyTimeout)
            {
                yield return null;
            }

            if (!sceneReady)
            {
                Debug.LogWarning("[Loading] 씬 준비 완료 신호를 기다리다 시간이 초과되었습니다.");
            }
        }

        SetProgress(1f);

        // 새 씬의 Awake와 Start가 실행되는 동안 화면 유지
        yield return new WaitForSecondsRealtime(postLoadDelay);
        yield return FadeOutLoadingScreen();

        loadingCanvas.SetActive(false);
        isLoading = false;
        sceneReady = false;

        Debug.Log($"[Loading] 씬 로드 완료: {sceneName}");
    }

    private void SetProgress(float progress)
    {
        if (progressFill != null)
        {
            progressFill.fillAmount = progress;
        }

        if (progressText != null)
        {
            progressText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
        }
    }
    public void NotifySceneReady()
    {
        sceneReady = true;

        Debug.Log("[Loading] 씬 준비 완료 신호 수신");
    }
    private IEnumerator FadeOutLoadingScreen()
    {
        if (loadingCanvasGroup == null || fadeOutDuration <= 0f)
        {
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < fadeOutDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;

            float ratio = Mathf.Clamp01(elapsedTime / fadeOutDuration);
            loadingCanvasGroup.alpha = 1f - ratio;

            yield return null;
        }

        loadingCanvasGroup.alpha = 0f;
        loadingCanvasGroup.blocksRaycasts = false;
    }
}