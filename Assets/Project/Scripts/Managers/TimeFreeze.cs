using UnityEngine;

// 대쉬 조준처럼 잠깐 게임 시간을 완전히 멈출 때 사용 (멈추기 전 배속을 기억했다가 되돌림).
// 멈춘 동안 TimeSlowController는 시간 배속을 건드리지 않는다.
public static class TimeFreeze
{
    private static float savedTimeScale = 1f;

    public static bool IsActive { get; private set; }

    public static void Begin()
    {
        if (IsActive) return;

        HitStop.Cancel();  //히트스톱 중이면 멈칫 배속을 저장해 두지 않도록 먼저 풀기
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        IsActive = true;
    }

    public static void End(bool restoreTimeScale = true)  //일시정지 메뉴가 시간을 넘겨받는 경우 false
    {
        if (!IsActive) return;

        IsActive = false;

        // 그사이 다른 연출(보스 처치 슬로모션 등)이 배속을 바꿨으면 덮어쓰지 않음
        if (restoreTimeScale && Time.timeScale == 0f) Time.timeScale = savedTimeScale;
    }
}
