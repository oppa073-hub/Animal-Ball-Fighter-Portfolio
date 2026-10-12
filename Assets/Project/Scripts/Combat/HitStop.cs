using UnityEngine;

// 처치 순간 아주 짧게 게임 시간을 거의 멈춰 타격감을 준다 (실제 시간 기준).
// 다른 시간 연출(대쉬 조준 정지, 산데비스탄 감속, 보스 처치 슬로모션, 일시정지) 중에는 걸지 않고,
// 끝날 때 배속이 그사이 바뀌었으면 덮어쓰지 않는다.
public class HitStop : MonoBehaviour
{
    private const float StopScale = 0.05f;

    private static HitStop instance;

    private bool isActive;
    private float remainingRealTime;

    public static void Play(float realDuration)
    {
        if (realDuration <= 0f) return;
        if (TimeFreeze.IsActive || TimeSlowController.IsActive) return;

        if (instance == null) instance = new GameObject("HitStop").AddComponent<HitStop>();

        if (instance.isActive)
        {
            instance.remainingRealTime = Mathf.Max(instance.remainingRealTime, realDuration);
            return;
        }

        if (!Mathf.Approximately(Time.timeScale, 1f)) return;

        Time.timeScale = StopScale;
        instance.isActive = true;
        instance.remainingRealTime = realDuration;
    }

    public static void Cancel()
    {
        if (instance != null) instance.Finish();
    }

    private void Update()
    {
        if (!isActive) return;

        remainingRealTime -= Time.unscaledDeltaTime;
        if (remainingRealTime <= 0f) Finish();
    }

    private void Finish()
    {
        if (!isActive) return;

        isActive = false;
        if (Mathf.Approximately(Time.timeScale, StopScale)) Time.timeScale = 1f;
    }

    private void OnDestroy()
    {
        Finish();
        if (instance == this) instance = null;
    }
}
