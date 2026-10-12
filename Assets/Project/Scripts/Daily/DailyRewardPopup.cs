using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 로비 '오늘의 보상' 창: 위쪽 7일 출석 칸 + 아래 오늘의 미션 3개
public class DailyRewardPopup : MonoBehaviour
{
    private LobbyUiKit kit;
    private RectTransform attendanceRow;
    private RectTransform missionList;
    private Button attendanceButton;
    private TMP_Text attendanceButtonLabel;

    public static void Show(Transform parent)
    {
        LobbyUiKit kit = LobbyUiKit.Load();
        if (kit == null || parent == null) return;

        GameObject go = new GameObject("DailyRewardPopup", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        LobbyUiKit.Stretch((RectTransform)go.transform);
        go.transform.SetAsLastSibling();

        DailyRewardPopup popup = go.AddComponent<DailyRewardPopup>();
        popup.kit = kit;
        popup.Build();
    }

    private void Build()
    {
        kit.Popup(transform, "Popup", LobbyUiKit.Loc("daily_title", "오늘의 보상"), new Vector2(960f, 1420f), Close, out RectTransform content);

        // ── 출석 ──
        TMP_Text attendanceTitle = kit.Text(content, "AttendanceTitle", LobbyUiKit.Loc("daily_attendance", "출석 보상"), 42f, kit.outlineMaterial, TextAlignmentOptions.Left);
        PlaceTop(attendanceTitle.rectTransform, 0f, 60f);

        attendanceRow = new GameObject("AttendanceRow", typeof(RectTransform)).GetComponent<RectTransform>();
        attendanceRow.SetParent(content, false);
        PlaceTop(attendanceRow, -70f, 330f);
        GridLayoutGroup grid = attendanceRow.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(200f, 150f);
        grid.spacing = new Vector2(14f, 16f);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;

        attendanceButton = kit.Button(content, "AttendanceButton", kit.greenButton, new Vector2(520f, 110f), ClaimAttendance);
        RectTransform buttonRect = (RectTransform)attendanceButton.transform;
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 1f);
        buttonRect.pivot = new Vector2(0.5f, 1f);
        buttonRect.anchoredPosition = new Vector2(0f, -420f);
        attendanceButtonLabel = kit.Text(buttonRect, "Label", "", 40f, kit.outlineMaterial);
        LobbyUiKit.Stretch(attendanceButtonLabel.rectTransform);

        // ── 미션 ──
        TMP_Text missionTitle = kit.Text(content, "MissionTitle", LobbyUiKit.Loc("daily_missions", "오늘의 미션"), 42f, kit.outlineMaterial, TextAlignmentOptions.Left);
        PlaceTop(missionTitle.rectTransform, -570f, 60f);

        missionList = new GameObject("MissionList", typeof(RectTransform)).GetComponent<RectTransform>();
        missionList.SetParent(content, false);
        PlaceTop(missionList, -640f, 560f);
        VerticalLayoutGroup layout = missionList.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 16f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        Refresh();
    }

    private static void PlaceTop(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(0f, height);
    }

    private void Refresh()
    {
        for (int i = attendanceRow.childCount - 1; i >= 0; i--) Destroy(attendanceRow.GetChild(i).gameObject);
        for (int i = missionList.childCount - 1; i >= 0; i--) Destroy(missionList.GetChild(i).gameObject);

        // 출석 칸
        int today = DailyRewardService.AttendanceDayIndex;
        bool canClaim = DailyRewardService.CanClaimAttendance;
        int claimedInCycle = SaveManager.Instance.Data.attendanceCount % DailyRewardService.AttendanceRewards.Length;
        if (!canClaim && claimedInCycle == 0 && SaveManager.Instance.Data.attendanceCount > 0) claimedInCycle = DailyRewardService.AttendanceRewards.Length;  //7일째를 막 받음

        for (int day = 0; day < DailyRewardService.AttendanceRewards.Length; day++)
        {
            bool claimed = day < claimedInCycle;
            bool isToday = day == today && canClaim;
            CreateDayCell(day, DailyRewardService.AttendanceRewards[day], claimed, isToday);
        }

        attendanceButton.interactable = canClaim;
        attendanceButton.GetComponent<Image>().sprite = canClaim ? kit.greenButton : kit.grayButton;
        attendanceButtonLabel.text = canClaim ? LobbyUiKit.Loc("daily_claim", "받기") : LobbyUiKit.Loc("daily_come_back", "내일 또 만나요!");

        // 미션
        foreach (DailyMissionProgressData mission in DailyRewardService.GetTodayMissions())
        {
            CreateMissionRow(mission);
        }
    }

