using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 플레이어 체력 UI: 화면 아래 가운데(대쉬 게이지 위) 가로 바. 실행 중 HUD 캔버스에 만든다.
// - HP 표시 + 체력바 + 수치, 위쪽에 실드 바(+수치)
// - 맞으면: 흰 잔상, 바 흔들림, 화면 가장자리 붉게 번쩍
// - 30% 이하: 바 붉게 맥동 + 가장자리 붉은 빛 유지
// - 회복: 초록 번쩍 + "+N" 떠오름
// 씬의 예전 원형 게이지(fillImg)는 숨기기만 한다 (UIManager가 이 오브젝트를 켜고 끄면 새 바도 같이 따라감).
public class PlayerHealthUI : MonoBehaviour
{
    private const float BarWidth = 600f;
    private const float BarHeight = 34f;
    private const float BarY = 205f;          //화면 아래에서 (대쉬 게이지 위)
    private const float LowHpRatio = 0.3f;
    private const float GhostDelay = 0.35f;
    private const float GhostSpeed = 0.7f;

    private static readonly Color HpColor = new Color(0.3f, 0.9f, 0.4f);
    private static readonly Color MidHpColor = new Color(1f, 0.8f, 0.25f);
    private static readonly Color LowHpColor = new Color(1f, 0.25f, 0.2f);
    private static readonly Color HealColor = new Color(0.55f, 1f, 0.6f);
    private static readonly Color ShieldColor = new Color(0.4f, 0.75f, 1f);

    private Health health;
    private PlayerShield shield;
    [SerializeField] private Image fillImg;

    private CombatUiStyle style;
    private RectTransform barRoot;
    private Image fill;
    private Image ghost;
    private TMP_Text hpText;
    private RectTransform heart;
    private RectTransform shieldRoot;
    private Image shieldFill;
    private TMP_Text shieldText;
    private TMP_Text healPopup;
    private RawImage vignette;
    private Texture2D vignetteTexture;

    private int lastHp = -1;
    private int shownHp = -1, shownMaxHp = -1, shownShield = -1;  //값이 바뀔 때만 문자열을 새로 만든다
    private float ghostRatio = 1f;
    private float ghostHoldTimer;
    private float hitFlash;    //화면 가장자리 번쩍임 세기 (0~1)
    private float healFlash;   //바 초록 번쩍임 세기 (0~1)

    public void Initialize(Health health)
    {
        this.health = health;
        shield = health != null ? health.GetComponent<PlayerShield>() : null;
        if (fillImg != null) fillImg.fillAmount = health.HpRatio;

        if (!Build()) return;  //HUD 캔버스를 못 찾으면 예전 원형 게이지를 그대로 둠
        HideLegacyVisual();

        lastHp = health.CurrentHp;
        shownHp = shownMaxHp = shownShield = -1;
        ghostRatio = health.HpRatio;
        SetBarVisible(gameObject.activeInHierarchy);
        Refresh(true);
    }

    private void OnEnable() => SetBarVisible(true);
    private void OnDisable() => SetBarVisible(false);

    private void OnDestroy()
    {
        barRoot?.DOKill();
        heart?.DOKill();
        healPopup?.DOKill();
        if (barRoot != null) Destroy(barRoot.gameObject);
        if (vignette != null) Destroy(vignette.gameObject);
        if (vignetteTexture != null) Destroy(vignetteTexture);
    }

    private void SetBarVisible(bool visible)
    {
        if (barRoot != null) barRoot.gameObject.SetActive(visible);
        if (vignette != null) vignette.gameObject.SetActive(visible);
    }

