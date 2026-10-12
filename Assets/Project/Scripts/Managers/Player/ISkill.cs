using UnityEngine;

public interface ISkill
{
    void UseSkill();
}

// 사용 횟수(충전)를 여러 개 가지며 충전마다 쿨타임이 따로 도는 스킬. PlayerSkill의 단일 쿨타임 대신 이쪽 판단을 쓴다.
public interface IChargeSkill : ISkill
{
    int MaxCharges { get; }
    int CurrentCharges { get; }
    float NextChargeRatio { get; }  //가장 먼저 충전될 횟수의 진행도 (0~1, 가득 차 있으면 1)
    bool CanUseSkill { get; }
}
