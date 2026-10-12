using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 전투 HUD 문구: 연속 처치(DOUBLE KILL...), 콤보 단계(GREAT!...), 피버 중 화면 가장자리 빛.
// CombatJuice가 소유한다. HUD 캔버스에 실행 중 생성하므로 씬 수정이 필요 없다. 히트스톱/슬로모션과 무관하게 실제 시간으로 움직인다.
public class CombatCalloutUI
{
    private const float MaxEdgeAlpha = 0.28f;  //눈부시지 않게

    private static readonly string[] MilestoneWords = { "GREAT!", "AWESOME!", "INSANE!", "CRAZY!!", "GODLIKE!!" };

    private Canvas hud;
    private bool searched;

    private TextMeshProUGUI multiKillText;
    private TextMeshProUGUI milestoneText;
    private TextMeshProUGUI perfectText;
    private TextMeshProUGUI missText;
    private TextMeshProUGUI trickText;
    private RawImage edgeGlow;
    private Texture2D edgeTexture;

    private Color edgeColor;
    private float edgeTargetAlpha;
    private float edgeAlpha;

    public void ShowMultiKill(int count)
    {
        if (!EnsureUi()) return;

        multiKillText.text = count switch
        {
            2 => "DOUBLE KILL",
            3 => "TRIPLE KILL",
            4 => "QUADRA KILL",
            _ => $"RAMPAGE x{count}"
        };

        multiKillText.color = count >= 5 ? new Color(1f, 0.35f, 0.35f) : count >= 3 ? new Color(1f, 0.6f, 0.2f) : new Color(1f, 0.9f, 0.3f);
        Pop(multiKillText, 1.6f, Random.Range(-6f, 6f), 0.7f);
    }

    public void ShowMilestone(int combo, Color color)
    {
        if (!EnsureUi()) return;

        int index = Mathf.Clamp(combo / 10 - 1, 0, MilestoneWords.Length - 1);
        milestoneText.text = MilestoneWords[index];
        milestoneText.color = color;
        Pop(milestoneText, 2.4f, Random.Range(-10f, 10f), 0.9f);
    }

    public void ShowPerfect(Vector3 worldPosition, int chain)  //공 바로 위에 크게
    {
        if (!EnsureUi()) return;

        perfectText.text = chain >= 2 ? $"PERFECT x{chain}" : "PERFECT!";
        perfectText.color = chain >= 3 ? new Color(1f, 0.55f, 0.15f) : new Color(1f, 0.88f, 0.25f);
        PlaceNear(perfectText.rectTransform, worldPosition, 150f);
        Pop(perfectText, 2f + 0.15f * Mathf.Min(chain, 4), Random.Range(-6f, 6f), 0.45f);
    }

    public void ShowTrick(string text, int gold, Vector3 worldPosition)  //트릭샷 (뱅크샷/원샷): 공 근처에 하늘색 문구 + 골드
    {
        if (!EnsureUi()) return;

        trickText.text = gold > 0 ? $"{text}\n<size=60%><color=#FFD54A>+{gold} G</color></size>" : text;
        trickText.color = new Color(0.45f, 0.95f, 1f);
        PlaceNear(trickText.rectTransform, worldPosition, 230f);
        Pop(trickText, 2.2f, Random.Range(-8f, 8f), 0.8f);
    }

    public void ShowMiss(Vector3 worldPosition)
    {
        ShowSmall("MISS", new Color(0.75f, 0.78f, 0.85f), worldPosition);
    }

    public void ShowSmall(string text, Color color, Vector3 worldPosition)  //짧은 작은 문구 (MISS, BLOCK, 가시 등)
    {
        if (!EnsureUi()) return;

        missText.text = text;
        missText.color = color;
        PlaceNear(missText.rectTransform, worldPosition, 110f);
        Pop(missText, 1.3f, 0f, 0.2f);
    }

