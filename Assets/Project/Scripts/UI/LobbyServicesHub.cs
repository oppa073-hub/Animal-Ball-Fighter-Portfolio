using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 로비 홈 화면 왼쪽의 둥근 버튼 줄 (오늘의 보상 / 랭킹 / 도감). 로비 씬에 자동으로 생긴다.
// - 받을 보상이 있으면 오늘의 보상 버튼에 빨간 점
// - 하루 첫 로비 진입 때 출석을 아직 안 받았으면 오늘의 보상 창을 자동으로 띄움 (튜토리얼 첫 인사가 끝난 뒤)
public class LobbyServicesHub : MonoBehaviour
{
    private const string LobbySceneName = "MainLobby";
    private const float ButtonSize = 150f;
    private const float ButtonGap = 70f;  //버튼 아래 이름 칸 포함

    private static readonly string[] BlockingPanels =
    {
        "NicknamePanel", "PrivacyConsentPanel", "LanguagePanel", "GoldPurchasePopup", "SoundSettingsPanel",
        "DailyRewardPopup", "RankingPopup", "CodexPopup", MiscSettingsMenu.PanelName,
    };

    private static bool dailyAutoShownThisSession;

    private LobbyUiKit kit;
    private Transform popupParent;
    private GameObject dailyDot;
    private float idleTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded += (scene, mode) => Spawn();
        Spawn();
    }

    private static void Spawn()
    {
        if (SceneManager.GetActiveScene().name != LobbySceneName) return;
        if (FindAnyObjectByType<LobbyServicesHub>() != null) return;

        new GameObject("LobbyServicesHub").AddComponent<LobbyServicesHub>();
    }

    private void Start()
    {
        kit = LobbyUiKit.Load();
        GameObject safeArea = GameObject.Find("SafeArea");
        GameObject homePage = GameObject.Find("HomePage");
        if (kit == null || safeArea == null || homePage == null)
        {
            Debug.LogWarning("[LobbyServicesHub] LobbyUiKit 또는 로비 UI(SafeArea/HomePage)를 찾지 못해 버튼을 만들지 않습니다.");
            enabled = false;
            return;
        }

        popupParent = safeArea.transform;

        RectTransform column = new GameObject("ServiceButtons", typeof(RectTransform)).GetComponent<RectTransform>();
        column.SetParent(homePage.transform, false);
        column.anchorMin = column.anchorMax = new Vector2(0f, 1f);
        column.pivot = new Vector2(0f, 1f);
        column.anchoredPosition = new Vector2(28f, -190f);
        column.sizeDelta = new Vector2(ButtonSize, (ButtonSize + ButtonGap) * 3f);

        Button daily = CreateButton(column, 0, "DailyButton", kit.giftIcon, "lobby_daily", "보상", OpenDaily);
        CreateButton(column, 1, "RankingButton", kit.rankingIcon, "lobby_ranking", "랭킹", () => RankingPopup.Show(popupParent));
        CreateButton(column, 2, "CodexButton", kit.bookIcon, "lobby_codex", "도감", () => CodexPopup.Show(popupParent));

        dailyDot = CreateDot((RectTransform)daily.transform);

        DailyRewardService.Changed += RefreshDot;
        RefreshDot();
    }

    private void OnDestroy()
    {
        DailyRewardService.Changed -= RefreshDot;
    }

    private Button CreateButton(RectTransform parent, int index, string name, Sprite icon, string labelKey, string labelFallback, System.Action onClick)
    {
        Image circle = kit.Image(parent, name, kit.circleButton != null ? kit.circleButton : kit.cellFrame, new Color(0.1f, 0.2f, 0.42f, 0.92f), kit.circleButton == null);
        circle.raycastTarget = true;
        RectTransform rect = circle.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -index * (ButtonSize + ButtonGap));
        rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);

        Button button = circle.gameObject.AddComponent<Button>();
        button.onClick.AddListener(() =>
        {
            if (IsAnyPanelOpen()) return;
            SoundManager.Instance?.PlaySFX(SoundId.UI_Click);
            rect.DOKill();
            rect.localScale = Vector3.one;
            rect.DOPunchScale(Vector3.one * 0.12f, 0.25f, 6, 0.6f).SetLink(rect.gameObject);
            onClick();
        });

        Image iconImage = kit.Image(rect, "Icon", icon, Color.white);
        LobbyUiKit.Stretch(iconImage.rectTransform);
        iconImage.rectTransform.offsetMin = new Vector2(18f, 18f);
        iconImage.rectTransform.offsetMax = new Vector2(-18f, -18f);

        TMP_Text text = kit.Text(rect, "Label", "", 34f, kit.outlineMaterial);
        LocalizedTextBinder.Bind(text, labelKey, labelFallback);  //언어를 바꾸면 버튼 이름도 바뀜
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = new Vector2(0.5f, 0f);
        textRect.anchorMax = new Vector2(0.5f, 0f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(0f, 4f);
        textRect.sizeDelta = new Vector2(ButtonSize + 40f, 56f);
        text.enableAutoSizing = true;
        text.fontSizeMin = 22f;
        text.fontSizeMax = 34f;

        return button;
    }

    private void OpenDaily()
    {
        DailyRewardPopup.Show(popupParent);
    }

    private GameObject CreateDot(RectTransform parent)
    {
        Image dot = kit.Image(parent, "RedDot", kit.circleButton != null ? kit.circleButton : null, new Color(1f, 0.25f, 0.25f));
        RectTransform rect = dot.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-14f, -14f);
        rect.sizeDelta = new Vector2(38f, 38f);
        rect.DOScale(1.18f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(dot.gameObject);
        return dot.gameObject;
    }

    private void RefreshDot()
    {
        if (dailyDot != null) dailyDot.SetActive(DailyRewardService.HasAnythingToClaim);
    }

    private void Update()
    {
        if (popupParent == null || SaveManager.Instance == null) return;
        if (LoadingScreenManager.Instance != null && LoadingScreenManager.Instance.IsLoading) return;

        if (IsAnyPanelOpen() || TutorialGuideUI.IsShowing)
        {
            idleTimer = 0f;
            return;
        }

        idleTimer += Time.unscaledDeltaTime;
        if (idleTimer < 1f) return;

        // 첫 인사 튜토리얼이 먼저 (안내가 끝나기 전엔 다른 창을 띄우지 않음)
        if (!TutorialProgress.IsDone(TutorialStep.LobbyWelcome)) return;

        if (!dailyAutoShownThisSession && DailyRewardService.CanClaimAttendance)
        {
            dailyAutoShownThisSession = true;
            DailyRewardPopup.Show(popupParent);
        }
    }

    private static bool IsAnyPanelOpen()
    {
        foreach (string panelName in BlockingPanels)
        {
            GameObject panel = GameObject.Find(panelName);
            if (panel != null && panel.activeInHierarchy) return true;
        }

        return false;
    }
}
