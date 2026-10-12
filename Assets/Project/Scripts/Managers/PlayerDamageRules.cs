using UnityEngine;

// 플레이어가 받는 피해의 캐주얼 난이도 규칙 (한 곳에서 조정)
// - 스테이지별 받는 피해 배율: 초반 스테이지는 덜 아프게. DamageManager가 플레이어 대상 피해에 모두 곱함 (몸통/투사체/보스 기술/폭발/상태이상)
// - 몸통 피해: 적 공격력 × ContactDamageMultiplier (일반 스테이지만, 무한 모드는 예전 공식)
// - 몸통 피격 무적: 적과 부딪혀 맞은 뒤 잠깐 몸통 피해를 받지 않음. 한 번 쏴서 적 무리를 지나갈 때 연달아 맞지 않도록
public static class PlayerDamageRules
{
    public const float ContactInvulnerableTime = 0.8f;
    public const float ContactDamageMultiplier = 2f;  //1배는 너무 쉬웠음 (테스트 피드백)

    private static readonly float[] StageDamageTaken = { 0.6f, 0.8f };  //1스테이지, 2스테이지 (3스테이지부터 1)

    private const float TutorialDamageTaken = 0.5f;

    private static float lastContactHitTime = -999f;

    public static float StageDamageTakenMultiplier
    {
        get
        {
            if (GameSessionData.IsEndless || GameSessionData.SelectedStage == null) return 1f;  //무한 모드는 기존 방별 성장 그대로
            if (TutorialMode.IsActive) return TutorialDamageTaken;

            int index = GameSessionData.SelectedStage.stageNumber - 1;
            return index >= 0 && index < StageDamageTaken.Length ? StageDamageTaken[index] : 1f;
        }
    }

    // 몸통 피해를 줄 수 있으면 true를 돌려주고 무적 시간을 시작한다
    public static bool TryConsumeContactHit()
    {
        if (Time.time - lastContactHitTime < ContactInvulnerableTime) return false;

        lastContactHitTime = Time.time;
        return true;
    }

    public static bool IsEarlyStage(int maxStage)  //일반 스테이지 1~maxStage
    {
        return !GameSessionData.IsEndless && GameSessionData.SelectedStage != null && GameSessionData.SelectedStage.stageNumber <= maxStage;
    }
}
