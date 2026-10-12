using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

// 튜토리얼 안내: 카툰풍 고양이가 통통 튀며 말풍선으로 한 줄씩 알려준다.
// - 막는 안내(blocking): 화면을 살짝 어둡게 하고 터치하면 다음 줄
// - 막지 않는 안내: 게임 조작은 그대로 되고, 시간이 지나거나 조건이 맞으면 넘어감 (드래그 손 시범 등)
// 화면 위(Screen Space Overlay, 정렬 500)에 그려 모든 HUD/팝업 위에 뜬다.
public class TutorialGuideUI : MonoBehaviour
{
    public class Request
    {
        public TutorialStep step;
        public string[] lineKeys;
        public string[] fallbacks;
        public bool blocking = true;
        public float anchorY = 0.62f;           //안내가 놓일 세로 위치 (안전 영역 기준 0~1)
        public RectTransform pointTarget;       //손가락으로 가리킬 UI (없으면 생략)
        public bool pointOnLastLineOnly = true;
        public bool dragDemo;                   //손이 끌었다 놓는 시범
        public Func<Vector2> dragFrom;          //시작 위치 (화면 비율 좌표, 매번 다시 계산: 플레이어 위치 등)
        public Vector2 dragDelta = new Vector2(0f, -0.14f);
        public bool holdDemo;                   //손이 꾹 누르는 시범 (대쉬 영역)
        public Vector2 holdAt = new Vector2(0.3f, 0.2f);
        public float autoAdvanceSeconds = 0f;   //막지 않는 안내: 줄마다 이 시간 뒤 넘어감 (0이면 마지막 줄은 조건까지 유지)
        public Func<bool> completeWhen;         //막지 않는 안내: 이 조건이 참이 되면 끝
        public bool holdLastLine;               //autoAdvanceSeconds가 있어도 마지막 줄은 completeWhen까지 유지 (전투 튜토리얼 강제 단계)
        public Action onFinished;
    }

    private const float TypeSpeed = 38f;  //초당 글자 수

    private static TutorialGuideUI instance;

    public static bool IsShowing => instance != null && instance.current != null;

    private TutorialGuideConfig config;
    private Canvas canvas;
    private CanvasGroup group;
    private Image dim;
    private RectTransform guideRoot;
    private RectTransform catRoot;
    private RectTransform bubble;
    private TMP_Text bubbleText;
    private TMP_Text continueHint;
    private RectTransform hand;
    private RectTransform ring;
    private RectTransform overlayRect;

    private Request current;
    private int lineIndex;
    private float lineTimer;
    private bool typing;
    private float typeProgress;
    private Coroutine demoRoutine;

    public static void Play(Request request)
    {
        if (request == null || request.lineKeys == null || request.lineKeys.Length == 0) return;

        if (instance == null)
        {
            TutorialGuideConfig loaded = TutorialGuideConfig.Load();
            if (loaded == null)
            {
                Debug.LogWarning("[Tutorial] Resources/TutorialGuideConfig 가 없어 튜토리얼을 건너뜁니다.");
                return;
            }

            instance = new GameObject("TutorialGuideUI").AddComponent<TutorialGuideUI>();
            instance.config = loaded;
            instance.Build();
        }

        instance.Begin(request);
    }

