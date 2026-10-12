using UnityEngine;

public static class MobilePerformanceSettings
{
    [RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
#if UNITY_ANDROID || UNITY_IOS
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
#endif

        // 출시 빌드: 일반 로그는 버리고 경고/에러만 남김 (로그마다 스택 트레이스를 뽑는 비용 제거)
        if (!Debug.isDebugBuild) Debug.unityLogger.filterLogType = LogType.Warning;
    }
}