    private void HideLegacyVisual()  //예전 원형 게이지는 화면에서만 숨김 (오브젝트/스크립트 연결은 유지)
    {
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    private void Update()
    {
        if (health == null || fill == null) return;
        Refresh(false);
    }

    private void Refresh(bool immediate)
    {
        float ratio = health.HpRatio;
        int hp = Mathf.Max(0, health.CurrentHp);

        if (fillImg != null) fillImg.fillAmount = ratio;
        fill.fillAmount = ratio;

        if (!immediate && lastHp >= 0)
        {
            if (hp < lastHp) OnDamaged();
            else if (hp > lastHp) OnHealed(hp - lastHp);
        }
        lastHp = hp;

        // 흰 잔상: 잠시 머물렀다가 실제 체력을 따라 내려감 (회복이면 바로 맞춤)
        if (immediate || ghostRatio < ratio) ghostRatio = ratio;
        else if (ghostHoldTimer > 0f) ghostHoldTimer -= Time.unscaledDeltaTime;
        else ghostRatio = Mathf.MoveTowards(ghostRatio, ratio, GhostSpeed * Time.unscaledDeltaTime);
        ghost.fillAmount = ghostRatio;

        // 색: 체력에 따라 초록 → 노랑 → 빨강, 낮으면 맥동, 회복 직후 초록 번쩍
        bool low = ratio <= LowHpRatio && hp > 0;
        Color color = ratio > 0.6f ? HpColor : ratio > LowHpRatio ? MidHpColor : LowHpColor;
        if (low && !DOTween.IsTweening(heart))  //피격 튕김 연출 중엔 맥동이 덮어쓰지 않게
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
            color = Color.Lerp(LowHpColor, new Color(1f, 0.6f, 0.5f), pulse);
            heart.localScale = Vector3.one * (1f + 0.12f * pulse);
        }
        else if (!low && !DOTween.IsTweening(heart)) heart.localScale = Vector3.one;

        healFlash = Mathf.MoveTowards(healFlash, 0f, Time.unscaledDeltaTime * 2.5f);
        fill.color = Color.Lerp(color, HealColor, healFlash);

        if (hp != shownHp || health.MaxHp != shownMaxHp)
        {
            shownHp = hp;
            shownMaxHp = health.MaxHp;
            hpText.text = $"{hp:N0} / {shownMaxHp:N0}";
        }

        // 화면 가장자리: 맞으면 번쩍, 저체력이면 은은하게 유지
        hitFlash = Mathf.MoveTowards(hitFlash, 0f, Time.unscaledDeltaTime * 2.2f);
        float lowGlow = low ? 0.16f + 0.08f * Mathf.Sin(Time.unscaledTime * 4f) : 0f;
        float alpha = Mathf.Max(hitFlash * 0.45f * ComfortSettings.FlashIntensity, lowGlow);  //섬광 줄이기 설정이면 피격 번쩍임만 약하게
        vignette.color = new Color(1f, 0.1f, 0.08f, alpha);
        vignette.enabled = alpha > 0.005f;

        RefreshShield();
    }

    private void RefreshShield()
    {
        bool hasShield = shield != null && shield.MaxShield > 0 && shield.CurrentShield > 0;
        shieldRoot.gameObject.SetActive(hasShield);
        if (!hasShield) return;

        shieldFill.fillAmount = (float)shield.CurrentShield / shield.MaxShield;
        if (shield.CurrentShield != shownShield)
        {
            shownShield = shield.CurrentShield;
            shieldText.text = $"+{shownShield:N0}";
        }
    }

    private void OnDamaged()
    {
        ghostHoldTimer = GhostDelay;
        hitFlash = 1f;

        barRoot.DOKill(true);
        barRoot.DOShakeAnchorPos(0.25f, 10f, 24, 90f, false, true).SetUpdate(true);
        heart.DOKill(true);
        heart.DOPunchScale(Vector3.one * 0.35f, 0.25f, 6, 0.5f).SetUpdate(true);
    }

    private void OnHealed(int amount)
    {
        healFlash = 1f;

        healPopup.DOKill();
        healPopup.rectTransform.DOKill();
        healPopup.text = $"+{amount}";
        healPopup.alpha = 1f;
        healPopup.rectTransform.anchoredPosition = new Vector2(BarWidth * 0.5f - 40f, 10f);
        healPopup.rectTransform.DOAnchorPosY(60f, 0.7f).SetEase(Ease.OutCubic).SetUpdate(true);
        healPopup.DOFade(0f, 0.35f).SetDelay(0.4f).SetUpdate(true);
    }

    // ───────── 화면 구성 ─────────