    private void PlaceNear(RectTransform rect, Vector3 worldPosition, float offsetY)  //월드 위치를 HUD 좌표로 (화면 밖으로 안 나가게)
    {
        Camera camera = Camera.main;
        if (camera == null) return;

        RectTransform hudRect = (RectTransform)hud.transform;
        Camera uiCamera = hud.renderMode == RenderMode.ScreenSpaceOverlay ? null : hud.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRect, camera.WorldToScreenPoint(worldPosition), uiCamera, out Vector2 local)) return;

        Rect bounds = hudRect.rect;
        local.y += offsetY;
        local.x = Mathf.Clamp(local.x, bounds.xMin + bounds.width * 0.25f, bounds.xMax - bounds.width * 0.25f);
        local.y = Mathf.Clamp(local.y, bounds.yMin + bounds.height * 0.1f, bounds.yMax - bounds.height * 0.15f);

        rect.anchoredPosition = local;
    }

    public void SetFever(int tier, Color color)
    {
        if (!EnsureUi()) return;

        edgeColor = color;
        edgeTargetAlpha = tier > 0 ? Mathf.Lerp(0.14f, MaxEdgeAlpha, (tier - 1) / 2f) : 0f;
    }

    public void Update()
    {
        if (edgeGlow == null) return;

        edgeAlpha = Mathf.MoveTowards(edgeAlpha, edgeTargetAlpha, Time.unscaledDeltaTime * 0.6f);
        float pulse = edgeTargetAlpha > 0f ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f) : 1f;

        edgeGlow.color = new Color(edgeColor.r, edgeColor.g, edgeColor.b, edgeAlpha * pulse);
        edgeGlow.enabled = edgeAlpha > 0.001f;
    }

    public void Dispose()
    {
        if (multiKillText != null) multiKillText.rectTransform.DOKill();
        if (milestoneText != null) milestoneText.rectTransform.DOKill();
        if (multiKillText != null) multiKillText.DOKill();
        if (milestoneText != null) milestoneText.DOKill();
        if (perfectText != null) { perfectText.rectTransform.DOKill(); perfectText.DOKill(); }
        if (missText != null) { missText.rectTransform.DOKill(); missText.DOKill(); }
        if (trickText != null) { trickText.rectTransform.DOKill(); trickText.DOKill(); }
        if (edgeTexture != null) Object.Destroy(edgeTexture);
    }

    private static void Pop(TextMeshProUGUI text, float startScale, float tilt, float holdTime)
    {
        RectTransform rect = text.rectTransform;
        rect.DOKill();
        text.DOKill();

        text.alpha = 1f;
        rect.localScale = Vector3.one * startScale;
        rect.localRotation = Quaternion.Euler(0f, 0f, tilt);

        rect.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
        rect.DOPunchRotation(new Vector3(0f, 0f, 8f), 0.35f, 8).SetDelay(0.2f).SetUpdate(true);
        text.DOFade(0f, 0.3f).SetDelay(holdTime).SetUpdate(true);
    }

    private bool EnsureUi()
    {
        if (hud != null) return true;
        if (searched) return false;
        searched = true;

        hud = ComboSystem.FindHudCanvas();
        if (hud == null)
        {
            Debug.LogWarning("[CombatCallout] HUD 캔버스를 찾지 못해 전투 문구를 표시하지 않습니다.");
            return false;
        }

        CreateEdgeGlow();
        multiKillText = CreateText("MultiKillText", new Vector2(0.5f, 0.665f), 70f);
        milestoneText = CreateText("MilestoneText", new Vector2(0.5f, 0.58f), 118f);
        perfectText = CreateText("PerfectText", new Vector2(0.5f, 0.5f), 84f);  //위치는 표시할 때 공 근처로 옮김
        missText = CreateText("MissText", new Vector2(0.5f, 0.5f), 52f);
        trickText = CreateText("TrickText", new Vector2(0.5f, 0.5f), 78f);
        trickText.rectTransform.sizeDelta = new Vector2(1000f, 220f);  //골드 줄까지 두 줄
        return true;
    }

    private TextMeshProUGUI CreateText(string name, Vector2 anchor, float fontSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(hud.transform, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.sizeDelta = new Vector2(1000f, 160f);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        CombatUiStyle style = CombatUiStyle.Get();
        TMP_Text sample = hud.GetComponentInChildren<TMP_Text>(true);
        if (style != null && style.boldFont != null) text.font = style.boldFont;
        else if (sample != null) text.font = sample.font;

        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold | FontStyles.Italic;
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(40, 10, 30, 255);
        text.alpha = 0f;
        return text;
    }

    private void CreateEdgeGlow()
    {
        GameObject go = new GameObject("FeverEdgeGlow", typeof(RectTransform));
        go.transform.SetParent(hud.transform, false);
        go.transform.SetAsFirstSibling();  //다른 HUD 뒤에

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        edgeTexture = CreateEdgeTexture();
        edgeGlow = go.AddComponent<RawImage>();
        edgeGlow.texture = edgeTexture;
        edgeGlow.raycastTarget = false;
        edgeGlow.color = new Color(1f, 1f, 1f, 0f);
        edgeGlow.enabled = false;
    }

    private static Texture2D CreateEdgeTexture()  //가운데는 투명, 가장자리로 갈수록 진해짐
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
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));  //가장자리까지 거리 (0~0.5)
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - edge / 0.22f), 2f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }
}
