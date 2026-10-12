using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 전투 화면 위쪽 가운데 통합 바: [런 골드] | [방 진행 점 + 현재/전체] | [방 종류 칩]
// 무한 모드는 가운데에 방 번호/최고 기록, 바 아래에 보상 골드(로비로 가져가는 골드)를 따로 표시한다.
// 기존 StageUI(오른쪽 위 글자)는 StageUI가 이 바로 내용을 넘기고 자신은 숨긴다. 실행 중에 HUD 캔버스에 만든다.
public class CombatTopBar : MonoBehaviour
{
    private const float BarWidth = 760f;
    private const float BarHeight = 86f;
    private const float PipSize = 26f;
    private const float PipSpacing = 12f;

    private static readonly Color PanelColor = new Color(0.03f, 0.06f, 0.16f, 0.78f);
    private static readonly Color ClearedPip = new Color(1f, 0.82f, 0.3f);
    private static readonly Color UpcomingPip = new Color(1f, 1f, 1f, 0.28f);
    private static readonly Color BossPip = new Color(1f, 0.3f, 0.25f);

    private static CombatTopBar instance;

    private CombatUiStyle style;
    private RectTransform bar;
    private TMP_Text goldText;
    private RectTransform goldGroup;
    private RectTransform pipRoot;
    private TMP_Text progressText;
    private Image chip;
    private TMP_Text chipText;
    private TMP_Text endlessText;
    private readonly List<Image> pips = new List<Image>();
    private Image currentPip;

    public static CombatTopBar GetOrCreate()
    {
        if (instance != null) return instance;

        Canvas hud = ComboSystem.FindHudCanvas();
        if (hud == null) return null;

        GameObject go = new GameObject("CombatTopBar", typeof(RectTransform));
        go.transform.SetParent(SafeAreaFitter.GetContentRoot(hud), false);
        go.transform.SetAsFirstSibling();  //팝업/패널보다 뒤
        instance = go.AddComponent<CombatTopBar>();
        instance.Build();
        return instance;
    }

    public static void SetVisible(bool visible)  //StageUI가 숨겨질 때(게임오버 등) 같이 숨김
    {
        if (instance != null) instance.gameObject.SetActive(visible);
    }

    private void OnEnable() => RunState.GoldChanged += HandleGoldChanged;
    private void OnDisable() => RunState.GoldChanged -= HandleGoldChanged;

    private void OnDestroy()
    {
        bar?.DOKill();
        goldGroup?.DOKill();
        if (instance == this) instance = null;
    }

    // ───────── 갱신 ─────────

    public void ShowRoom(RoomType type, int room, string label)
    {
        gameObject.SetActive(true);

        chipText.text = label;
        chip.color = GetRoomColor(type);
        chip.rectTransform.DOKill();
        chip.rectTransform.localScale = Vector3.one;
        chip.rectTransform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 6, 0.5f).SetUpdate(true);

        if (GameSessionData.IsEndless) ShowEndlessProgress(room);
        else ShowStageProgress(room);

