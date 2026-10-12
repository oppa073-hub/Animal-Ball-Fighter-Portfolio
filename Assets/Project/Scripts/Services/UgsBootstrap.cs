using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using Unity.Services.RemoteConfig;
using UnityEngine;
using UnityEngine.UnityConsent;
using UgsAnalytics = Unity.Services.Analytics.AnalyticsService;
using UgsCustomEvent = Unity.Services.Analytics.CustomEvent;

// Unity Gaming Services 연결 담당 (앱 시작 시 자동 생성, 씬이 바뀌어도 유지).
// 세이브는 기기(PlayerPrefs)에만 저장하고, 여기서는 익명 로그인 뒤 아래만 쓴다.
// 1) Remote Config: 밸런스 배율(RemoteTuning) 받아오기
// 2) Analytics: 개인정보 동의(AdManager에 저장된 값, 읽기만 함)를 EndUserConsent의 AnalyticsIntent로 넘겨, 동의했을 때만 수집
// 3) Leaderboards: 무한 모드 최고 기록 등록/조회
// 네트워크가 없거나 대시보드 설정 전이면 모두 조용히 실패하고 게임은 그대로 진행된다.
public class UgsBootstrap : MonoBehaviour
{
    public static UgsBootstrap Instance { get; private set; }

    public static bool IsSignedIn => Instance != null && Instance.signedIn;

    private bool signedIn;
    private bool servicesReady;
    private ConsentStatus appliedAnalyticsConsent = ConsentStatus.Unspecified;  //EndUserConsent에 마지막으로 넘긴 값
    private int submittedEndlessBest;  //이미 랭킹에 올린 무한 모드 기록

    private ServicesConfig config;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (Instance != null) return;