    public static void Stop()
    {
        if (instance != null) instance.Finish(false);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        gameObject.AddComponent<AdaptiveCanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        group = gameObject.AddComponent<CanvasGroup>();
        overlayRect = (RectTransform)transform;

        // 어둡게 + 터치 받기
        dim = CreateImage("Dim", transform, null, new Color(0.02f, 0.04f, 0.1f, 0.55f));
        Stretch(dim.rectTransform);
        Button dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(HandleTap);

        // 강조 고리 + 손
        ring = CreateImage("HighlightRing", transform, config.ringSprite, new Color(1f, 0.85f, 0.3f, 1f)).rectTransform;
        ring.GetComponent<Image>().raycastTarget = false;
        ring.gameObject.SetActive(false);

        hand = CreateImage("Hand", transform, config.handSprite, Color.white).rectTransform;
        hand.sizeDelta = new Vector2(150f, 150f);
        hand.pivot = new Vector2(0.9f, 0.12f);  //손끝 (그림의 오른쪽 아래)
        hand.GetComponent<Image>().raycastTarget = false;
        hand.GetComponent<Image>().preserveAspect = true;
        hand.gameObject.SetActive(false);

        // 안전 영역 안의 안내 묶음
        RectTransform safe = new GameObject(SafeAreaFitter.ContainerName, typeof(RectTransform)).GetComponent<RectTransform>();
        safe.SetParent(transform, false);
        Stretch(safe);
        safe.gameObject.AddComponent<SafeAreaFitter>();

        guideRoot = new GameObject("Guide", typeof(RectTransform)).GetComponent<RectTransform>();
        guideRoot.SetParent(safe, false);
        guideRoot.sizeDelta = new Vector2(1000f, 320f);

        // 고양이 (흰 테두리 원 + 원형 마스크 + 얼굴)
        catRoot = new GameObject("Cat", typeof(RectTransform)).GetComponent<RectTransform>();
        catRoot.SetParent(guideRoot, false);
        catRoot.anchorMin = catRoot.anchorMax = new Vector2(0f, 0.5f);
        catRoot.anchoredPosition = new Vector2(130f, -10f);
        catRoot.sizeDelta = new Vector2(230f, 230f);

        Image shadow = CreateImage("Shadow", catRoot, config.circleSprite, new Color(0f, 0f, 0f, 0.25f));
        Stretch(shadow.rectTransform);
        shadow.rectTransform.anchoredPosition = new Vector2(6f, -10f);
        shadow.raycastTarget = false;

        Image border = CreateImage("Border", catRoot, config.circleSprite, Color.white);
        Stretch(border.rectTransform);
        border.raycastTarget = false;

        Image mask = CreateImage("Mask", border.transform, config.circleSprite, Color.white);
        Stretch(mask.rectTransform);
        mask.rectTransform.offsetMin = new Vector2(9f, 9f);
        mask.rectTransform.offsetMax = new Vector2(-9f, -9f);
        mask.raycastTarget = false;
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        Image face = CreateImage("Face", mask.transform, config.catSprite, Color.white);
        Stretch(face.rectTransform);
        face.preserveAspect = true;
        face.raycastTarget = false;

        // 말풍선 (왼쪽 꼬리가 고양이를 향함)
        Image bubbleImage = CreateImage("Bubble", guideRoot, config.bubbleSprite, Color.white);
        bubbleImage.type = Image.Type.Sliced;
        bubbleImage.pixelsPerUnitMultiplier = 0.6f;  //원본 스프라이트가 작아서 테두리를 키움
        bubbleImage.raycastTarget = false;
        bubble = bubbleImage.rectTransform;
        bubble.anchorMin = bubble.anchorMax = new Vector2(0f, 0.5f);
        bubble.pivot = new Vector2(0f, 0.5f);
        bubble.anchoredPosition = new Vector2(240f, 0f);
        bubble.sizeDelta = new Vector2(740f, 300f);

        bubbleText = CreateText("Text", bubble, 44f, config.textColor, TextAlignmentOptions.MidlineLeft);
        RectTransform textRect = bubbleText.rectTransform;
        Stretch(textRect);
        textRect.offsetMin = new Vector2(90f, 50f);   //왼쪽 꼬리 + 여백
        textRect.offsetMax = new Vector2(-50f, -40f);
        bubbleText.enableAutoSizing = true;
        bubbleText.fontSizeMin = 30f;
        bubbleText.fontSizeMax = 46f;
        bubbleText.lineSpacing = 8f;

        continueHint = CreateText("Continue", bubble, 28f, new Color(0.35f, 0.55f, 0.95f), TextAlignmentOptions.BottomRight);
        RectTransform hintRect = continueHint.rectTransform;
        hintRect.anchorMin = hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(1f, 0f);
        hintRect.anchoredPosition = new Vector2(-40f, 26f);
        hintRect.sizeDelta = new Vector2(320f, 40f);
        continueHint.text = "▼";

        gameObject.SetActive(false);
    }

    private void Begin(Request request)
    {
        if (current != null) Finish(false);

        current = request;
        lineIndex = 0;

        gameObject.SetActive(true);  //씬 오브젝트라 씬이 바뀌면 함께 사라짐

        dim.gameObject.SetActive(request.blocking);
        group.blocksRaycasts = request.blocking;

        guideRoot.anchorMin = guideRoot.anchorMax = new Vector2(0.5f, request.anchorY);
        guideRoot.anchoredPosition = Vector2.zero;

        // 등장: 아래에서 통 튀어오름
        guideRoot.DOKill();
        guideRoot.localScale = Vector3.one * 0.6f;
        guideRoot.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
        group.alpha = 0f;
        group.DOKill();
        group.DOFade(1f, 0.2f).SetUpdate(true);

        ShowLine();
    }

