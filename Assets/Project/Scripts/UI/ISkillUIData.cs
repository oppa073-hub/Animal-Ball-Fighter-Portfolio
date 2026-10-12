using UnityEngine;

public interface ISkillUIData
{
    Sprite SkillIcon { get; }
    float CooldownRatio { get; }

    void TryUseSkill();
}

// 스킬 버튼 부가 표시용 (남은 쿨타임 초, 증강 스킬 레벨, 자동 발동 여부)
public interface ISkillDetailUIData
{
    float RemainCooldown { get; }  //초 (준비됐으면 0)
    int SkillLevel { get; }        //0이면 레벨 표시 안 함
    bool IsAutoSkill { get; }      //자동 발동 (버튼으로 쓰지 않음)
}

// 스킬 버튼에 남은 사용 횟수를 표시해야 하는 스킬 (MaxCharges가 2 이상일 때만 표시)
public interface ISkillChargeUIData
{
    int MaxCharges { get; }
    int CurrentCharges { get; }
}