        GameObject go = new GameObject("UgsBootstrap");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<UgsBootstrap>();
    }

    private async void Start()
    {
        config = ServicesConfig.Load();

        // 세이브 매니저가 생길 때까지 (Bootstrap → 로비)
        while (SaveManager.Instance == null) await Task.Yield();
        SaveManager.Instance.OnSaved += HandleSaved;

        await InitializeAsync();
    }

    private void OnDestroy()
    {
        if (SaveManager.Instance != null) SaveManager.Instance.OnSaved -= HandleSaved;
        if (Instance == this) Instance = null;
    }

    private async Task InitializeAsync()
    {
        try
        {
            await UnityServices.InitializeAsync();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[UGS] 초기화 실패 (오프라인 등): {exception.Message}");
            return;
        }

        servicesReady = true;
        SyncAnalyticsConsent();

        await SignInAsync();
        if (!signedIn) return;

        await FetchRemoteConfigAsync();
        await SyncPlayerNameAsync();
        await SubmitEndlessBestAsync();
    }

    // ── 로그인 (익명) ──

    private async Task SignInAsync()
    {
        IAuthenticationService auth = AuthenticationService.Instance;

        try
        {
            if (!auth.IsSignedIn) await auth.SignInAnonymouslyAsync();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[UGS] 로그인 실패: {exception.Message}");
        }

        signedIn = auth.IsSignedIn;
    }

    // 설정에서 개인정보 동의를 바꾸면 이벤트 기록이 없어도 바로 반영 (SDK 자동 수집 포함)
    private float consentCheckTimer;

    private void Update()
    {
        consentCheckTimer += Time.unscaledDeltaTime;
        if (consentCheckTimer < 1f) return;

        consentCheckTimer = 0f;
        SyncAnalyticsConsent();
    }

    // 무한 모드 최고 기록이 바뀌면 바로 랭킹에 올림
    private void HandleSaved()
    {
        if (signedIn && SaveManager.Instance != null && SaveManager.Instance.Data.bestEndlessRoom > submittedEndlessBest) _ = SubmitEndlessBestAsync();
    }

    // ── Remote Config ──

    private struct UserAttributes { }
    private struct AppAttributes { public string appVersion; }

    private async Task FetchRemoteConfigAsync()
    {
        try
        {
            RuntimeConfig remote = await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes { appVersion = Application.version });

            RemoteTuning.Values defaults = new RemoteTuning.Values();
            RemoteTuning.Apply(new RemoteTuning.Values
            {
                enemyHpMultiplier = remote.GetFloat("enemy_hp_multiplier", defaults.enemyHpMultiplier),
                bossHpMultiplier = remote.GetFloat("boss_hp_multiplier", defaults.bossHpMultiplier),
                bossCooldownMultiplier = remote.GetFloat("boss_cooldown_multiplier", defaults.bossCooldownMultiplier),
                stageGoldMultiplier = remote.GetFloat("stage_gold_multiplier", defaults.stageGoldMultiplier),
            });
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[RemoteConfig] 받기 실패 (지난 값 사용): {exception.Message}");
        }
    }

    // ── Analytics (동의한 경우만) ──

    // 광고 개인정보 선택(AdManager)을 분석 동의로 그대로 넘긴다. 아직 고르지 않았으면 넘기지 않음 (수집 안 함)
    // 옛 방식(Start/Stop 호출)과 섞어 쓰면 SDK가 예외를 던지므로 이 방식만 쓴다.
    private void SyncAnalyticsConsent()
    {
        if (AdManager.Instance == null || !AdManager.Instance.HasSavedPrivacyConsent) return;

        ConsentStatus wanted = AdManager.Instance.SavedPrivacyConsent ? ConsentStatus.Granted : ConsentStatus.Denied;
        if (wanted == appliedAnalyticsConsent) return;

        try
        {
            ConsentState state = EndUserConsent.GetConsentState();
            state.AnalyticsIntent = wanted;
            EndUserConsent.SetConsentState(state);
            appliedAnalyticsConsent = wanted;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Analytics] 동의 상태 전달 실패: {exception.Message}");
        }
    }

    public static void RecordEvent(string eventName, (string key, object value)[] parameters)
    {
        if (Instance == null || !Instance.servicesReady) return;

        Instance.SyncAnalyticsConsent();  //동의가 방금 바뀐 경우
        if (Instance.appliedAnalyticsConsent != ConsentStatus.Granted) return;

        try
        {
            UgsCustomEvent customEvent = new UgsCustomEvent(eventName);
            if (parameters != null)
            {
                foreach ((string key, object value) in parameters) customEvent.Add(key, value);
            }

            UgsAnalytics.Instance.RecordEvent(customEvent);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Analytics] 이벤트 기록 실패 ({eventName}): {exception.Message}");
        }
    }

    // ── 닉네임 (랭킹 표시 이름) ──

    private async Task SyncPlayerNameAsync()
    {
        string nickname = SaveManager.Instance != null ? SaveManager.Instance.Data.nickname : null;
        if (string.IsNullOrWhiteSpace(nickname)) return;

        try
        {
            string current = AuthenticationService.Instance.PlayerName;
            if (current == null || !current.StartsWith(nickname.Replace(' ', '_'))) await AuthenticationService.Instance.UpdatePlayerNameAsync(nickname.Replace(' ', '_'));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[UGS] 닉네임 등록 실패: {exception.Message}");
        }
    }

    // ── 리더보드 ──

    public static async Task SubmitEndlessBestAsync()
    {
        if (!IsSignedIn || SaveManager.Instance == null) return;

        int best = SaveManager.Instance.Data.bestEndlessRoom;
        if (best <= 0) return;

        try
        {
            Instance.submittedEndlessBest = best;  //실패하면 아래에서 되돌려 다음 저장 때 다시 시도
            await LeaderboardsService.Instance.AddPlayerScoreAsync(Instance.config.endlessLeaderboardId, best);
        }
        catch (Exception exception)
        {
            if (Instance != null) Instance.submittedEndlessBest = 0;
            Debug.LogWarning($"[Leaderboard] 기록 등록 실패: {exception.Message}");
        }
    }

    public class RankingEntry
    {
        public int rank;  //1부터
        public string name;
        public int score;
        public bool isMine;
    }

    public static async Task<(List<RankingEntry> top, RankingEntry mine)> FetchEndlessRankingAsync(int limit = 30)
    {
        List<RankingEntry> top = new List<RankingEntry>();
        RankingEntry mine = null;

        if (!IsSignedIn) return (top, null);

        await SubmitEndlessBestAsync();

        string leaderboardId = Instance.config.endlessLeaderboardId;
        string myId = AuthenticationService.Instance.PlayerId;

        try
        {
            LeaderboardScoresPage page = await LeaderboardsService.Instance.GetScoresAsync(leaderboardId, new GetScoresOptions { Limit = limit });
            foreach (LeaderboardEntry entry in page.Results)
            {
                top.Add(new RankingEntry { rank = entry.Rank + 1, name = CleanName(entry.PlayerName), score = (int)entry.Score, isMine = entry.PlayerId == myId });
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Leaderboard] 순위 불러오기 실패: {exception.Message}");
        }

        try
        {
            LeaderboardEntry entry = await LeaderboardsService.Instance.GetPlayerScoreAsync(leaderboardId);
            mine = new RankingEntry { rank = entry.Rank + 1, name = CleanName(entry.PlayerName), score = (int)entry.Score, isMine = true };
        }
        catch (Exception)
        {
            mine = null;  //아직 기록 없음
        }

        return (top, mine);
    }

    private static string CleanName(string playerName)  //"닉네임#1234" → "닉네임"
    {
        if (string.IsNullOrEmpty(playerName)) return "???";
        int hash = playerName.LastIndexOf('#');
        string name = hash > 0 ? playerName.Substring(0, hash) : playerName;
        return name.Replace('_', ' ');
    }
}
