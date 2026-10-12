using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 로비 사이드 메뉴(소리/광고 골드/언어) 맨 아래에 '기타' 버튼을 붙이고, 누르면 기타 창을 연다. 로비 씬에 자동으로 생긴다.
// 기타 창: 튜토리얼 다시 보기 / 개인정보 설정
// - 튜토리얼 다시 보기: 전투 튜토리얼(TutorialMode)을 다시 연다 (완료 골드는 처음 한 번만)
// - 개인정보 설정: 기존 PrivacySettingsButton.OpenConsent 흐름 그대로 (동의 창 열기 + 변경 시 재시작 안내)
public class MiscSettingsMenu : MonoBehaviour
{
    public const string PanelName = "MiscSettingsPanel";  //튜토리얼/자동 팝업이 열려 있는 동안 끼어들지 않도록 이름으로 확인함

    private const string LobbySceneName = "MainLobby";
    private const float SideButtonStep = 110f;  //사이드 메뉴 버튼 높이 100 + 간격 10

    private LobbyUiKit kit;
    private Transform popupParent;
    private PrivacySettingsButton privacy;
    private GameObject panel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded += (scene, mode) => Spawn();
        Spawn();
    }

    private static void Spawn()
    {
        if (SceneManager.GetActiveScene().name != LobbySceneName) return;
        if (FindAnyObjectByType<MiscSettingsMenu>() != null) return;

        new GameObject("MiscSettingsMenu").AddComponent<MiscSettingsMenu>();
    }

    private void Start()
    {
        kit = LobbyUiKit.Load();
        RectTransform sideMenu = FindRect("SideMenu");  //사이드 메뉴는 처음에 꺼져 있어 GameObject.Find로 못 찾음
        RectTransform template = FindRect("LanguageButton");
        RectTransform safeArea = FindRect("SafeArea");
        if (kit == null || sideMenu == null || template == null || safeArea == null)
        {
            Debug.LogWarning("[MiscSettingsMenu] LobbyUiKit 또는 로비 사이드 메뉴를 찾지 못해 기타 버튼을 만들지 않습니다.");
            return;
        }

        popupParent = safeArea;
        privacy = gameObject.AddComponent<PrivacySettingsButton>();

        // 언어 버튼을 복제해 같은 모양으로 맨 아래에 붙임 (씬에 연결된 언어 창 열기 동작은 지우고 새로 연결)
        GameObject buttonObject = Instantiate(template.gameObject, sideMenu);
        buttonObject.name = "MiscSettingsButton";
        buttonObject.transform.SetAsLastSibling();

        // 복제본의 UIButtonSfx는 메뉴가 켜져 있었으면 이미 Awake에서 등록돼 아래 교체로 지워지고, 꺼져 있었으면 나중에 등록돼 남는다.
        // 어느 쪽이든 클릭음이 한 번만 나도록 떼어 내고 직접 낸다.
        UIButtonSfx sfx = buttonObject.GetComponent<UIButtonSfx>();
        if (sfx != null) DestroyImmediate(sfx);

        Button button = buttonObject.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(() =>
        {
            SoundManager.Instance?.PlaySFX(SoundId.UI_Click);
            Open();
        });

        Image icon = buttonObject.transform.childCount > 0 ? buttonObject.transform.GetChild(0).GetComponent<Image>() : null;
        if (icon != null && kit.settingIcon != null) icon.sprite = kit.settingIcon;

        sideMenu.sizeDelta += new Vector2(0f, SideButtonStep);
    }

    private void Open()
    {
        FindAnyObjectByType<LobbySideMenu>()?.Close();

        if (panel != null) Destroy(panel);

        RectTransform root = kit.Popup(popupParent, PanelName, LobbyUiKit.Loc("settings_misc", "기타"), new Vector2(700f, 560f), Close, out RectTransform content);
        panel = root.gameObject;

        CreateMenuButton(content, "TutorialReplayButton", kit.yellowButton, LobbyUiKit.Loc("settings_tutorial_replay", "튜토리얼 다시 보기"), 75f, ReplayTutorial);
        CreateMenuButton(content, "PrivacySettingsButton", kit.skyButton, LobbyUiKit.Loc("settings_privacy", "개인정보 설정"), -75f, OpenPrivacy);
    }

    private void Close()
    {
        if (panel != null) Destroy(panel);
        panel = null;
    }

    private void CreateMenuButton(RectTransform parent, string name, Sprite sprite, string text, float y, System.Action onClick)
    {
        Button button = kit.Button(parent, name, sprite, new Vector2(500f, 120f), onClick);
        RectTransform rect = (RectTransform)button.transform;
        rect.anchoredPosition = new Vector2(0f, y);

        TMP_Text label = kit.Text(rect, "Label", text, 42f, kit.outlineMaterial);
        LobbyUiKit.Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(16f, 0f);
        label.rectTransform.offsetMax = new Vector2(-16f, 0f);
        label.enableAutoSizing = true;
        label.fontSizeMin = 24f;
        label.fontSizeMax = 42f;
    }

    private void ReplayTutorial()
    {
        Close();
        TutorialMode.Start();
    }

    private void OpenPrivacy()
    {
        Close();  //동의 창과 겹치지 않게 먼저 닫음
        privacy.OpenConsent();
    }

    private static RectTransform FindRect(string objectName)
    {
        foreach (RectTransform rect in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rect.name == objectName && rect.gameObject.scene.name == LobbySceneName) return rect;
        }

        return null;
    }
}
