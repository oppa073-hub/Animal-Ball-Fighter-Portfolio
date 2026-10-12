using System;

// 전투 중 일어난 일을 알리는 정적 이벤트 모음. 콤보, 진동, 일일 미션, 업적 등이 구독한다.
// 씬이 바뀌어도 남는 static 이벤트이므로 구독한 쪽은 OnDisable/OnDestroy에서 반드시 해제한다.
public static class GameEvents
{
    public static event Action<EnemyController, bool> EnemyKilled;  //(적, 보스 여부)
    public static event Action<int> ComboChanged;  //현재 콤보 (0이면 끊김)
    public static event Action PlayerDamaged;
    public static event Action<int, bool> RoomCleared;  //(방 번호, 무한 모드 여부)
    public static event Action<int> StageCleared;  //스테이지 번호
    public static event Action<EnemyController> EnemySpawned;  //도감 기록 등
    public static event Action BankShot;  //뱅크샷 성공 (일일 미션 등)
    public static event Action SkillUsed;  //캐릭터 고유 스킬 사용

    public static void RaiseEnemyKilled(EnemyController enemy, bool isBoss) => EnemyKilled?.Invoke(enemy, isBoss);
    public static void RaiseComboChanged(int combo) => ComboChanged?.Invoke(combo);
    public static void RaisePlayerDamaged() => PlayerDamaged?.Invoke();
    public static void RaiseRoomCleared(int roomNumber, bool isEndless) => RoomCleared?.Invoke(roomNumber, isEndless);
    public static void RaiseStageCleared(int stageNumber) => StageCleared?.Invoke(stageNumber);
    public static void RaiseEnemySpawned(EnemyController enemy) => EnemySpawned?.Invoke(enemy);
    public static void RaiseBankShot() => BankShot?.Invoke();
    public static void RaiseSkillUsed() => SkillUsed?.Invoke();
}
