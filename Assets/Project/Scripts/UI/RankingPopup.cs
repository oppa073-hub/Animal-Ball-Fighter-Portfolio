using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 로비 '랭킹' 창: 무한 모드 최고 방 기록 순위 (Unity Leaderboards).
// 아래쪽에 내 순위.
public class RankingPopup : MonoBehaviour
{
    private const int TopCount = 30;

    private LobbyUiKit kit;
    private RectTransform listContent;
    private TMP_Text statusText;
    private RectTransform myRow;
    private bool closed;

    public static void Show(Transform parent)
    {
        LobbyUiKit kit = LobbyUiKit.Load();
        if (kit == null || parent == null) return;

        GameObject go = new GameObject("RankingPopup", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        LobbyUiKit.Stretch((RectTransform)go.transform);
        go.transform.SetAsLastSibling();

        RankingPopup popup = go.AddComponent<RankingPopup>();
        popup.kit = kit;
        popup.Build();
    }

    private void Build()
    {
        kit.Popup(transform, "Popup", LobbyUiKit.Loc("ranking_title", "무한 모드 랭킹"), new Vector2(960f, 1500f), Close, out RectTransform content);

        TMP_Text subtitle = kit.Text(content, "Subtitle", LobbyUiKit.Loc("ranking_subtitle", "최고 도달 방 기준"), 32f, kit.shadowMaterial);
        Place(subtitle.rectTransform, 0f, 50f);

        // 순위 목록 (스크롤)
        RectTransform scrollRect = new GameObject("Scroll", typeof(RectTransform)).GetComponent<RectTransform>();
        scrollRect.SetParent(content, false);
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(0f, 150f);
        scrollRect.offsetMax = new Vector2(0f, -70f);
        Image scrollBack = kit.Image(scrollRect, "Back", kit.cellFrame, new Color(0f, 0f, 0f, 0.25f), true);
        LobbyUiKit.Stretch(scrollBack.rectTransform);

        RectTransform viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
        viewport.SetParent(scrollRect, false);
        LobbyUiKit.Stretch(viewport);
        viewport.offsetMin = new Vector2(12f, 12f);
        viewport.offsetMax = new Vector2(-12f, -12f);
        viewport.gameObject.AddComponent<RectMask2D>();
        Image viewportHit = viewport.gameObject.AddComponent<Image>();
        viewportHit.color = new Color(0f, 0f, 0f, 0f);

        listContent = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        listContent.SetParent(viewport, false);
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;
        listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = listContent;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        statusText = kit.Text(scrollRect, "Status", LobbyUiKit.Loc("ranking_loading", "불러오는 중..."), 36f, kit.shadowMaterial);
        LobbyUiKit.Stretch(statusText.rectTransform);

        // 내 순위
        myRow = new GameObject("MyRow", typeof(RectTransform)).GetComponent<RectTransform>();
        myRow.SetParent(content, false);
        myRow.anchorMin = new Vector2(0f, 0f);
        myRow.anchorMax = new Vector2(1f, 0f);
        myRow.pivot = new Vector2(0.5f, 0f);
        myRow.anchoredPosition = new Vector2(0f, 10f);
        myRow.sizeDelta = new Vector2(0f, 120f);

        Load();
    }

    private void OnDestroy()
    {
        closed = true;
    }

    private static void Place(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(0f, height);
    }

    private async void Load()
    {
        if (!UgsBootstrap.IsSignedIn)
        {
            statusText.text = LobbyUiKit.Loc("ranking_offline", "인터넷에 연결하면 랭킹을 볼 수 있어요.");
            BuildMyRow(null);
            return;
        }

        (List<UgsBootstrap.RankingEntry> top, UgsBootstrap.RankingEntry mine) = await UgsBootstrap.FetchEndlessRankingAsync(TopCount);
        if (closed || this == null) return;

        statusText.text = top.Count == 0 ? LobbyUiKit.Loc("ranking_empty", "아직 기록이 없어요. 무한 모드에 도전해 보세요!") : "";
        foreach (UgsBootstrap.RankingEntry entry in top)
        {
            CreateRow(listContent, entry, 100f);
        }

        BuildMyRow(mine);
    }

    private void BuildMyRow(UgsBootstrap.RankingEntry mine)
    {
        for (int i = myRow.childCount - 1; i >= 0; i--) Destroy(myRow.GetChild(i).gameObject);

        if (mine == null)
        {
            int best = SaveManager.Instance != null ? SaveManager.Instance.Data.bestEndlessRoom : 0;
            mine = new UgsBootstrap.RankingEntry
            {
                rank = 0,
                name = SaveManager.Instance != null && !string.IsNullOrWhiteSpace(SaveManager.Instance.Data.nickname) ? SaveManager.Instance.Data.nickname : LobbyUiKit.Loc("ranking_me", "나"),
                score = best,
                isMine = true,
            };
        }

        RectTransform row = CreateRow(myRow, mine, 120f);
        LobbyUiKit.Stretch(row);
    }

    private RectTransform CreateRow(RectTransform parent, UgsBootstrap.RankingEntry entry, float height)
    {
        Color background = entry.isMine ? new Color(0.95f, 0.7f, 0.2f, 0.95f) : new Color(0.08f, 0.18f, 0.38f, 0.95f);
        Image row = kit.Image(parent, $"Rank{entry.rank}", kit.cellFrame, background, true);
        RectTransform rect = row.rectTransform;
        rect.sizeDelta = new Vector2(0f, height);

        // 순위 (1~3위는 왕관)
        if (entry.rank >= 1 && entry.rank <= 3)
        {
            Color[] crownColors = { new Color(1f, 0.85f, 0.25f), new Color(0.82f, 0.86f, 0.92f), new Color(0.85f, 0.55f, 0.3f) };
            Image crown = kit.Image(rect, "Crown", kit.crownIcon, crownColors[entry.rank - 1]);
            crown.rectTransform.anchorMin = crown.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            crown.rectTransform.anchoredPosition = new Vector2(70f, 0f);
            crown.rectTransform.sizeDelta = new Vector2(76f, 76f);
        }

        string rankLabel = entry.rank > 0 ? entry.rank.ToString() : "-";
        TMP_Text rankText = kit.Text(rect, "Rank", rankLabel, entry.rank >= 1 && entry.rank <= 3 ? 30f : 40f, kit.outlineMaterial);
        RectTransform rankRect = rankText.rectTransform;
        rankRect.anchorMin = rankRect.anchorMax = new Vector2(0f, 0.5f);
        rankRect.anchoredPosition = new Vector2(70f, entry.rank >= 1 && entry.rank <= 3 ? -6f : 0f);
        rankRect.sizeDelta = new Vector2(110f, height);

        TMP_Text nameText = kit.Text(rect, "Name", entry.name, 36f, kit.outlineMaterial, TextAlignmentOptions.Left);
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.offsetMin = new Vector2(140f, 0f);
        nameRect.offsetMax = new Vector2(-220f, 0f);
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = TextOverflowModes.Ellipsis;

        string scoreLabel = entry.score > 0 ? string.Format(LobbyUiKit.Loc("ranking_room_format", "{0}번 방"), entry.score) : "-";
        TMP_Text scoreText = kit.Text(rect, "Score", scoreLabel, 36f, kit.outlineMaterial, TextAlignmentOptions.Right);
        RectTransform scoreRect = scoreText.rectTransform;
        scoreRect.anchorMin = new Vector2(1f, 0f);
        scoreRect.anchorMax = new Vector2(1f, 1f);
        scoreRect.pivot = new Vector2(1f, 0.5f);
        scoreRect.anchoredPosition = new Vector2(-30f, 0f);
        scoreRect.sizeDelta = new Vector2(200f, 0f);

        return rect;
    }

    private void Close()
    {
        Destroy(gameObject);
    }
}
