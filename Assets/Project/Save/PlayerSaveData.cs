using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StageRecordData
{
    public int stageNumber;
    public int bestReachedRoom;
    public bool isCleared;
}
[System.Serializable]
public class CharacterProgressData
{
    public CharacterId characterId;
    public int level = 1;
}

[System.Serializable]
public class SkillTreeProgressData  //캐릭터 고유 스킬 트리 진행도
{
    public CharacterId characterId;
    public int tier;       //0 없음, 1 기본 강화, 2 갈래 2단계, 3 궁극
    public int branch;     //0 미선택, 1 A갈래, 2 B갈래
    public int spentGold;  //롤백 환불 계산용
}

[System.Serializable]
public class DailyMissionProgressData  //오늘의 미션 하나
{
    public string missionId;
    public int progress;
    public bool claimed;
}

[System.Serializable]
public class EnemyKillCountData  //도감: 적 종류별 처치 수
{
    public string enemyId;  //EnemyData 에셋 이름
    public int count;
}

[System.Serializable]
public class PlayerSaveData
{
    public int highestUnlockedStage = 1;
    public CharacterId selectedCharacterId = CharacterId.Cat;
    public int gold = 0;
    public int bestEndlessRoom = 0;
    public string nickname = "";

    [Header("Permanent Level")]
    public int maxHpUpgradeLevel = 0;
    public int attackUpgradeLevel = 0;
    public int moveSpeedUpgradeLevel = 0;

    [Header("Character Unlock")]
    public List<CharacterId> unlockedCharacters = new List<CharacterId>
    {
        CharacterId.Cat
    };

    public List<StageRecordData> stageRecords = new List<StageRecordData>();
    public List<CharacterProgressData> characterProgress = new List<CharacterProgressData>();

    [Header("IAP")]
    public List<string> processedIapTransactionIds = new List<string>();

    [Header("Dash")]  //기존 세이브와 호환: 필드는 항상 끝에 추가
    public int dashCapacityUpgradeLevel = 0;
    public int dashRechargeUpgradeLevel = 0;

    [Header("Skill Tree")]
    public List<SkillTreeProgressData> skillTrees = new List<SkillTreeProgressData>();

    [Header("Ascension")]  //해금된 최고 승천 단계 (0이면 기본 난이도만). 기존 세이브엔 없어서 0으로 시작
    public int ascensionUnlocked = 0;

    [Header("Tutorial")]  //본 튜토리얼 단계 비트 플래그. tutorialChecked: 기존 유저 판별을 한 번 했는지
    public int tutorialFlags = 0;
    public bool tutorialChecked = false;

    [Header("Daily")]  //날짜는 기기 현지 날짜 yyyy-MM-dd
    public string lastAttendanceDate = "";
    public int attendanceCount = 0;  //누적 출석 일수 (7일 주기 보상 계산용)
    public string dailyMissionDate = "";
    public List<DailyMissionProgressData> dailyMissions = new List<DailyMissionProgressData>();

    [Header("Codex")]  //보스 첫 처치 보상을 받은 보스, 만난 적 (EnemyData 에셋 이름)
    public List<string> defeatedBosses = new List<string>();
    public List<string> encounteredEnemies = new List<string>();
    public List<EnemyKillCountData> enemyKillCounts = new List<EnemyKillCountData>();
}