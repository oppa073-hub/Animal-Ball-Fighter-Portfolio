using System;
using Unity.Services.LevelPlay;
using UnityEngine;

public class AdManager : MonoBehaviour
{
    public static AdManager Instance { get; private set; }

    [Header("LevelPlay")]
    [SerializeField] private string appKey;
    [SerializeField] private string rewardedAdUnitId;

    [Header("Reward")]
    [SerializeField] private int rewardedGoldAmount = 500;

    private LevelPlayRewardedAd rewardedAd;
    private Action pendingRewardAction;
    private const string PrivacyConsentKey = "AdsPrivacyConsent";
    private bool initializationStarted;
    private bool isShowingRewardedAd;
    private bool showAfterLoad;
    private bool isRewardedAdLoading;
    public bool HasSavedPrivacyConsent => PlayerPrefs.HasKey(PrivacyConsentKey);

    public bool SavedPrivacyConsent => PlayerPrefs.GetInt(PrivacyConsentKey, 0) == 1;

    public bool IsRewardedAdReady =>
        rewardedAd != null && rewardedAd.IsAdReady();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (!ValidateAdSettings()) return;

        LevelPlay.OnInitSuccess += OnInitSuccess;
        LevelPlay.OnInitFailed += OnInitFailed;

        if (HasSavedPrivacyConsent)
        {
            ApplyPrivacyConsent(SavedPrivacyConsent, false);
        }
        else
        {
            Debug.Log("[Ads] 개인정보 선택을 기다리고 있습니다.");
        }
    }
    private bool ValidateAdSettings()
    {
#if !UNITY_EDITOR
    if (string.IsNullOrWhiteSpace(appKey))
    {
        Debug.LogError(
            "[Ads] App Key가 입력되지 않았습니다."
        );

        return false;
    }
#endif

        if (string.IsNullOrWhiteSpace(rewardedAdUnitId))
        {
            Debug.LogError("[Ads] Rewarded Ad Unit ID가 입력되지 않았습니다.");

            return false;
        }

        return true;
    }

    public void ApplyPrivacyConsent(bool consent, bool saveChoice = true)
    {
        if (saveChoice)
        {
            PlayerPrefs.SetInt( PrivacyConsentKey, consent ? 1 : 0);

            PlayerPrefs.Save();
        }

        // 반드시 LevelPlay.Init보다 먼저 전달
        LevelPlayPrivacySettings.SetGDPRConsent(consent);

        Debug.Log($"[Ads] 개인정보 동의 적용: {consent}");

        if (!initializationStarted)
        {
            InitializeLevelPlay();
        }
    }

    private void InitializeLevelPlay()
    {
        if (initializationStarted) return;

        initializationStarted = true;

#if UNITY_EDITOR
        Debug.Log("[Ads] 에디터 Mock 광고 모드로 초기화");

        LevelPlay.Init("editor");

        // 에디터 Mock 처리
        InitializeRewardedAd();
#else
    Debug.Log("[Ads] 실제 기기 광고 모드로 초기화");

    LevelPlay.Init(appKey);
#endif
    }
    private void OnInitSuccess(LevelPlayConfiguration configuration)
    {
        Debug.Log("[Ads] LevelPlay 초기화 성공");

        InitializeRewardedAd();
    }
    private void OnInitFailed(LevelPlayInitError error)
    {
        initializationStarted = false;
        Debug.LogError($"[Ads] LevelPlay 초기화 실패: {error}");
    }
    private void InitializeRewardedAd()
    {
        // 초기화 성공 이벤트와 에디터 코드에서 중복 생성되는 것 방지
        if (rewardedAd != null)
        {
            return;
        }

        Debug.Log("[Ads] Rewarded 광고 객체 생성");

        CreateRewardedAd();
        LoadRewardedAd();
    }
    private void CreateRewardedAd()
    {
        rewardedAd = new LevelPlayRewardedAd(rewardedAdUnitId);

        rewardedAd.OnAdLoaded += OnRewardedLoaded;
        rewardedAd.OnAdLoadFailed += OnRewardedLoadFailed;
        rewardedAd.OnAdDisplayed += OnRewardedDisplayed;
        rewardedAd.OnAdDisplayFailed += OnRewardedDisplayFailed;
        rewardedAd.OnAdRewarded += OnRewarded;
        rewardedAd.OnAdClosed += OnRewardedClosed;
        rewardedAd.OnAdClicked += OnRewardedClicked;
        rewardedAd.OnAdInfoChanged += OnRewardedInfoChanged;
    }

    public void LoadRewardedAd()
    {
        if (rewardedAd == null)
        {
            Debug.LogWarning("[Ads] Rewarded 광고 객체가 아직 생성되지 않았습니다.");
            return;
        }


        if (isRewardedAdLoading || rewardedAd.IsAdReady()) return;

        isRewardedAdLoading = true;

        Debug.Log("[Ads] Rewarded 광고 로드 요청");

        rewardedAd.LoadAd();
    }

    public void ShowRewardedAd(Action rewardAction)
    {
        if (isShowingRewardedAd)
        {
            Debug.LogWarning("[Ads] 이미 광고를 표시하고 있습니다.");
            return;
        }

        pendingRewardAction = rewardAction;
        showAfterLoad = true;

        if (rewardedAd == null)
        {
            Debug.LogWarning("[Ads] SDK 초기화 대기 또는 재시도");

            if (!initializationStarted && HasSavedPrivacyConsent)
            {
                ApplyPrivacyConsent(SavedPrivacyConsent, false);
            }

            return;
        }

        if (!rewardedAd.IsAdReady())
        {
            Debug.LogWarning("[Ads] 광고 로드 대기");
            LoadRewardedAd();
            return;
        }

        ShowLoadedRewardedAd();
    }
    private void ShowLoadedRewardedAd()
    {
        if (rewardedAd == null || !rewardedAd.IsAdReady()) return;

        showAfterLoad = false;
        isShowingRewardedAd = true;

        Debug.Log("[Ads] Rewarded 광고 표시 요청");

        rewardedAd.ShowAd();
    }

    public void ShowGoldRewardedAd()
    {
        ShowRewardedAd(() =>
        {
            SaveManager.Instance.AddGold(rewardedGoldAmount);

            Debug.Log($"[Ads] 광고 보상 골드 {rewardedGoldAmount} 지급");
        });
    }

    private void OnRewardedLoaded(LevelPlayAdInfo adInfo)
    {
        isRewardedAdLoading = false;
        Debug.Log($"[Ads] Rewarded 광고 로드 완료: {adInfo}");

        if (!showAfterLoad || pendingRewardAction == null) return;

        showAfterLoad = false;
        ShowLoadedRewardedAd();
    }

    private void OnRewardedLoadFailed(LevelPlayAdError error)
    {
        isRewardedAdLoading = false;
        showAfterLoad = false;
        pendingRewardAction = null;
        Debug.LogError($"[Ads] Rewarded 광고 로드 실패: {error}");
    }

    private void OnRewardedDisplayed(LevelPlayAdInfo adInfo)
    {
        Debug.Log($"[Ads] Rewarded 광고 표시 성공: {adInfo}");
    }

    private void OnRewardedDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
    {
        Debug.LogError($"[Ads] Rewarded 광고 표시 실패: {error}");

        isShowingRewardedAd = false;
        showAfterLoad = false;
        pendingRewardAction = null;

        LoadRewardedAd();
    }

    private void OnRewarded(LevelPlayAdInfo adInfo, LevelPlayReward reward)
    {
        Debug.Log($"[Ads] 광고 시청 완료. 보상: {reward.Name}, 수량: {reward.Amount}");

        Action rewardAction = pendingRewardAction;
        pendingRewardAction = null;

        if (rewardAction == null)
        {
            Debug.LogWarning("[Ads] 광고 보상 콜백이 없습니다. 이벤트 순서를 확인하세요.");
            return;
        }

        rewardAction.Invoke();
    }

    private void OnRewardedClosed(LevelPlayAdInfo adInfo)
    {
        Debug.Log("[Ads] Rewarded 광고 닫힘");
        isShowingRewardedAd = false;
        showAfterLoad = false;
        // 보상 이벤트 없이 닫혔을 경우
        // 이전 콜백이 남는 것을 방지
        LoadRewardedAd();
    }

    private void OnRewardedClicked(LevelPlayAdInfo adInfo)
    {
        Debug.Log("[Ads] Rewarded 광고 클릭");
    }

    private void OnRewardedInfoChanged(LevelPlayAdInfo adInfo)
    {
        Debug.Log($"[Ads] Rewarded 광고 정보 변경: {adInfo}");
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        LevelPlay.OnInitSuccess -= OnInitSuccess;
        LevelPlay.OnInitFailed -= OnInitFailed;

        if (rewardedAd != null)
        {
            rewardedAd.OnAdLoaded -= OnRewardedLoaded;
            rewardedAd.OnAdLoadFailed -= OnRewardedLoadFailed;
            rewardedAd.OnAdDisplayed -= OnRewardedDisplayed;
            rewardedAd.OnAdDisplayFailed -= OnRewardedDisplayFailed;
            rewardedAd.OnAdRewarded -= OnRewarded;
            rewardedAd.OnAdClosed -= OnRewardedClosed;
            rewardedAd.OnAdClicked -= OnRewardedClicked;
            rewardedAd.OnAdInfoChanged -= OnRewardedInfoChanged;
        }

        Instance = null;
    }

    [ContextMenu("Reset Ads Privacy Consent")]
    private void ResetPrivacyConsentForTesting()
    {
        PlayerPrefs.DeleteKey(PrivacyConsentKey);
        PlayerPrefs.Save();

        Debug.Log("[Ads] 저장된 개인정보 선택을 삭제했습니다. 플레이를 다시 시작하세요.");
    }
}