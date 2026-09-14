using System.Collections.Generic;
using UnityEngine;

public class PlayerStats : MonoBehaviour  //전투 중 현재 스탯 데이터
{
    // ===== 기본 스탯 =====
    public int MaxHp { get; private set; }  //최대 세력
    public float Attack { get; private set; }  //기본 공격력
    public float SkillDamage { get; private set; }  //스킬 피해량
    public float MoveSpeed { get; private set; }  //기본 이동속도

    // ===== 치명타 =====
    public float CriticalChance { get; private set; }  //치명타 확률
    public float CriticalDamage { get; private set; }  //치명타 피해 배율

    public KeyCode SkillKey { get; private set; }//원래 모바일이지만 일단 테스트용

    // ===== 고유 스킬 =====
    public float SkillCooldown { get; private set; }  //스킬 쿨타임
    public float SkillRange { get; private set; }  //스킬 사거리
    public float SkillAngle { get; private set; }  //스킬 공격 각도

    // ===== 충돌 데미지 =====
    public float CollisionDamageMultiplier { get; private set; } = 1f;  //증강으로 증가하는 충돌 피해 배율 
    public float TemporaryCollisionDamageMultiplier { get; private set; } = 1f;  //캐릭터 스킬 등에서 일시적으로 적용되는 충돌 피해 배율
    public float FinalCollisionDamageMultiplier => CollisionDamageMultiplier * TemporaryCollisionDamageMultiplier;  //실제 충돌 데미지 계산에 사용되는 최종 배율

    // ===== 회복 / 흡혈 =====
    public float HealOnRoomClearPercent { get; private set; }  //룸 클리어시 최대 체력 회복 비율
    public float LifeStealPercent { get; private set; }  //가한 피해 기준 흡혈 비율

    // ===== 영구 강화 데이터 =====
    [SerializeField] private ShopItemData[] permanentUpgradeItems;

    public void Initialize(CharacterData data)
    {
        CharacterId characterId = SaveManager.Instance.Data.selectedCharacterId;

        CharacterProgressData progress = SaveManager.Instance.GetCharacterProgress(characterId);

        MaxHp = CharacterLevelCalculator.GetMaxHp(data.maxHp, progress.level);

        Attack = CharacterLevelCalculator.GetAttack(data.attack, progress.level);

        SkillDamage = data.skillDamage;
        MoveSpeed = data.moveSpeed;

        CriticalChance = data.criticalChance;
        CriticalDamage = data.criticalDamage;

        SkillKey = data.skillKey;
        SkillCooldown = data.skillCooldown;
        SkillRange = data.skillRange;
        SkillAngle = data.skillAngle;

        CollisionDamageMultiplier = 1f;
        HealOnRoomClearPercent = 0f;
        LifeStealPercent = 0f;
        TemporaryCollisionDamageMultiplier = 1f;

        ApplyPermanentUpgrades();
        Debug.Log( $"[Player] {data.characterName} / " +$"HP:{MaxHp} / ATK:{Attack} / SPD:{MoveSpeed}");
    }
    private void ApplyPermanentUpgrades()  //저장된 영구 강화 레벨을 현재 스탯에 적용
    {
        PlayerSaveData save = SaveManager.Instance.Data;

        ShopItemData hpItem = GetUpgradeItem(ShopItemType.MaxHp);
        ShopItemData attackItem = GetUpgradeItem(ShopItemType.Attack);
        ShopItemData speedItem = GetUpgradeItem(ShopItemType.MoveSpeed);

        MaxHp = PermanentUpgradeCalculator.GetMaxHp(MaxHp, save, hpItem);
        Attack = PermanentUpgradeCalculator.GetAttack(Attack, save, attackItem);
        MoveSpeed = PermanentUpgradeCalculator.GetMoveSpeed(MoveSpeed, save, speedItem);
    }

    public void AddAttackPercent(float value)
    {
        Attack *= 1 + value;
        Debug.Log("공격력 : " + Attack);
    }
    public void AddMaxHpPercent(float value)
    {
        int previousMaxHp = MaxHp;

        MaxHp = Mathf.RoundToInt(MaxHp * (1f + value));

        int increasedAmount = MaxHp - previousMaxHp;

        GetComponent<Health>()?.IncreaseMaxHp(increasedAmount);
    }
    public void AddMoveSpeedPercent(float value)
    {
        MoveSpeed = Mathf.Min(MoveSpeed * (1f + value), PermanentUpgradeCalculator.MaxMoveSpeed);

        Debug.Log("속도 : " + MoveSpeed);
    }
    public void AddSkillDamagePercent(float value)
    {
        SkillDamage *= 1 + value;
        Debug.Log("스킬 데미지 : " + SkillDamage);
    }
    public void AddCriticalChance(float value)
    {
        CriticalChance += value;
        Debug.Log("치명타 확률 : " + CriticalChance);
    }
    public void AddCriticalDamagePercent(float value)
    {
        CriticalDamage += value;
        Debug.Log("치명타 데미지 : " + CriticalDamage);
    }
    public void AddCollisionDamagePercent(float value)  //충돌 데미지만 증가
    {
        CollisionDamageMultiplier *= 1f + value;
        Debug.Log("충돌 데미지 배율 : " + CollisionDamageMultiplier);
    }
    public void AddHealOnRoomClearPercent(float value)
    {
        HealOnRoomClearPercent += value;

        Debug.Log($"[Augment] 룸 클리어 회복: {HealOnRoomClearPercent * 100f:F0}%");
    }
    public void AddLifeStealPercent(float value)
    {
        LifeStealPercent += value;

        Debug.Log($"[Augment] 흡혈: {LifeStealPercent * 100f:F0}%");
    }


    public void HealOnRoomClear(Health health)  //실제 회복시키는 함수
    {
        if (HealOnRoomClearPercent <= 0f) return;

        float healAmount = MaxHp * HealOnRoomClearPercent;
        health.Heal(healAmount);

        Debug.Log($"[Augment] 방 클리어 회복: {healAmount:F0}");
    }
    public void ApplyLifeSteal(int dealtDamage, Health health)
    {
        if (LifeStealPercent <= 0f) return;
        if (dealtDamage <= 0) return;

        float healAmount = dealtDamage * LifeStealPercent;
        health.Heal(healAmount);

        Debug.Log($"[LifeSteal] 피해:{dealtDamage}, 회복:{healAmount:F1}");
    }

    public void SetTemporaryCollisionMultiplier(float multiplier)  //돌진 등 일시 스킬용 충돌 데미지 배율 설정
    {
        TemporaryCollisionDamageMultiplier = multiplier;
    }

    public void ResetTemporaryCollisionMultiplier()  //일시 충돌 데미지 배율 초기화
    {
        TemporaryCollisionDamageMultiplier = 1f;
    }

    private ShopItemData GetUpgradeItem(ShopItemType type)  //상점 강화 타입에 맞는 데이터 검색
    {
        for (int i = 0; i < permanentUpgradeItems.Length; i++)
        {
            if (permanentUpgradeItems[i].itemType == type) return permanentUpgradeItems[i];
        }

        return null;
    }
}