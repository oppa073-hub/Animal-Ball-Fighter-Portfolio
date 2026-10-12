using UnityEngine;
using UnityEngine.UI;

// 산데비스탄 화면 연출: 청록색 톤 + 주사선 + 지지직거리는 글리치 띠. 실제 시간(unscaled) 기준으로 움직인다.
// 메인 카메라의 Screen Space Camera 캔버스로 HUD(정렬 0)보다 아래에 깔아 버튼은 가리지 않고, 화면 비율 고정 영역 안에만 그린다.
public class SandevistanScreenEffect : MonoBehaviour
{
    private const int GlitchBarCount = 7;

    private static readonly Color DefaultTintColor = new Color(0f, 0.85f, 0.8f, 1f);  //플레이어 산데비스탄 (청록)
    public static readonly Color BossTintColor = new Color(1f, 0.15f, 0.45f, 1f);  //보스 산데비스탄 (붉은 자홍)

    private Color tintColor = DefaultTintColor;
    private static readonly Color[] BarColors =
    {
        new Color(0.3f, 1f, 0.95f, 1f),
        new Color(1f, 1f, 1f, 1f),
        new Color(1f, 0.25f, 0.8f, 1f),
    };

    private Canvas canvas;
    private Image tintImage;
    private RawImage scanlineImage;
    private Texture2D scanlineTexture;
    private RectTransform canvasRect;
    private readonly Image[] glitchBars = new Image[GlitchBarCount];

    private float intensity;        //현재 연출 세기 (0~1)
    private float targetIntensity;  //목표 세기
    private float burstTimer;       //시작/종료 순간 강한 글리치
    private float glitchTimer;
    private float fadeSpeed = 4f;

    public static SandevistanScreenEffect Create()
    {
        return Create(DefaultTintColor);
    }

    public static SandevistanScreenEffect Create(Color tint)
    {
        GameObject root = new GameObject("SandevistanScreenEffect", typeof(RectTransform));
        SandevistanScreenEffect effect = root.AddComponent<SandevistanScreenEffect>();
        effect.tintColor = tint;
        effect.Build();
        return effect;
    }

    public bool IsShowing => canvas != null && canvas.gameObject.activeSelf && targetIntensity > 0f;

    public void Burst(float duration)  //순간 강한 글리치 (산데비스탄끼리 겹칠 때 등)
    {
        if (canvas == null) return;

        burstTimer = Mathf.Max(burstTimer, duration);
        canvas.gameObject.SetActive(true);
    }

    public void Show(float fadeInTime)
    {
        targetIntensity = 1f;
        fadeSpeed = 1f / Mathf.Max(0.05f, fadeInTime);
        burstTimer = 0.25f;
        canvas.gameObject.SetActive(true);
    }

    public void Hide(float fadeOutTime)
    {
        if (targetIntensity <= 0f) return;

        targetIntensity = 0f;
        fadeSpeed = 1f / Mathf.Max(0.05f, fadeOutTime);
        burstTimer = 0.15f;
    }

    public void HideImmediate()
    {
        targetIntensity = 0f;
        intensity = 0f;
        burstTimer = 0f;
        ApplyVisuals();
        canvas.gameObject.SetActive(false);
    }

    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = 0.45f;  //HUD(0.5)보다 카메라에 가깝지만 정렬 순서로 HUD 아래에 그림
        canvas.sortingOrder = -1;
        canvasRect = (RectTransform)transform;

        tintImage = CreateImage("Tint");
        tintImage.color = Color.clear;

        GameObject scanlineObject = new GameObject("Scanlines", typeof(RectTransform));
        scanlineObject.transform.SetParent(transform, false);
        scanlineImage = scanlineObject.AddComponent<RawImage>();
        scanlineTexture = CreateScanlineTexture();
        scanlineImage.texture = scanlineTexture;
        scanlineImage.raycastTarget = false;
        Stretch(scanlineImage.rectTransform);

