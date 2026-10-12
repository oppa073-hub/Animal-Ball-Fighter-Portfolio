using UnityEngine;

// 짧은 진동 피드백. 안드로이드는 밀리초 단위 진동(VibrationEffect), iOS는 기본 진동이 길어서 강한 피드백에만 사용한다.
// 설정은 PlayerPrefs(VIBRATION_ON)에 저장한다 (세이브 데이터와 별개, 사운드 볼륨과 같은 방식).
public static class HapticFeedback
{
    private const string EnabledKey = "VIBRATION_ON";
    private const float MinInterval = 0.05f;  //연속 충돌 때 진동이 뭉개지지 않도록

    private static float lastVibrateTime = -1f;
    private static int lastStrength = -1;
    private static bool? enabledCache;

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject vibrator;
    private static AndroidJavaClass vibrationEffectClass;
    private static bool androidInitialized;
    private static bool androidFailed;
    private static int androidSdk;
#endif

    public static bool Enabled
    {
        get
        {
            if (!enabledCache.HasValue) enabledCache = PlayerPrefs.GetInt(EnabledKey, 1) == 1;
            return enabledCache.Value;
        }
        set
        {
            enabledCache = value;
            PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static void Light() => Vibrate(15, 60, 0);    //적 타격
    public static void Medium() => Vibrate(30, 140, 1);  //적 처치, 피격
    public static void Heavy() => Vibrate(80, 255, 2);   //보스 처치

    private static void Vibrate(long milliseconds, int amplitude, int strength)
    {
        if (!Enabled) return;

        float now = Time.unscaledTime;
        if (now - lastVibrateTime < MinInterval && strength <= lastStrength) return;  //더 강한 진동은 간격 제한 무시 (타격 직후 처치 등)

        if (!Vibrate(milliseconds, amplitude, strength >= 2)) return;  //실제로 울리지 않았으면 시간을 기록하지 않음

        lastVibrateTime = now;
        lastStrength = strength;
    }

    private static bool Vibrate(long milliseconds, int amplitude, bool strong)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return VibrateAndroid(milliseconds, amplitude, strong);
#elif UNITY_IOS && !UNITY_EDITOR
        if (!strong) return false;
        Handheld.Vibrate();
        return true;
#else
        return false;
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static bool VibrateAndroid(long milliseconds, int amplitude, bool strong)
    {
        if (androidFailed) return false;

        try
        {
            if (!androidInitialized)
            {
                androidInitialized = true;

                using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    androidSdk = version.GetStatic<int>("SDK_INT");
                }

                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
            }

            if (vibrator == null) return false;

            if (androidSdk >= 26)
            {
                if (vibrationEffectClass == null) vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");

                using (AndroidJavaObject effect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, Mathf.Clamp(amplitude, 1, 255)))
                {
                    vibrator.Call("vibrate", effect);
                }

                return true;
            }

            if (strong)
            {
                // 안드로이드 8.0 미만: 세기 조절이 안 되므로 강한 피드백에만 기본 진동 (이 호출이 있어야 빌드 시 VIBRATE 권한이 자동으로 들어감)
                Handheld.Vibrate();
                return true;
            }

            return false;
        }
        catch (System.Exception exception)
        {
            androidFailed = true;  //권한 없음 등: 이번 실행 동안 더 시도하지 않음
            Debug.LogWarning($"[Haptic] 진동 사용 불가: {exception.Message}");
            return false;
        }
    }
#endif
}
