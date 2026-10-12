using System;

// 튜토리얼 단계. 본 단계는 세이브(PlayerSaveData.tutorialFlags)에 비트로 기록한다 (기기 PlayerPrefs 세이브).
// 기존 세이브와 호환: 값은 끝에 추가만 할 것 (이미 저장된 비트 의미가 바뀌면 안 됨)
[Flags]
public enum TutorialStep
{
    None = 0,
    LobbyWelcome = 1 << 0,
    Launch = 1 << 1,
    Speed = 1 << 2,
    Dash = 1 << 3,
    Skill = 1 << 4,
    Augment = 1 << 5,
    Door = 1 << 6,
    Blessing = 1 << 7,
    SpecialRoom = 1 << 8,
    Boss = 1 << 9,
    LobbyUpgrade = 1 << 10,
    DailyRewards = 1 << 11,
    CombatTutorial = 1 << 12,        //전투 튜토리얼을 마쳤거나 나감 (처음 스테이지 1 도전 시 자동 시작하지 않음)
    CombatTutorialReward = 1 << 13,  //전투 튜토리얼 완료 보상을 받음 (한 번만)
}

public static class TutorialProgress
{
    public static bool IsDone(TutorialStep step)
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Data == null) return true;  //세이브가 없으면 띄우지 않음

        return (SaveManager.Instance.Data.tutorialFlags & (int)step) != 0;
    }

    public static void MarkDone(TutorialStep step)
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Data == null) return;
        if (IsDone(step)) return;

        SaveManager.Instance.Data.tutorialFlags |= (int)step;
        SaveManager.Instance.Save();
    }
}

// 다른 시스템이 튜토리얼에 알리는 순간 (팝업이 뜬 직후 등)
public static class TutorialEvents
{
    public static event Action<TutorialStep> Triggered;

    public static void Raise(TutorialStep step)
    {
        Triggered?.Invoke(step);
    }
}
