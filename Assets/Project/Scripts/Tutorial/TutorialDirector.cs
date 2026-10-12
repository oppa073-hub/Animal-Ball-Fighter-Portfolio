using UnityEngine;
using UnityEngine.SceneManagement;

// 처음 하는 유저에게 상황에 맞춰 고양이 안내(TutorialGuideUI)를 띄운다. 씬마다 자동으로 생긴다.
// 로비 안내와 실제 판의 로그라이크 안내(축복/갈림길/특수 방)만 맡는다. 전투 조작은 전투 튜토리얼(TutorialMode)에서 배운다.
// 각 안내는 한 번만 나오고(세이브에 기록), 튜토리얼 추가 전부터 하던 유저는 SaveManager가 전부 본 것으로 처리한다.
public class TutorialDirector : MonoBehaviour
{
    private const string LobbySceneName = "MainLobby";

    private static readonly string[] LobbyBlockingPanels =
    {
        "NicknamePanel", "PrivacyConsentPanel", "LanguagePanel", "GoldPurchasePopup", "SoundSettingsPanel", "DailyRewardPopup",
        "RankingPopup", "CodexPopup", MiscSettingsMenu.PanelName,
    };

    private bool isLobby;
    private float readyTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded += (scene, mode) => Spawn();
        Spawn();
    }

    private static void Spawn()
    {
        if (FindAnyObjectByType<TutorialDirector>() != null) return;
        new GameObject("TutorialDirector").AddComponent<TutorialDirector>();
    }

    private void Awake()
    {
        isLobby = SceneManager.GetActiveScene().name == LobbySceneName;

        TutorialEvents.Triggered += HandleTriggered;
    }

    private void OnDestroy()
    {
        TutorialEvents.Triggered -= HandleTriggered;
    }

    private void Update()
    {
        if (SaveManager.Instance == null || TutorialGuideUI.IsShowing) return;
        if (LoadingScreenManager.Instance != null && LoadingScreenManager.Instance.IsLoading) return;

        if (isLobby) UpdateLobby();  //전투 안내(발사/대쉬/퍼펙트/스킬/증강/적/보스)는 전투 튜토리얼(CombatTutorialGuide)이 맡음
    }

    // ── 로비 ──

    private void UpdateLobby()
    {
        if (IsAnyLobbyPanelOpen())
        {
            readyTimer = 0f;
            return;
        }

        readyTimer += Time.unscaledDeltaTime;
        if (readyTimer < 0.6f) return;

        if (!TutorialProgress.IsDone(TutorialStep.LobbyWelcome))
        {
            Play(TutorialStep.LobbyWelcome, true, 0.42f, FindRect("StartButton"),
                ("tutorial_lobby_1", "안녕! 나는 고양이야. 애니멀 볼 파이트에 온 걸 환영해!"),
                ("tutorial_lobby_2", "우리는 공처럼 통통 튀면서 몬스터를 물리칠 거야."),
                ("tutorial_lobby_3", "준비됐으면 아래의 시작 버튼을 눌러 모험을 떠나 보자!"));
            return;
        }

        // 한 판 해 보고 골드가 모였으면 강화 안내
        PlayerSaveData data = SaveManager.Instance.Data;
        if (!TutorialProgress.IsDone(TutorialStep.LobbyUpgrade) && data.stageRecords.Count > 0 && data.gold >= 50)
        {
            Play(TutorialStep.LobbyUpgrade, true, 0.5f, FindRect("ShopButton"),
                ("tutorial_upgrade_1", "골드를 꽤 모았네! 대단해!"),
                ("tutorial_upgrade_2", "상점에서 체력, 공격력, 대쉬를 강화하면 훨씬 쉬워져. 한번 가 볼까?"));
        }
    }

    private bool IsAnyLobbyPanelOpen()
    {
        foreach (string panelName in LobbyBlockingPanels)
        {
            GameObject panel = GameObject.Find(panelName);
            if (panel != null && panel.activeInHierarchy) return true;
        }

        return false;
    }

    // 팝업이 뜬 순간 (RunFlow 등에서 알림)
    private void HandleTriggered(TutorialStep step)
    {
        if (SaveManager.Instance == null || TutorialProgress.IsDone(step) || TutorialGuideUI.IsShowing) return;

        switch (step)
        {
            case TutorialStep.Blessing:
                Play(step, true, 0.86f, null,
                    ("tutorial_blessing_1", "모험을 시작할 때 축복을 하나 받을 수 있어."),
                    ("tutorial_blessing_2", "이번 판 내내 도와주니까 신중하게 골라!"));
                break;

            case TutorialStep.Door:
                Play(step, true, 0.86f, null,
                    ("tutorial_door_1", "갈림길이야! 다음에 갈 방을 직접 고를 수 있어."),
                    ("tutorial_door_2", "빨간 방은 강한 엘리트, 노란 방은 상점, 초록 방은 쉬어 가는 모닥불이야."));
                break;

            case TutorialStep.SpecialRoom:
                Play(step, true, 0.86f, null,
                    ("tutorial_special_1", "특별한 방이네! 이번 판에서 모은 골드로 도움을 받을 수 있어."),
                    ("tutorial_special_2", "판이 끝나면 런 골드는 사라지니까 아끼지 말고 써!"));
                break;
        }
    }

    // ── 도우미 ──

    private static void Play(TutorialStep step, bool blocking, float anchorY, RectTransform target, params (string key, string fallback)[] lines)
    {
        string[] keys = new string[lines.Length];
        string[] fallbacks = new string[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            keys[i] = lines[i].key;
            fallbacks[i] = lines[i].fallback;
        }

        TutorialGuideUI.Play(new TutorialGuideUI.Request
        {
            step = step,
            lineKeys = keys,
            fallbacks = fallbacks,
            blocking = blocking,
            anchorY = anchorY,
            pointTarget = target,
        });
    }

    private static RectTransform FindRect(string objectName)
    {
        GameObject go = GameObject.Find(objectName);
        return go != null ? go.transform as RectTransform : null;
    }
}