    private void ShowLine()
    {
        string key = current.lineKeys[lineIndex];
        string fallback = current.fallbacks != null && lineIndex < current.fallbacks.Length ? current.fallbacks[lineIndex] : key;

        string text = fallback;
        if (!string.IsNullOrEmpty(key))  //키가 없으면 fallback 그대로 (미리 번역해 숫자를 넣은 줄 등)
        try
        {
            string localized = LocalizationSettings.StringDatabase.GetLocalizedString("UI", key);
            if (!string.IsNullOrEmpty(localized)) text = localized;
        }
        catch (Exception)
        {
            // Localization 준비 전이면 기본 문구
        }

        bubbleText.text = text;
        bubbleText.maxVisibleCharacters = 0;
        typing = true;
        typeProgress = 0f;
        lineTimer = 0f;

        continueHint.gameObject.SetActive(false);

        // 말풍선 톡 튀기
        bubble.DOKill();
        bubble.localScale = new Vector3(0.9f, 0.9f, 1f);
        bubble.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);

        SoundManager.Instance?.PlaySFX(SoundId.UI_Click);

        UpdatePointer();
    }

    private void UpdatePointer()
    {
        if (demoRoutine != null)
        {
            StopCoroutine(demoRoutine);
            demoRoutine = null;
        }

        hand.DOKill();
        ring.DOKill();
        hand.gameObject.SetActive(false);
        ring.gameObject.SetActive(false);

        bool isLast = lineIndex == current.lineKeys.Length - 1;

        if (current.pointTarget != null && (!current.pointOnLastLineOnly || isLast))
        {
            demoRoutine = StartCoroutine(PointRoutine(current.pointTarget));
        }
        else if (current.dragDemo && isLast)
        {
            demoRoutine = StartCoroutine(DragDemoRoutine());
        }
        else if (current.holdDemo && isLast)
        {
            demoRoutine = StartCoroutine(HoldDemoRoutine());
        }
    }

    private void Update()
    {
        if (current == null) return;

        float deltaTime = Time.unscaledDeltaTime;

        // 타자 치듯 한 글자씩
        if (typing)
        {
            typeProgress += TypeSpeed * deltaTime;
            int visible = Mathf.Min(bubbleText.text.Length, Mathf.FloorToInt(typeProgress));
            bubbleText.maxVisibleCharacters = visible;

            if (visible >= bubbleText.text.Length) EndTyping();
        }

        // 고양이: 통통 튀고, 말하는 동안은 입을 오물거리듯 찌그러짐
        float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.2f)) * 14f;
        catRoot.anchoredPosition = new Vector2(130f, -10f + bob);
        float talk = typing ? 1f + Mathf.Sin(Time.unscaledTime * 22f) * 0.05f : 1f;
        catRoot.localScale = new Vector3(1f / talk, talk, 1f);
        catRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 1.6f) * 4f);

        if (continueHint.gameObject.activeSelf)
        {
            float blink = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            continueHint.alpha = 0.4f + 0.6f * blink;
        }

        if (!current.blocking)
        {
            lineTimer += deltaTime;
            bool isLast = lineIndex == current.lineKeys.Length - 1;

            if (current.completeWhen != null && current.completeWhen())
            {
                Finish(true);
                return;
            }

            float advance = current.autoAdvanceSeconds > 0f ? current.autoAdvanceSeconds : 3.5f;
            if (!typing && lineTimer >= advance + bubbleText.text.Length / TypeSpeed)
            {
                if (!isLast) NextLine();
                else if ((current.autoAdvanceSeconds > 0f && !current.holdLastLine) || current.completeWhen == null) Finish(true);
            }
        }
    }

    private void EndTyping()
    {
        typing = false;
        bubbleText.maxVisibleCharacters = bubbleText.text.Length;

        if (current != null && current.blocking) continueHint.gameObject.SetActive(true);
    }

    private void HandleTap()
    {
        if (current == null || !current.blocking) return;

        if (typing)
        {
            EndTyping();  //빨리 보기
            return;
        }

        NextLine();
    }

    private void NextLine()
    {
        lineIndex++;
        if (lineIndex >= current.lineKeys.Length)
        {
            Finish(true);
            return;
        }

        ShowLine();
    }

    private void Finish(bool completed)
    {
        if (current == null) return;

        Request finished = current;
        current = null;

        if (demoRoutine != null)
        {
            StopCoroutine(demoRoutine);
            demoRoutine = null;
        }

        hand.DOKill();
        ring.DOKill();
        guideRoot.DOKill();
        group.DOKill();
        gameObject.SetActive(false);

        if (completed)
        {
            if (finished.step != TutorialStep.None) TutorialProgress.MarkDone(finished.step);
            finished.onFinished?.Invoke();
        }
    }

    // ── 손가락/강조 ──

    private IEnumerator PointRoutine(RectTransform target)
    {
        ring.gameObject.SetActive(true);
        hand.gameObject.SetActive(true);

        while (target != null && target.gameObject.activeInHierarchy)
        {
            Vector2 center = TargetToOverlay(target, out Vector2 size);
            float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f);

            ring.anchoredPosition = center;
            float ringSize = Mathf.Max(size.x, size.y) + 60f;
            ring.sizeDelta = new Vector2(ringSize, ringSize) * pulse;

            // 손끝이 대상 가운데를 톡톡 두드림 (손은 왼쪽 위에서 뻗어 옴 → 화면 가장자리 버튼도 손이 잘리지 않음)
            float tap = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
            hand.anchoredPosition = center + new Vector2(-12f, 12f) * tap;
            hand.localRotation = Quaternion.identity;

            yield return null;
        }

        ring.gameObject.SetActive(false);
        hand.gameObject.SetActive(false);
    }

    private IEnumerator DragDemoRoutine()
    {
        Image handImage = hand.GetComponent<Image>();
        hand.gameObject.SetActive(true);
        hand.localRotation = Quaternion.identity;

        while (true)
        {
            Vector2 fromViewport = current.dragFrom != null ? current.dragFrom() : new Vector2(0.5f, 0.3f);
            Vector2 from = ViewportToOverlay(fromViewport);
            Vector2 to = ViewportToOverlay(fromViewport + current.dragDelta);

            hand.anchoredPosition = from;
            handImage.color = new Color(1f, 1f, 1f, 0f);
            yield return Fade(handImage, 1f, 0.2f);
            yield return Wait(0.2f);

            // 꾹 누르고 아래로 끌기
            float timer = 0f;
            while (timer < 0.8f)
            {
                float t = timer / 0.8f;
                hand.anchoredPosition = Vector2.Lerp(from, to, 1f - (1f - t) * (1f - t));
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            // 놓기 (튕겨 나가는 느낌)
            hand.DOPunchScale(Vector3.one * -0.15f, 0.25f, 6).SetUpdate(true);
            yield return Wait(0.3f);
            yield return Fade(handImage, 0f, 0.25f);
            yield return Wait(0.4f);
        }
    }

    private IEnumerator HoldDemoRoutine()
    {
        Image handImage = hand.GetComponent<Image>();
        hand.gameObject.SetActive(true);
        ring.gameObject.SetActive(true);
        hand.localRotation = Quaternion.identity;
        handImage.color = Color.white;

        while (true)
        {
            Vector2 at = ViewportToOverlay(current.holdAt);
            hand.anchoredPosition = at;
            ring.anchoredPosition = at;

            // 꾹 누르면 고리가 줄어들며 차오름
            float timer = 0f;
            while (timer < 0.9f)
            {
                float t = timer / 0.9f;
                ring.sizeDelta = Vector2.one * Mathf.Lerp(260f, 120f, t);
                hand.localScale = Vector3.one * Mathf.Lerp(1f, 0.88f, Mathf.Min(1f, t * 4f));
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            // 끌어서 방향 정하기
            Vector2 drag = at + new Vector2(140f, 160f);
            timer = 0f;
            while (timer < 0.5f)
            {
                hand.anchoredPosition = Vector2.Lerp(at, drag, timer / 0.5f);
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            hand.localScale = Vector3.one;
            yield return Wait(0.6f);
        }
    }

    private IEnumerator Fade(Image image, float to, float duration)
    {
        float from = image.color.a;
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            Color color = image.color;
            color.a = Mathf.Lerp(from, to, timer / duration);
            image.color = color;
            yield return null;
        }
    }

    private static IEnumerator Wait(float seconds)
    {
        float timer = 0f;
        while (timer < seconds)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private Vector2 TargetToOverlay(RectTransform target, out Vector2 size)
    {
        Canvas targetCanvas = target.GetComponentInParent<Canvas>();
        Camera targetCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? targetCanvas.rootCanvas.worldCamera : null;

        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 4; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(targetCamera, corners[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, screen, null, out Vector2 local);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        size = max - min;
        return (min + max) * 0.5f;
    }

    private Vector2 ViewportToOverlay(Vector2 viewport)
    {
        Rect rect = overlayRect.rect;
        return new Vector2((viewport.x - 0.5f) * rect.width, (viewport.y - 0.5f) * rect.height);
    }

    // ── 생성 도우미 ──

    private Image CreateImage(string objectName, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    private TMP_Text CreateText(string objectName, Transform parent, float size, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (config.font != null) text.font = config.font;
        if (config.fontMaterial != null) text.fontSharedMaterial = config.fontMaterial;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