        RefreshEndless();
        HandleGoldChanged(RunState.Gold);
    }

    public void RefreshEndless()
    {
        bool endless = GameSessionData.IsEndless;
        endlessText.gameObject.SetActive(endless);
        if (!endless) return;

        int best = SaveManager.Instance != null ? SaveManager.Instance.Data.bestEndlessRoom : 0;
        endlessText.text = string.Format(RunPopupUI.Loc("hud_endless_reward_format", "Reward {0} G  ·  Best Room {1}"), GameSessionData.EndlessRunGold, best);
    }

    private void HandleGoldChanged(int gold)
    {
        if (goldText == null) return;

        goldText.text = $"{gold:N0}";
        goldGroup.DOKill();
        goldGroup.localScale = Vector3.one;
        goldGroup.DOPunchScale(Vector3.one * 0.15f, 0.25f, 6, 0.6f).SetUpdate(true);
    }

    private void ShowStageProgress(int room)
    {
        int total = GameSessionData.SelectedStage != null ? Mathf.Max(1, GameSessionData.SelectedStage.totalRooms) : 5;
        progressText.text = $"{Mathf.Min(room, total)} / {total}";

        EnsurePips(total);
        float width = total * PipSize + (total - 1) * PipSpacing;

        currentPip = null;
        for (int i = 0; i < pips.Count; i++)
        {
            Image pip = pips[i];
            bool used = i < total;
            pip.gameObject.SetActive(used);
            if (!used) continue;

            int number = i + 1;
            bool isBoss = number == total;
            pip.rectTransform.anchoredPosition = new Vector2(-width * 0.5f + PipSize * 0.5f + i * (PipSize + PipSpacing), 0f);
            pip.rectTransform.localScale = Vector3.one;
            pip.sprite = isBoss && style != null && style.star != null ? style.star : (style != null ? style.nodeFrame : null);

            if (number < room) pip.color = ClearedPip;
            else if (number == room) { pip.color = isBoss ? BossPip : Color.white; currentPip = pip; }
            else pip.color = isBoss ? new Color(BossPip.r, BossPip.g, BossPip.b, 0.55f) : UpcomingPip;
        }

        pipRoot.anchoredPosition = new Vector2(0f, 12f);
        progressText.rectTransform.anchoredPosition = new Vector2(0f, -22f);
    }

    private void ShowEndlessProgress(int room)
    {
        foreach (Image pip in pips) pip.gameObject.SetActive(false);
        currentPip = null;

        progressText.text = string.Format(RunPopupUI.Loc("hud_endless_room_format", "ROOM {0}"), room);
        progressText.rectTransform.anchoredPosition = Vector2.zero;
    }

    private void Update()
    {
        if (currentPip != null)  //현재 방 점은 숨쉬듯 커졌다 작아짐
        {
            float pulse = 1.15f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
            currentPip.rectTransform.localScale = Vector3.one * pulse;
        }
    }

    private static Color GetRoomColor(RoomType type)
    {
        return type switch
        {
            RoomType.Elite => new Color(0.85f, 0.3f, 0.3f),
            RoomType.Boss => new Color(0.7f, 0.12f, 0.15f),
            RoomType.Shop => new Color(0.85f, 0.62f, 0.15f),
            RoomType.Rest => new Color(0.25f, 0.65f, 0.35f),
            RoomType.Event => new Color(0.55f, 0.35f, 0.85f),
            _ => new Color(0.25f, 0.5f, 0.85f)
        };
    }

    private void EnsurePips(int count)
    {
        while (pips.Count < count)
        {
            Image pip = NewImage("Pip", pipRoot, style != null ? style.nodeFrame : null, UpcomingPip);
            pip.type = Image.Type.Simple;
            pip.preserveAspect = true;
            pip.raycastTarget = false;
            pip.rectTransform.sizeDelta = new Vector2(PipSize, PipSize);
            pips.Add(pip);
        }
    }

    // ───────── 화면 구성 ─────────

    private void Build()
    {
        style = CombatUiStyle.Get();

        bar = (RectTransform)transform;
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1f);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.anchoredPosition = new Vector2(0f, -22f);
        bar.sizeDelta = new Vector2(BarWidth, BarHeight);

        Image background = gameObject.AddComponent<Image>();
        background.sprite = style != null ? style.badge : null;
        background.type = Image.Type.Sliced;
        background.color = PanelColor;
        background.raycastTarget = false;

        // 왼쪽: 런 골드
        goldGroup = NewRect("Gold", bar);
        goldGroup.anchorMin = goldGroup.anchorMax = new Vector2(0f, 0.5f);
        goldGroup.pivot = new Vector2(0f, 0.5f);
        goldGroup.anchoredPosition = new Vector2(22f, 0f);
        goldGroup.sizeDelta = new Vector2(190f, 64f);

        if (style != null && style.coinSprite != null)
        {
            Image coin = NewImage("Coin", goldGroup, style.coinSprite, Color.white);
            coin.preserveAspect = true;
            coin.raycastTarget = false;
            coin.rectTransform.anchorMin = coin.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            coin.rectTransform.pivot = new Vector2(0f, 0.5f);
            coin.rectTransform.sizeDelta = new Vector2(52f, 52f);
        }

        goldText = NewText("Text", goldGroup, 42f, new Color(1f, 0.86f, 0.3f));
        goldText.alignment = TextAlignmentOptions.Left;
        goldText.rectTransform.anchorMin = Vector2.zero;
        goldText.rectTransform.anchorMax = Vector2.one;
        goldText.rectTransform.offsetMin = new Vector2(62f, 0f);
        goldText.rectTransform.offsetMax = Vector2.zero;

        // 가운데: 방 진행
        pipRoot = NewRect("Pips", bar);
        pipRoot.sizeDelta = new Vector2(320f, PipSize);

        progressText = NewText("Progress", bar, 30f, Color.white);
        progressText.rectTransform.sizeDelta = new Vector2(320f, 40f);

        // 오른쪽: 방 종류 칩
        chip = NewImage("RoomChip", bar, style != null ? style.nodeFrame : null, Color.gray);
        chip.type = Image.Type.Sliced;
        chip.raycastTarget = false;
        chip.rectTransform.anchorMin = chip.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        chip.rectTransform.pivot = new Vector2(1f, 0.5f);
        chip.rectTransform.anchoredPosition = new Vector2(-16f, 0f);
        chip.rectTransform.sizeDelta = new Vector2(180f, 60f);

        chipText = NewText("Label", chip.transform, 32f, Color.white);
        chipText.rectTransform.anchorMin = Vector2.zero;
        chipText.rectTransform.anchorMax = Vector2.one;
        chipText.rectTransform.offsetMin = new Vector2(8f, 0f);
        chipText.rectTransform.offsetMax = new Vector2(-8f, 0f);
        chipText.enableAutoSizing = true;
        chipText.fontSizeMin = 20f;
        chipText.fontSizeMax = 32f;

        // 무한 모드: 바 아래 보상 골드 / 최고 기록
        endlessText = NewText("EndlessInfo", bar, 28f, new Color(1f, 0.9f, 0.55f));
        endlessText.rectTransform.anchorMin = endlessText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        endlessText.rectTransform.pivot = new Vector2(0.5f, 1f);
        endlessText.rectTransform.anchoredPosition = new Vector2(0f, -6f);
        endlessText.rectTransform.sizeDelta = new Vector2(BarWidth, 40f);
        endlessText.gameObject.SetActive(false);
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
        text.outlineWidth = 0.22f;
        text.outlineColor = new Color32(5, 12, 35, 255);
        return text;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rect = NewRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
}
