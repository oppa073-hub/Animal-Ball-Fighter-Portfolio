using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
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
    [SerializeField] private Image loadingIconCharacterImage;  //아이콘 테두리(원형 마스크) 안의 캐릭터 이미지. 비어 있으면 loadingIcon의 Image를 쓴다

    [Header("Character Icon")]
    [SerializeField] private CharacterData[] characterIconSources;  //로비의 CharacterDatabase가 아직 없는 첫 로딩용
    private Image loadingIconImage;
    private float iconBaseY;

    [Header("Loading Settings")]
    [SerializeField] private float minimumLoadingTime = 0.7f;
    [SerializeField] private float postLoadDelay = 0.15f;
    [SerializeField] private float iconRotationSpeed = 180f;
    [SerializeField] private float sceneReadyTimeout = 10f;
    private bool sceneReady;

    [SerializeField] private CanvasGroup loadingCanvasGroup;
    [SerializeField] private float fadeOutDuration = 0.25f;
    [SerializeField] private float fadeInDuration = 0.2f;

    [Header("Progress Bar")]
    [SerializeField] private float progressFollowSpeed = 2.5f;  //표시 진행도가 실제 진행도를 따라가는 속도 (초당 비율)

    [Header("Icon Bounce")]
    [SerializeField] private float iconBounceHeight = 40f;
    [SerializeField] private float iconBounceFrequency = 2.2f;  //초당 튀는 횟수

    [Header("Background")]
    [SerializeField] private RectTransform backgroundBlur;
    [SerializeField] private RectTransform backgroundArt;
    [SerializeField] private float backgroundBlurZoom = 0.08f;
    [SerializeField] private float backgroundArtZoom = 0.025f;
    [SerializeField] private float backgroundZoomPeriod = 12f;

    [Header("Tips")]
    [SerializeField] private TMP_Text tipText;
    [SerializeField] private string tipTableName = "UI";
    [SerializeField] private string[] tipKeys;
    private int lastTipIndex = -1;

    private float targetProgress;
    private float displayedProgress;
    private float loadingVisibleTime;

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

        loadingIconImage = loadingIconCharacterImage;
        if (loadingIconImage == null && loadingIcon != null) loadingIconImage = loadingIcon.GetComponent<Image>();
        if (loadingIcon is RectTransform iconRect) iconBaseY = iconRect.anchoredPosition.y;

        loadingCanvas.SetActive(true);
        SetProgress(0f, true);
        RefreshLoadingIcon();
    }

    private void Start()
    {
        LoadScene(initialSceneName);
    }

    private void Update()
    {
        if (!isLoading) return;

        loadingVisibleTime += Time.unscaledDeltaTime;

        displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, progressFollowSpeed * Time.unscaledDeltaTime);
        ApplyProgressVisual(displayedProgress);

        UpdateLoadingIcon();
        UpdateBackgroundZoom();
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
        bool wasHidden = !loadingCanvas.activeSelf || (loadingCanvasGroup != null && loadingCanvasGroup.alpha < 0.99f);

        loadingCanvas.SetActive(true);

        if (loadingCanvasGroup != null)
        {
            loadingCanvasGroup.blocksRaycasts = true;
            if (!wasHidden) loadingCanvasGroup.alpha = 1f;
        }

        isLoading = true;
        loadingVisibleTime = 0f;
        SetProgress(0f, true);
        RefreshLoadingIcon();
        ShowRandomTip();
        UpdateBackgroundZoom();

        float loadingStartTime = Time.unscaledTime;

        if (wasHidden)
        {
            yield return FadeInLoadingScreen();
        }
        else
        {
            // 로딩 화면이 먼저 한 프레임 그려지도록 대기
            yield return null;
        }

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

            float displayedTarget = waitForSceneReady
                    ? progress * 0.9f
                    : progress;

            SetProgress(displayedTarget);

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

        // 바가 끝까지 차오르는 모습을 보여준 뒤 넘어간다 (최대 0.4초)
        float catchUpStartTime = Time.unscaledTime;
        while (displayedProgress < 0.999f && Time.unscaledTime - catchUpStartTime < 0.4f)
        {
            yield return null;
        }
        SetProgress(1f, true);

        // 새 씬의 Awake와 Start가 실행되는 동안 화면 유지
        yield return new WaitForSecondsRealtime(postLoadDelay);
        yield return FadeOutLoadingScreen();

        loadingCanvas.SetActive(false);
        isLoading = false;
        sceneReady = false;

        Debug.Log($"[Loading] 씬 로드 완료: {sceneName}");
    }

    private void SetProgress(float progress, bool immediate = false)
    {
        targetProgress = Mathf.Clamp01(progress);

        if (immediate)
        {
            displayedProgress = targetProgress;
            ApplyProgressVisual(displayedProgress);
        }
    }

    private void ApplyProgressVisual(float progress)
    {
        if (progressFill != null)
        {
            if (progressFill.type == Image.Type.Filled)
            {
                progressFill.fillAmount = progress;
            }
            else
            {
                // 둥근(Sliced) 바는 fillAmount 대신 폭을 줄여서 모서리가 찌그러지지 않게 한다
                RectTransform fillRect = progressFill.rectTransform;
                fillRect.anchorMax = new Vector2(progress, fillRect.anchorMax.y);
            }
        }

        if (progressText != null)
        {
            progressText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
        }
    }

    private void UpdateLoadingIcon()
    {
        if (loadingIcon == null) return;

        loadingIcon.Rotate(0f, 0f, -iconRotationSpeed * Time.unscaledDeltaTime);

        // 아이콘이 진행 바 위에 있으면 바 끝을 따라가며 통통 튄다
        if (loadingIcon is RectTransform iconRect && progressFill != null && iconRect.parent == progressFill.rectTransform.parent)
        {
            float bounce = Mathf.Abs(Mathf.Sin(loadingVisibleTime * Mathf.PI * iconBounceFrequency));
            iconRect.anchorMin = new Vector2(displayedProgress, iconRect.anchorMin.y);
            iconRect.anchorMax = new Vector2(displayedProgress, iconRect.anchorMax.y);
            iconRect.anchoredPosition = new Vector2(0f, iconBaseY + bounce * iconBounceHeight);
        }
    }

    private void UpdateBackgroundZoom()
    {
        // 배경을 아주 천천히 확대/축소해서 정지 화면 느낌을 줄인다
        float wave = 0.5f - 0.5f * Mathf.Cos(loadingVisibleTime * Mathf.PI * 2f / Mathf.Max(0.1f, backgroundZoomPeriod));

        if (backgroundBlur != null) backgroundBlur.localScale = Vector3.one * (1f + backgroundBlurZoom * wave);
        if (backgroundArt != null) backgroundArt.localScale = Vector3.one * (1f + backgroundArtZoom * wave);
    }

    private void ShowRandomTip()
    {
        if (tipText == null) return;

        if (tipKeys == null || tipKeys.Length == 0)
        {
            tipText.text = "";
            return;
        }

        int index = Random.Range(0, tipKeys.Length);
        if (tipKeys.Length > 1 && index == lastTipIndex) index = (index + 1) % tipKeys.Length;
        lastTipIndex = index;

        string key = tipKeys[index];
        tipText.text = "";

        // 첫 로딩에서는 Localization 초기화가 끝나지 않았을 수 있어서 비동기로 받아 온다
        var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tipTableName, key);
        if (handle.IsDone)
        {
            tipText.text = handle.Result;
        }
        else
        {
            handle.Completed += op =>
            {
                if (tipText != null && lastTipIndex == index) tipText.text = op.Result;
            };
        }
    }

    private void RefreshLoadingIcon()  //로딩 아이콘을 현재 선택된 캐릭터 아이콘으로 (세이브가 없으면 기본 캐릭터 고양이)
    {
        if (loadingIconImage == null) return;

        CharacterId selectedId = SaveManager.Instance != null
            ? SaveManager.Instance.Data.selectedCharacterId
            : SaveManager.PeekSelectedCharacterId();

        Sprite icon = FindCharacterIcon(selectedId);
        if (icon == null) icon = FindCharacterIcon(CharacterId.Cat);

        if (icon != null) loadingIconImage.sprite = icon;
    }

    private Sprite FindCharacterIcon(CharacterId id)
    {
        if (CharacterDatabase.Instance != null && CharacterDatabase.Instance.TryGetCharacter(id, out CharacterData databaseData) && databaseData.characterIcon != null)
        {
            return databaseData.characterIcon;
        }

        if (characterIconSources == null) return null;

        for (int i = 0; i < characterIconSources.Length; i++)
        {
            CharacterData data = characterIconSources[i];
            if (data != null && data.characterId == id) return data.characterIcon;
        }

        return null;
    }

    public void NotifySceneReady()
    {
        sceneReady = true;

        Debug.Log("[Loading] 씬 준비 완료 신호 수신");
    }

    private IEnumerator FadeInLoadingScreen()
    {
        if (loadingCanvasGroup == null || fadeInDuration <= 0f)
        {
            if (loadingCanvasGroup != null) loadingCanvasGroup.alpha = 1f;
            yield break;
        }

        float startAlpha = loadingCanvasGroup.alpha;
        float elapsedTime = 0f;

        while (elapsedTime < fadeInDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;

            float ratio = Mathf.Clamp01(elapsedTime / fadeInDuration);
            loadingCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, ratio);

            yield return null;
        }

        loadingCanvasGroup.alpha = 1f;
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