    private bool Build()
    {
        if (barRoot != null) return true;

        Canvas hud = ComboSystem.FindHudCanvas();
        if (hud == null) return false;

        style = CombatUiStyle.Get();

        // 화면 가장자리 붉은 빛 (맨 뒤)
        GameObject vignetteObject = new GameObject("DamageVignette", typeof(RectTransform));
        vignetteObject.transform.SetParent(hud.transform, false);
        vignetteObject.transform.SetAsFirstSibling();
        RectTransform vignetteRect = (RectTransform)vignetteObject.transform;
        vignetteRect.anchorMin = Vector2.zero;
        vignetteRect.anchorMax = Vector2.one;
        vignetteRect.offsetMin = vignetteRect.offsetMax = Vector2.zero;
        vignetteTexture = CreateEdgeTexture();
        vignette = vignetteObject.AddComponent<RawImage>();
        vignette.texture = vignetteTexture;
        vignette.raycastTarget = false;
        vignette.enabled = false;

        // 바 묶음
        GameObject rootObject = new GameObject("PlayerHealthBar", typeof(RectTransform));
        rootObject.transform.SetParent(SafeAreaFitter.GetContentRoot(hud), false);
        barRoot = (RectTransform)rootObject.transform;
        barRoot.anchorMin = barRoot.anchorMax = new Vector2(0.5f, 0f);
        barRoot.pivot = new Vector2(0.5f, 0f);
        barRoot.anchoredPosition = new Vector2(0f, BarY);
        barRoot.sizeDelta = new Vector2(BarWidth, BarHeight);

        // 바탕 + 잔상 + 채움
        Image background = NewImage("Background", barRoot, style != null ? style.badge : null, new Color(0.03f, 0.05f, 0.12f, 0.85f));
        background.type = Image.Type.Sliced;
        Stretch(background.rectTransform, -6f);

        ghost = NewFilled("Ghost", barRoot, new Color(1f, 1f, 1f, 0.85f));
        fill = NewFilled("Fill", barRoot, HpColor);

        hpText = NewText("HpText", barRoot, 28f, Color.white);
        hpText.alignment = TextAlignmentOptions.Right;
        Stretch(hpText.rectTransform, 0f);
        hpText.rectTransform.offsetMax = new Vector2(-12f, 4f);
        hpText.rectTransform.offsetMin = new Vector2(12f, -4f);

        // 하트 (바 왼쪽 끝)
        heart = NewRect("Heart", barRoot);
        heart.anchorMin = heart.anchorMax = new Vector2(0f, 0.5f);
        heart.anchoredPosition = new Vector2(-42f, 0f);  //바 왼쪽 바깥
        heart.sizeDelta = new Vector2(64f, 64f);
        TMP_Text heartText = NewText("Icon", heart, 34f, new Color(1f, 0.35f, 0.4f));
        heartText.text = "HP";  //폰트에 하트 기호가 없을 수 있어 글자로
        Stretch(heartText.rectTransform, 0f);

        // 실드 (바 위쪽 얇은 파란 바)
        shieldRoot = NewRect("Shield", barRoot);
        shieldRoot.anchorMin = new Vector2(0f, 1f);
        shieldRoot.anchorMax = new Vector2(1f, 1f);
        shieldRoot.pivot = new Vector2(0.5f, 0f);
        shieldRoot.anchoredPosition = new Vector2(0f, 8f);
        shieldRoot.sizeDelta = new Vector2(-60f, 14f);

        Image shieldBack = NewImage("Back", shieldRoot, null, new Color(0.03f, 0.05f, 0.12f, 0.75f));
        Stretch(shieldBack.rectTransform, -3f);
        shieldFill = NewFilled("Fill", shieldRoot, ShieldColor);

        shieldText = NewText("Text", shieldRoot, 24f, ShieldColor);
        shieldText.alignment = TextAlignmentOptions.Left;
        shieldText.rectTransform.anchorMin = shieldText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        shieldText.rectTransform.pivot = new Vector2(0f, 0.5f);
        shieldText.rectTransform.anchoredPosition = new Vector2(8f, 0f);
        shieldText.rectTransform.sizeDelta = new Vector2(80f, 30f);
        shieldRoot.gameObject.SetActive(false);

        // 회복 "+N"
        healPopup = NewText("HealPopup", barRoot, 36f, HealColor);
        healPopup.rectTransform.anchorMin = healPopup.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        healPopup.rectTransform.sizeDelta = new Vector2(160f, 50f);
        healPopup.alpha = 0f;

        return true;
    }

    private static Sprite whiteSprite;

    private Image NewFilled(string name, Transform parent, Color color)
    {
        // Image의 Filled 방식은 스프라이트가 없으면 채움이 적용되지 않으므로 흰 스프라이트를 만들어 씀
        if (whiteSprite == null) whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));

        Image image = NewImage(name, parent, whiteSprite, color);
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.raycastTarget = false;
        Stretch(image.rectTransform, 0f);
        return image;
    }

    private TMP_Text NewText(string name, Transform parent, float size, Color color)
    {
        RectTransform rect = NewRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (style != null && style.boldFont != null) text.font = style.boldFont;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(5, 12, 35, 255);
        return text;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rect = NewRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    private static Texture2D CreateEdgeTexture()  //가운데 투명, 가장자리로 갈수록 진함
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (size - 1f);
                float v = y / (size - 1f);
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - edge / 0.25f), 2f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }
}
