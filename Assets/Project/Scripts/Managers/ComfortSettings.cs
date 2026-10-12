using UnityEngine;

// 편의/접근성 설정: 화면 흔들림, 섬광(화면 전체 번쩍임/글리치) 줄이기.
// 진동 설정(HapticFeedback)과 같은 방식으로 PlayerPrefs에 저장한다 (세이브 데이터와 별개).
public static class ComfortSettings
{
    private const string ScreenShakeKey = "SCREEN_SHAKE_ON";
    private const string ReduceFlashKey = "REDUCE_FLASH_ON";

    private static bool? screenShakeCache;
    private static bool? reduceFlashCache;

    public static bool ScreenShakeEnabled
    {
        get
        {
            if (!screenShakeCache.HasValue) screenShakeCache = PlayerPrefs.GetInt(ScreenShakeKey, 1) == 1;
            return screenShakeCache.Value;
        }
        set
        {
            screenShakeCache = value;
            PlayerPrefs.SetInt(ScreenShakeKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static bool ReduceFlash
    {
        get
        {
            if (!reduceFlashCache.HasValue) reduceFlashCache = PlayerPrefs.GetInt(ReduceFlashKey, 0) == 1;
            return reduceFlashCache.Value;
        }
        set
        {
            reduceFlashCache = value;
            PlayerPrefs.SetInt(ReduceFlashKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    // 화면 전체 연출(산데비스탄 글리치, 피격 붉은 테두리 등)의 세기 배율
    public static float FlashIntensity => ReduceFlash ? 0.35f : 1f;
}