        for (int i = 0; i < GlitchBarCount; i++)
        {
            glitchBars[i] = CreateImage("GlitchBar");
            RectTransform rect = glitchBars[i].rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            glitchBars[i].enabled = false;
        }

        canvas.gameObject.SetActive(false);
    }

    private Image CreateImage(string objectName)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform));
        imageObject.transform.SetParent(transform, false);

        Image image = imageObject.AddComponent<Image>();
        image.raycastTarget = false;
        Stretch(image.rectTransform);
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Texture2D CreateScanlineTexture()  //가로 줄무늬 (위 2줄 어둡게, 아래 2줄 투명)
    {
        Texture2D texture = new Texture2D(1, 4, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Point;
        texture.SetPixels(new[] { Color.clear, Color.clear, new Color(0f, 0.1f, 0.12f, 1f), new Color(0f, 0.1f, 0.12f, 1f) });
        texture.Apply();
        return texture;
    }

    private void Update()
    {
        if (canvas == null || !canvas.gameObject.activeSelf) return;
        if (Time.timeScale <= 0f) return;  //일시정지 중에는 화면을 그대로 둠

        float deltaTime = Time.unscaledDeltaTime;

        if (canvas.worldCamera == null) canvas.worldCamera = Camera.main;

        intensity = Mathf.MoveTowards(intensity, targetIntensity, fadeSpeed * deltaTime);
        burstTimer = Mathf.Max(0f, burstTimer - deltaTime);

        ApplyVisuals();
        UpdateGlitch(deltaTime);

        if (intensity <= 0f && targetIntensity <= 0f && burstTimer <= 0f)
        {
            canvas.gameObject.SetActive(false);
        }
    }

    private void ApplyVisuals()
    {
        float flicker = 1f + Random.Range(-0.08f, 0.08f);  //지지직 깜빡임

        float comfort = ComfortSettings.FlashIntensity;  //섬광 줄이기 설정
        Color tint = tintColor;
        tint.a = (0.22f * intensity * flicker + burstTimer * 0.6f) * comfort;
        tintImage.color = tint;

        float height = canvasRect.rect.height > 0f ? canvasRect.rect.height : Screen.height;
        scanlineImage.color = new Color(1f, 1f, 1f, 0.35f * intensity);
        scanlineImage.uvRect = new Rect(0f, Time.unscaledTime * 0.6f, 1f, height / 6f);  //주사선이 천천히 흐름
    }

    private void OnDestroy()
    {
        if (scanlineTexture != null) Destroy(scanlineTexture);
    }

    private void UpdateGlitch(float deltaTime)
    {
        glitchTimer -= deltaTime;
        if (glitchTimer > 0f) return;

        glitchTimer = Random.Range(0.03f, 0.08f);

        float height = canvasRect.rect.height > 0f ? canvasRect.rect.height : Screen.height;
        float width = canvasRect.rect.width > 0f ? canvasRect.rect.width : Screen.width;
        float chance = (0.25f * intensity + burstTimer * 3f) * (ComfortSettings.ReduceFlash ? 0.2f : 1f);

        for (int i = 0; i < glitchBars.Length; i++)
        {
            Image bar = glitchBars[i];
            bool show = intensity > 0.01f && Random.value < chance;
            bar.enabled = show;
            if (!show) continue;

            RectTransform rect = bar.rectTransform;
            float barHeight = Random.Range(2f, 18f) * (height / 1920f);
            float shift = Random.Range(-0.06f, 0.06f) * width;  //가로로 어긋남

            rect.sizeDelta = new Vector2(0f, barHeight);
            rect.anchoredPosition = new Vector2(shift, Random.Range(-0.5f, 0.5f) * height);

            Color color = BarColors[Random.Range(0, BarColors.Length)];
            color.a = Random.Range(0.25f, 0.6f) * Mathf.Max(intensity, burstTimer * 4f);
            bar.color = color;
        }
    }
}
