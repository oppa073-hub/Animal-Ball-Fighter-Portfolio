// 적이 죽을 때 방 클리어 판정(RoomManager.OnEnemyDead)보다 먼저 실행할 처리 (예: 분열 소환)
public interface IEnemyDeathHook
{
    void OnBeforeEnemyDeath(EnemyController enemy);
}