    private void CreateDayCell(int day, int reward, bool claimed, bool isToday)
    {
        Image cell = kit.Image(attendanceRow, $"Day{day + 1}", kit.cellFrame, isToday ? new Color(1f, 0.78f, 0.25f, 1f) : new Color(0.08f, 0.18f, 0.38f, 0.95f), true);
        bool isBig = day == DailyRewardService.AttendanceRewards.Length - 1;

        TMP_Text dayLabel = kit.Text(cell.rectTransform, "Day", string.Format(LobbyUiKit.Loc("daily_day_format", "{0}일"), day + 1), 30f, kit.outlineMaterial);
        RectTransform dayRect = dayLabel.rectTransform;
        dayRect.anchorMin = new Vector2(0f, 1f);
        dayRect.anchorMax = new Vector2(1f, 1f);
        dayRect.pivot = new Vector2(0.5f, 1f);
        dayRect.anchoredPosition = new Vector2(0f, -8f);
        dayRect.sizeDelta = new Vector2(0f, 40f);

        Image coin = kit.Image(cell.rectTransform, "Coin", isBig ? kit.giftIcon : kit.coinIcon, Color.white);
        coin.rectTransform.anchoredPosition = new Vector2(0f, -4f);
        coin.rectTransform.sizeDelta = new Vector2(64f, 64f);

        TMP_Text amount = kit.Text(cell.rectTransform, "Amount", reward.ToString(), 32f, kit.outlineMaterial);
        RectTransform amountRect = amount.rectTransform;
        amountRect.anchorMin = new Vector2(0f, 0f);
        amountRect.anchorMax = new Vector2(1f, 0f);
        amountRect.pivot = new Vector2(0.5f, 0f);
        amountRect.anchoredPosition = new Vector2(0f, 6f);
        amountRect.sizeDelta = new Vector2(0f, 40f);

        if (claimed)
        {
            Image dim = kit.Image(cell.rectTransform, "Claimed", null, new Color(0f, 0f, 0f, 0.45f));
            LobbyUiKit.Stretch(dim.rectTransform);
            Image check = kit.Image(cell.rectTransform, "Check", kit.checkIcon, new Color(0.45f, 1f, 0.5f));
            check.rectTransform.sizeDelta = new Vector2(80f, 80f);
        }

        if (isToday)
        {
            cell.rectTransform.DOScale(1.06f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(cell.gameObject);
        }
    }

    private void CreateMissionRow(DailyMissionProgressData mission)
    {
        DailyRewardService.MissionDefinition definition = DailyRewardService.GetDefinition(mission.missionId);
        if (definition == null) return;

        Image row = kit.Image(missionList, mission.missionId, kit.cellFrame, new Color(0.08f, 0.18f, 0.38f, 0.95f), true);
        row.rectTransform.sizeDelta = new Vector2(0f, 170f);

        Image icon = kit.Image(row.rectTransform, "Icon", kit.missionIcon, Color.white);
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(70f, 0f);
        iconRect.sizeDelta = new Vector2(84f, 84f);

        string title = string.Format(LobbyUiKit.Loc(definition.titleKey, definition.titleFallback), definition.target);
        TMP_Text titleText = kit.Text(row.rectTransform, "Title", title, 36f, kit.outlineMaterial, TextAlignmentOptions.Left);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(130f, 0f);
        titleRect.offsetMax = new Vector2(-260f, -14f);

        // 진행 막대
        Image barBack = kit.Image(row.rectTransform, "BarBack", kit.cellFrame, new Color(0f, 0f, 0f, 0.4f), true);
        RectTransform barRect = barBack.rectTransform;
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0.5f);
        barRect.offsetMin = new Vector2(130f, 28f);
        barRect.offsetMax = new Vector2(-260f, -10f);

        float ratio = Mathf.Clamp01((float)mission.progress / definition.target);
        Image barFill = kit.Image(barRect, "Fill", kit.cellFrame, mission.claimed ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.35f, 0.85f, 0.4f), true);
        RectTransform fillRect = barFill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(Mathf.Max(0.04f, ratio), 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        TMP_Text progress = kit.Text(barRect, "Progress", $"{mission.progress} / {definition.target}", 26f, kit.outlineMaterial);
        LobbyUiKit.Stretch(progress.rectTransform);

        // 보상 버튼
        bool done = mission.progress >= definition.target;
        Sprite sprite = mission.claimed ? kit.grayButton : done ? kit.yellowButton : kit.skyButton;
        Button claim = kit.Button(row.rectTransform, "Claim", sprite, new Vector2(220f, 120f), () =>
        {
            if (DailyRewardService.ClaimMission(mission) > 0)
            {
                SoundManager.Instance?.PlaySFX(SoundId.CoinCollect);
                Refresh();
            }
        });
        claim.interactable = done && !mission.claimed;
        RectTransform claimRect = (RectTransform)claim.transform;
        claimRect.anchorMin = claimRect.anchorMax = new Vector2(1f, 0.5f);
        claimRect.pivot = new Vector2(1f, 0.5f);
        claimRect.anchoredPosition = new Vector2(-20f, 0f);

        string claimText = mission.claimed ? LobbyUiKit.Loc("daily_claimed", "완료") : $"<sprite name=\"coin\">{definition.reward}";
        TMP_Text claimLabel = kit.Text(claimRect, "Label", mission.claimed ? claimText : definition.reward.ToString(), 36f, kit.outlineMaterial);
        LobbyUiKit.Stretch(claimLabel.rectTransform);
        claimLabel.rectTransform.offsetMin = new Vector2(70f, 0f);

        if (!mission.claimed)
        {
            Image coin = kit.Image(claimRect, "Coin", kit.coinIcon, Color.white);
            RectTransform coinRect = coin.rectTransform;
            coinRect.anchorMin = coinRect.anchorMax = new Vector2(0f, 0.5f);
            coinRect.anchoredPosition = new Vector2(52f, 0f);
            coinRect.sizeDelta = new Vector2(54f, 54f);
        }
        else
        {
            claimLabel.rectTransform.offsetMin = Vector2.zero;
        }
    }

    private void ClaimAttendance()
    {
        int reward = DailyRewardService.ClaimAttendance();
        if (reward <= 0) return;

        SoundManager.Instance?.PlaySFX(SoundId.CoinCollect);
        HapticFeedback.Medium();
        Refresh();
    }

    private void Close()
    {
        Destroy(gameObject);
    }
}
