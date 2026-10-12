using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 전투 튜토리얼 진행 (TutorialMode가 켜진 스테이지 씬에 자동으로 생긴다). 방 구성은 EnemySpawner.SpawnTutorialRoom.
// 1방: 소개 → 드래그 발사 → 대쉬(해낼 때까지 세계 정지) → 퍼펙트 반사(노란 링이 뜨면 슬로모션, 성공할 때까지) → 증강 / 2방: 특수 적 소개 → 스킬(해낼 때까지 세계 정지)
// 3방: 엘리트 → 증강 스킬 / 4방: 보스 → 완료(처음이면 골드) → 로비. 화면 위 '나가기'로 언제든 로비로.
// 세계 정지: Time.timeScale = 0을 유지 (일시정지 해제/히트스톱 등이 풀어도 매 프레임 다시 멈춤).
//   대쉬 조준이 시작되는 순간에는 정지를 풀어 줌 → 대쉬가 쓰는 TimeFreeze가 배속 1을 기억하도록
public class CombatTutorialGuide : MonoBehaviour
{
    public static CombatTutorialGuide Instance { get; private set; }

    private enum Hold { None, Dash, Skill }

    private const float PerfectSlowScale = 0.35f;  //퍼펙트 연습: 벽에 다가갈 때 이 배속 (판정은 실제 시간 기준이라 쉬워짐)
    private const int PerfectMaxMisses = 5;         //이만큼 놓치면 넘어감
    private const float PerfectMaxPracticeTime = 30f; //실제 시간. 아예 안 눌러도 이만큼 지나면 넘어감 (발사가 바뀌어도 누적)
    private const int BossRoom = 4;                   //보스 방까지 못 배운 단계는 건너뜀 (보스 산데비스탄 감속과 겹치지 않게)

    private bool introDone, launchShown, dashDone, perfectDone, augmentDone, enemiesDone, skillDone, eliteDone, skillAugmentDone, bossDone, finished;
    private bool launched;
    private bool leaving;
    private bool perfectStarted;
    private float baseFixedDeltaTime = 0.02f;
    private PlayerDash dash;
    private bool dashStepActive, skillStepActive, perfectStepActive;
    private int perfectMisses;
    private float perfectPracticeTime;
    private bool perfectSlowApplied;
    private PlayerPerfectBounce perfectBounce;
    private Hold hold;
    private float stateTimer;
    private float playingTimer;
    private GameState lastState;
    private int lastRoom;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded += (scene, mode) => Spawn();
        Spawn();
    }

    private static void Spawn()
    {
        if (!TutorialMode.IsActive || FindAnyObjectByType<RoomManager>() == null) return;
        if (FindAnyObjectByType<CombatTutorialGuide>() != null) return;

        new GameObject("CombatTutorialGuide").AddComponent<CombatTutorialGuide>();
    }

    private void Awake()
    {
        Instance = this;
        baseFixedDeltaTime = Time.fixedDeltaTime;
        PlayerBallController.OnPlayerLaunched += HandleLaunched;
        PlayerDash.OnAimStarted += HandleDashAimStarted;
        PlayerDash.OnDashPerformed += HandleDashPerformed;
        GameEvents.SkillUsed += HandleSkillUsed;
        PlayerPerfectBounce.OnPerfect += HandlePerfect;
        PlayerPerfectBounce.OnMiss += HandleMiss;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        PlayerBallController.OnPlayerLaunched -= HandleLaunched;
        PlayerDash.OnAimStarted -= HandleDashAimStarted;
        PlayerDash.OnDashPerformed -= HandleDashPerformed;
        GameEvents.SkillUsed -= HandleSkillUsed;
        PlayerPerfectBounce.OnPerfect -= HandlePerfect;
        PlayerPerfectBounce.OnMiss -= HandleMiss;
        EndPerfectSlow();
        ReleaseHold();
    }

    private void Start()
    {
        CreateExitButton();
    }

    private void Update()
    {
        if (finished || GameManager.Instance == null || RoomManager.Instance == null) return;
        if (LoadingScreenManager.Instance != null && LoadingScreenManager.Instance.IsLoading) return;

        GameState state = GameManager.Instance.currentState;
        int room = RoomManager.Instance.CurrentRoom;

        if (state != lastState || room != lastRoom)
        {
            stateTimer = 0f;
            lastState = state;
            lastRoom = room;
        }

        stateTimer += Time.unscaledDeltaTime;
        if (state == GameState.Playing && hold == Hold.None && !TimeFreeze.IsActive) playingTimer += Time.unscaledDeltaTime;

        MaintainHold(state);
        MaintainPerfectSlow(state);

        if (TutorialGuideUI.IsShowing || RunPopupUI.IsOpen) return;

        switch (state)
        {
            case GameState.Ready: UpdateReady(room); break;
            case GameState.Playing: UpdatePlaying(room); break;
            case GameState.AugmentSelect: UpdateAugment(room); break;
        }
    }

    // ───────── 방 시작 (발사 전) ─────────

    private void UpdateReady(int room)
    {
        if (stateTimer < 0.4f || !RoomManager.Instance.HasActiveEnemies) return;

        if (room == 1 && !introDone)
        {
            introDone = true;
            Say(true, 0.66f, null,
                ("tut_intro_1", "전투 연습장에 온 걸 환영해! 내가 싸우는 법을 하나씩 알려줄게."),
                ("tut_intro_2", "언제든 왼쪽 위 '나가기'로 로비에 돌아갈 수 있어."));
            return;
        }

        if (room == 1 && !launchShown && !launched)
        {
            launchShown = true;
            TutorialGuideUI.Play(new TutorialGuideUI.Request
            {
                lineKeys = new[] { "tut_launch_1" },
                fallbacks = new[] { "나를 꾹 누르고 날아갈 반대쪽으로 쭉 당겼다 놓아 봐! 많이 당길수록 세게 날아가." },
                blocking = false,
                anchorY = 0.74f,
                dragDemo = true,
                dragFrom = PlayerViewportPosition,
                dragDelta = new Vector2(0f, -0.13f),
                completeWhen = () => launched,
            });
            return;
        }

        if (room == 2 && !enemiesDone)
        {
            enemiesDone = true;
            Say(true, 0.7f, null,
                ("tut_enemy_1", "이번엔 특별한 몬스터들이야. 하나씩 알아 두자!"),
                ("tut_enemy_ranged", "멀리서 총알을 쏘는 몬스터는 탄을 피하면서 먼저 쓰러뜨려."),
                ("tut_enemy_shield", "방패 몬스터는 정면이 단단해. 옆이나 뒤를 노려 봐!"),
                ("tut_enemy_spiky", "가시 몬스터에 부딪히면 나도 따끔해. 퍼펙트 반사나 스킬로 공격하면 안전해."),
                ("tut_enemy_bomber", "폭탄 몬스터는 쓰러질 때 터져서 주변 몬스터에게 피해를 줘. 무리 가운데서 터뜨리자!"));
            return;
        }

        if (room == 3 && !eliteDone && stateTimer > 0.8f)
        {
            eliteDone = true;
            Say(true, 0.7f, null,
                ("tut_elite_1", "크고 강한 엘리트 몬스터야! 분열, 순간이동, 재생, 질주 중 특성을 하나 갖고 있어."),
                ("tut_elite_2", "엘리트를 쓰러뜨리면 특별한 증강 스킬을 고를 수 있어. 힘내!"));
            return;
        }

        if (room == 4 && !bossDone && stateTimer > 1.2f)
        {
            bossDone = true;
            Say(true, 0.66f, null,
                ("tut_boss_1", "드디어 보스야! 위쪽에 보스 체력바가 보이지?"),
                ("tut_boss_2", "바닥에 빨간 경고가 보이면 얼른 피해! 공격이 끝난 틈이 기회야."),
                ("tut_boss_3", "체력이 절반 아래로 내려가면 보스가 화나서 더 빨라져. 조심해!"));
        }
    }

    // ───────── 날아가는 중 ─────────

    private void UpdatePlaying(int room)
    {
        if (room >= BossRoom)
        {
            dashDone = perfectDone = skillDone = true;
            return;
        }

        if (!dashDone && !dashStepActive && launched && playingTimer > 0.5f && !TimeFreeze.IsActive)
        {
            BeginDashStep();
            return;
        }

        if (dashDone && !perfectDone && !perfectStepActive && playingTimer > 0.8f)
        {
            BeginPerfectStep();
            return;
        }

        if (room >= 2 && dashDone && perfectDone && !skillDone && !skillStepActive && playingTimer > 0.6f && !TimeFreeze.IsActive)
        {
            BeginSkillStep();
        }
    }

    private void BeginDashStep()
    {
        dashStepActive = true;
        if (dash == null) dash = FindAnyObjectByType<PlayerDash>();
        dash?.RefillCharges();
        SetHold(Hold.Dash);

        TutorialGuideUI.Play(new TutorialGuideUI.Request
        {
            lineKeys = new[] { "tut_dash_1", "tut_dash_2" },
            fallbacks = new[] { "잠깐 멈춰! 이번엔 대쉬를 배워 보자.", "화면 왼쪽 아래를 꾹 누른 채로 가고 싶은 방향으로 끌었다가 놓아 봐!" },
            blocking = false,
            anchorY = 0.72f,
            holdDemo = true,
            holdAt = new Vector2(0.3f, 0.2f),
            autoAdvanceSeconds = 2.2f,
            holdLastLine = true,
            completeWhen = () => dashDone,
        });
    }

    private void BeginPerfectStep()
    {
        perfectBounce = FindAnyObjectByType<PlayerPerfectBounce>();
        if (perfectBounce == null)
        {
            perfectDone = true;
            return;
        }

        perfectStepActive = true;
        if (!perfectStarted)  //공이 멈춰 다시 쏴도 놓친 횟수/연습 시간은 이어서 셈
        {
            perfectStarted = true;
            perfectMisses = 0;
            perfectPracticeTime = 0f;
        }

        bool firstTime = perfectPracticeTime <= 0f && perfectMisses == 0;  //다시 쏠 때는 핵심 한 줄만
        TutorialGuideUI.Play(new TutorialGuideUI.Request
        {
            lineKeys = firstTime ? new[] { "tut_perfect_1", "tut_perfect_2" } : new[] { "tut_perfect_2" },
            fallbacks = firstTime
                ? new[] { "이번엔 '퍼펙트 반사'야! 벽에 가까워지면 공 둘레에 노란 링이 줄어들어.", PerfectHowTo }
                : new[] { PerfectHowTo },
            blocking = false,
            anchorY = 0.74f,
            autoAdvanceSeconds = 3f,
            holdLastLine = true,
            completeWhen = () => perfectDone,
        });
    }

    private const string PerfectHowTo = "링이 공에 딱 닿아 벽에 부딪히는 순간 화면 왼쪽 아래를 톡! 성공하면 다음 공격이 강해지고 잠깐 빨라져.";

    private void MaintainPerfectSlow(GameState state)  //연습 중: 노란 링이 뜨는 동안만 슬로모션
    {
        if (!perfectStepActive || perfectDone) return;

        if (state != GameState.Playing)  //방이 끝나면 다음 방에서 이어서
        {
            if (state != GameState.Paused) { perfectStepActive = false; EndPerfectSlow(); TutorialGuideUI.Stop(); }
            return;
        }

        if (TimeFreeze.IsActive || hold != Hold.None) return;

        perfectPracticeTime += Time.unscaledDeltaTime;
        if (perfectPracticeTime > PerfectMaxPracticeTime) { SkipPerfectStep(); return; }

        bool nearWall = perfectBounce != null && perfectBounce.TimeToWall >= 0f;
        if (nearWall && Time.timeScale > PerfectSlowScale)
        {
            Time.timeScale = PerfectSlowScale;
            Time.fixedDeltaTime = baseFixedDeltaTime * PerfectSlowScale;  //물리도 같이 촘촘하게 (끊겨 보이지 않게)
            perfectSlowApplied = true;
        }
        else if (!nearWall && perfectSlowApplied) EndPerfectSlow();
    }

    private void EndPerfectSlow()
    {
        if (!perfectSlowApplied) return;

        perfectSlowApplied = false;
        Time.fixedDeltaTime = baseFixedDeltaTime;
        if (!TimeFreeze.IsActive && Mathf.Approximately(Time.timeScale, PerfectSlowScale)) Time.timeScale = 1f;
    }

    private void HandlePerfect(Vector3 position, int chain)
    {
        if (!perfectStepActive || perfectDone) return;

        perfectDone = true;
        perfectStepActive = false;
        EndPerfectSlow();
        playingTimer = 0f;

        TutorialGuideUI.Stop();
        TutorialGuideUI.Play(new TutorialGuideUI.Request
        {
            lineKeys = new[] { "tut_perfect_ok" },
            fallbacks = new[] { "퍼펙트! 연속으로 성공하면 더 강해져. 이제 몬스터를 모두 쓰러뜨려 봐!" },
            blocking = false,
            anchorY = 0.74f,
            autoAdvanceSeconds = 3f,
        });
    }

    private void HandleMiss(Vector3 position)
    {
        if (!perfectStepActive || perfectDone) return;

        perfectMisses++;
        if (perfectMisses >= PerfectMaxMisses) SkipPerfectStep();
    }

    private void SkipPerfectStep()  //너무 어려우면 넘어감 (실전에서 천천히 익히면 됨)
    {
        perfectDone = true;
        perfectStepActive = false;
        EndPerfectSlow();
        TutorialGuideUI.Stop();
        playingTimer = 0f;
    }

    private void BeginSkillStep()
    {
        PlayerSkill skill = FindAnyObjectByType<PlayerSkill>();
        if (skill == null)
        {
            skillDone = true;
            return;
        }

        skill.ReduceCooldown(9999f);  //바로 쓸 수 있게 (충전형 스킬은 이미 차 있음)
        if (!skill.IsSkillReady)
        {
            skillDone = true;
            return;
        }

        skillStepActive = true;
        SetHold(Hold.Skill);

        TutorialGuideUI.Play(new TutorialGuideUI.Request
        {
            lineKeys = new[] { "tut_skill_1", "tut_skill_2" },
            fallbacks = new[] { "멈춰! 오른쪽 아래 버튼은 캐릭터마다 다른 필살기야.", "버튼이 다 차올랐어. 지금 눌러 봐!" },
            blocking = false,
            anchorY = 0.66f,
            pointTarget = FindSkillButton(),
            autoAdvanceSeconds = 2.2f,
            holdLastLine = true,
            completeWhen = () => skillDone,
        });
    }

    // ───────── 방 클리어 후 ─────────

    private void UpdateAugment(int room)
    {
        if (stateTimer < 0.35f) return;

        if (!augmentDone)
        {
            augmentDone = true;
            Say(true, 0.86f, null,
                ("tut_augment_1", "방을 다 정리하면 증강을 하나 고를 수 있어."),
                ("tut_augment_2", "마음에 드는 카드를 골라 봐. 같은 증강을 또 고르면 더 강해져!"));
            return;
        }

        if (room == 3 && !skillAugmentDone)
        {
            skillAugmentDone = true;
            Say(true, 0.86f, null,
                ("tut_skill_augment_1", "엘리트를 이겨서 증강 스킬이 나왔어!"),
                ("tut_skill_augment_2", "고르면 스킬 버튼이 하나 더 생기고, 같은 스킬을 또 고르면 레벨이 올라가."));
        }
    }

    public void OnTutorialCleared()  //RoomManager: 마지막(보스) 방 클리어
    {
        if (finished) return;
        finished = true;
        ReleaseHold();

        EndPerfectSlow();

        int gold = TutorialMode.Complete();
        string rewardLine = gold > 0
            ? string.Format(LobbyUiKit.Loc("tut_done_reward", "첫 완료 선물로 {0} 골드를 줄게. 상점에서 강해져 봐!"), gold)
            : LobbyUiKit.Loc("tut_done_again", "다시 봐도 잘하네! 이제 진짜 모험을 떠나 보자.");

        TutorialGuideUI.Play(new TutorialGuideUI.Request
        {
            lineKeys = new[] { "tut_done_1", null },
            fallbacks = new[] { "대단해! 튜토리얼을 모두 마쳤어!", rewardLine },
            blocking = true,
            anchorY = 0.62f,
            onFinished = Leave,
        });

        if (!TutorialGuideUI.IsShowing) Leave();  //안내를 띄우지 못했으면 바로 로비로 (막히지 않게)
    }

    private void Leave()
    {
        if (leaving) return;
        leaving = true;
        TutorialMode.LeaveStage();
    }

    // ───────── 세계 정지 ─────────

    private void SetHold(Hold type)
    {
        hold = type;
        Time.timeScale = 0f;
    }

    private void ReleaseHold()
    {
        if (hold == Hold.None) return;

        hold = Hold.None;
        if (!TimeFreeze.IsActive && Time.timeScale == 0f && (GameManager.Instance == null || GameManager.Instance.currentState != GameState.Paused)) Time.timeScale = 1f;
    }

    private void MaintainHold(GameState state)
    {
        // 대쉬 조준을 시작했다가 끌지 않고 놓은 경우: 다시 멈추고 기다림
        if (dashStepActive && !dashDone && hold == Hold.None && !TimeFreeze.IsActive && state == GameState.Playing) hold = Hold.Dash;

        if (hold == Hold.None) return;

        if (state != GameState.Playing)  //방 클리어 등으로 전투가 끝나면 단계 취소 (다음 기회에)
        {
            if (state == GameState.Paused) return;

            if (hold == Hold.Dash) dashStepActive = false;
            if (hold == Hold.Skill) skillStepActive = false;
            hold = Hold.None;
            TutorialGuideUI.Stop();
            return;
        }

        if (hold == Hold.Dash) dash?.RefillCharges();  //오래 눌러 게이지를 써 버려도 다시 대쉬할 수 있게
        if (!TimeFreeze.IsActive && Time.timeScale != 0f) Time.timeScale = 0f;  //일시정지 해제/히트스톱 등이 배속을 되돌려도 다시 멈춤
    }

    private void HandleLaunched()
    {
        launched = true;
        playingTimer = 0f;
    }

    private void HandleDashAimStarted()
    {
        if (hold != Hold.Dash) return;

        hold = Hold.None;
        Time.timeScale = 1f;  //TimeFreeze.Begin이 바로 다시 멈추고 이 배속을 기억함
    }

    private void HandleDashPerformed()
    {
        if (!dashStepActive) return;

        dashStepActive = false;
        dashDone = true;
        playingTimer = 0f;
    }

    private void HandleSkillUsed()
    {
        if (!skillStepActive) return;

        skillStepActive = false;
        skillDone = true;
        ReleaseHold();
    }

    // ───────── 나가기 버튼 ─────────

    private void CreateExitButton()
    {
        LobbyUiKit kit = LobbyUiKit.Load();
        if (kit == null) return;

        GameObject canvasObject = new GameObject("TutorialExitCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 510;  //고양이 안내(500) 위: 막는 안내 중에도 누를 수 있게
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        canvasObject.AddComponent<AdaptiveCanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform safeArea = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
        safeArea.SetParent(canvasObject.transform, false);
        LobbyUiKit.Stretch(safeArea);
        safeArea.gameObject.AddComponent<SafeAreaFitter>();

        Button button = kit.Button(safeArea, "TutorialExitButton", kit.grayButton, new Vector2(190f, 78f), Exit);
        RectTransform rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -126f);  //위쪽 통합 바(높이 86) 아래

        TMP_Text label = kit.Text(rect, "Label", LobbyUiKit.Loc("tut_exit", "나가기"), 34f, kit.outlineMaterial);
        LobbyUiKit.Stretch(label.rectTransform);
        label.enableAutoSizing = true;
        label.fontSizeMin = 22f;
        label.fontSizeMax = 34f;
    }

    public void Exit()
    {
        if (leaving) return;
        leaving = true;
        finished = true;

        EndPerfectSlow();
        ReleaseHold();
        TimeFreeze.End();
        TutorialGuideUI.Stop();
        Time.timeScale = 1f;

        TutorialMode.ExitToLobby();
    }

    // ───────── 도우미 ─────────

    private static void Say(bool blocking, float anchorY, RectTransform target, params (string key, string fallback)[] lines)
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
            lineKeys = keys,
            fallbacks = fallbacks,
            blocking = blocking,
            anchorY = anchorY,
            pointTarget = target,
        });
    }

    private static RectTransform FindSkillButton()  //2방에서는 캐릭터 스킬 칸 하나만 차 있음
    {
        foreach (SkillSlotUI slot in FindObjectsByType<SkillSlotUI>(FindObjectsSortMode.None))
        {
            if (!slot.IsEmpty && slot.gameObject.activeInHierarchy) return (RectTransform)slot.transform;
        }

        return null;
    }

    private static Vector2 PlayerViewportPosition()
    {
        GameObject player = GameObject.FindWithTag("Player");
        Camera camera = Camera.main;
        if (player == null || camera == null) return new Vector2(0.5f, 0.25f);

        Vector3 viewport = camera.WorldToViewportPoint(player.transform.position);
        return new Vector2(viewport.x, viewport.y);
    }
}
