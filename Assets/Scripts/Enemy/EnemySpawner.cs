using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System;

public class EnemySpawner : MonoBehaviour
{
    private List<EnemyData> normalEnemies = new List<EnemyData>();
    private List<EnemyData> eliteEnemies = new List<EnemyData>();
    private List<EnemyData> bossEnemies = new List<EnemyData>();
    private AsyncOperationHandle<IList<EnemyData>> normalLoadHandle;
    private AsyncOperationHandle<IList<EnemyData>> eliteLoadHandle;
    private AsyncOperationHandle<IList<EnemyData>> bossLoadHandle;
    private bool isEnemyDataLoaded;

    [SerializeField] private Transform player;
    [SerializeField] private float minSpawnDistance = 5;
    [SerializeField] private BoxCollider spawnAreaCollider;
    [SerializeField] private float spawnCheckRadius = 3f;
    [SerializeField] private int maxSpawnTryCount;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private LayerMask enemyLayer;

    [SerializeField] private BossHealthUI bossHealthUI;

    private int spawnCount = 0;

    public event Action OnEnemyDataLoaded;
    public event Action OnEnemyDataLoadFailed;

    private void Start()
    {
        LoadEnemyData();
    }
    private void OnDestroy()
    {
        if (normalLoadHandle.IsValid()) Addressables.Release(normalLoadHandle);

        if (eliteLoadHandle.IsValid()) Addressables.Release(eliteLoadHandle);

        if (bossLoadHandle.IsValid()) Addressables.Release(bossLoadHandle);
    }
    public bool SpawnEnemies(int roomNumber)
    {
        if (!isEnemyDataLoaded)
        {
            Debug.LogWarning("[EnemySpawner] EnemyData 로드 전");

            return false;
        }

        if (GameSessionData.IsEndless)
        {
            spawnCount = CalculateEndlessSpawnCount(roomNumber);
        }
        else
        {
            spawnCount = Mathf.Clamp(2 + roomNumber, 3, 10);
        }

        if (RoomManager.Instance.RoomType == RoomType.Event)
        {
            Debug.LogError("[EnemySpawner] Event Room이 아직 구현되지 않았습니다.");

            return false;
        }

        switch (RoomManager.Instance.RoomType)
        {
            case RoomType.Normal:
                SpawnNormalRoom(roomNumber);
                break;

            case RoomType.Elite:
                SpawnEliteRoom(roomNumber);
                break;

            case RoomType.Boss:
                SpawnBossRoom(roomNumber);
                break;
        }

        return RoomManager.Instance.HasActiveEnemies;
    }
    private int CalculateEndlessSpawnCount(int roomNumber)
    {
        int baseCount = 3;
        int additionalCount = (roomNumber - 1) / 5;

        return Mathf.Clamp(baseCount + additionalCount, 3, 10);
    }

    public EnemyController SpawnRandomEnemyFromList(List<EnemyData> list, int count, int roomNumber) // 적 스폰 메인 함수
    {
        Debug.Log("SpawnRandomEnemyFromList 호출됨");
        if (list.Count == 0) return null;

        EnemyController lastSpawnedEnemy = null;

        for (int i = 0; i < count; i++)
        {
            EnemyData selectedData = list[UnityEngine.Random.Range(0, list.Count)];

            if (selectedData.enemyPrefab == null) continue;

            for (int j = 0; j < maxSpawnTryCount; j++)
            {
                Vector3 randomSpawnPo = GetRandomSpawnPosition();
                if (!IsValidSpawnPosition(randomSpawnPo)) continue;

                var enemy = ObjectPoolManager.Instance.GetObject(
                    selectedData.enemyPrefab,
                    randomSpawnPo,
                    Quaternion.identity
                );

                if (enemy == null)  
                {
                    Debug.LogWarning($"[EnemySpawner] {selectedData.enemyPrefab.name} 풀 부족");
                    continue;
                }

                var enemyCon = enemy.GetComponent<EnemyController>();

                if (enemyCon == null)
                {
                    Debug.LogError("적 에너미 컨트롤러 없음");
                }
                else
                {
                    enemyCon.Initialize(selectedData, roomNumber);
                    lastSpawnedEnemy = enemyCon;
                }

                break;
            }
        }

        return lastSpawnedEnemy;
    }
    public void SpawnNormalRoom(int roomNumber)
    {
        SpawnRandomEnemyFromList(normalEnemies, spawnCount, roomNumber);
    }
    public void SpawnEliteRoom(int roomNumber)
    {
        int normalCount = 3;

        if (GameSessionData.IsEndless)
        {
            normalCount = Mathf.Clamp(
                2 + (roomNumber / 5),
                3,
                8
            );
        }

        SpawnRandomEnemyFromList(eliteEnemies, 1, roomNumber);
        SpawnRandomEnemyFromList(normalEnemies, normalCount, roomNumber);
    }
    public void SpawnBossRoom(int roomNumber)
    {
        EnemyController boss;

        if (GameSessionData.IsEndless)
        {
            int bossIndex = roomNumber / 5;

            BossId targetBossId =
                bossIndex % 2 == 1
                ? BossId.Robot
                : BossId.Hero;

            EnemyData bossData =  bossEnemies.Find(data => data.bossId == targetBossId);

            if (bossData == null)
            {
                Debug.LogError($"[EnemySpawner] {targetBossId} 보스 데이터를 찾지 못했습니다.");
                return;
            }

            boss = SpawnRandomEnemyFromList(
                new List<EnemyData> { bossData },
                1,
                roomNumber
            );
        }
        else
        {
            BossId targetBossId = GameSessionData.SelectedStage.bossId;

            EnemyData bossData = bossEnemies.Find(data => data.bossId == targetBossId);

            if (bossData == null)
            {
                Debug.LogError($"[EnemySpawner] {targetBossId} 보스 데이터를 찾지 못했습니다.");
                return;
            }

            boss = SpawnRandomEnemyFromList(
                new List<EnemyData> { bossData },
                1,
                roomNumber
            );
        }

        if (boss == null) return;

        Health bossHealth = boss.GetComponent<Health>();

        if (bossHealth == null) return;

        UIManager.Instance.ShowBossHealth(bossHealth, boss);
    }

    public Vector3 GetRandomSpawnPosition()
    {
        var bounds = spawnAreaCollider.bounds;

        float x = UnityEngine.Random.Range(bounds.min.x, bounds.max.x);
        float z = UnityEngine.Random.Range(bounds.min.z, bounds.max.z);

        Vector3 spawnPosition = new Vector3(
             x,
            player.position.y,
             z
            );

        return spawnPosition;
    }

    public bool IsValidSpawnPosition(Vector3 enemy)
    {
        float distance = Vector3.Distance(player.position, enemy);
        if (distance < minSpawnDistance)
        {
            return false;
        }

        if (Physics.CheckSphere(enemy, spawnCheckRadius, obstacleLayer)) return false;

        if (Physics.CheckSphere(enemy, spawnCheckRadius, enemyLayer)) return false;

        return true;
    }
    private async void LoadEnemyData()
    {
        try
        {
            normalLoadHandle = Addressables.LoadAssetsAsync<EnemyData>("EnemyData_Normal", data => normalEnemies.Add(data));

            eliteLoadHandle = Addressables.LoadAssetsAsync<EnemyData>("EnemyData_Elite", data => eliteEnemies.Add(data));

            bossLoadHandle = Addressables.LoadAssetsAsync<EnemyData>("EnemyData_Boss", data => bossEnemies.Add(data));

            await normalLoadHandle.Task;
            await eliteLoadHandle.Task;
            await bossLoadHandle.Task;

            if (this == null) return;

            bool allSucceeded =
                normalLoadHandle.Status == AsyncOperationStatus.Succeeded &&
                eliteLoadHandle.Status == AsyncOperationStatus.Succeeded &&
                bossLoadHandle.Status == AsyncOperationStatus.Succeeded;

            bool hasRequiredData =
                normalEnemies.Count > 0 &&
                eliteEnemies.Count > 0 &&
                bossEnemies.Count > 0;

            if (!allSucceeded || !hasRequiredData)
            {
                Debug.LogError("[Addressables] EnemyData 로드 실패 또는 데이터가 비어 있습니다.");

                OnEnemyDataLoadFailed?.Invoke();
                return;
            }

            isEnemyDataLoaded = true;

            RegisterEnemyPools();

            Debug.Log(
                $"[Addressables] EnemyData 로드 완료 " +
                $"Normal:{normalEnemies.Count}, " +
                $"Elite:{eliteEnemies.Count}, " +
                $"Boss:{bossEnemies.Count}"
            );

            OnEnemyDataLoaded?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            OnEnemyDataLoadFailed?.Invoke();
        }
    }
    private void RegisterEnemyPools()  //적 프리팹 풀 등록
    {
        for (int i = 0; i < normalEnemies.Count; i++)
        {
            if (normalEnemies[i].enemyPrefab != null)
            {
                ObjectPoolManager.Instance.RegisterPool(normalEnemies[i].enemyPrefab, 20);
            }
        }

        for (int i = 0; i < eliteEnemies.Count; i++)
        {
            if (eliteEnemies[i].enemyPrefab != null)
            {
                ObjectPoolManager.Instance.RegisterPool(eliteEnemies[i].enemyPrefab, 5);
            }
        }

        for (int i = 0; i < bossEnemies.Count; i++)
        {
            if (bossEnemies[i].enemyPrefab != null)
            {
                ObjectPoolManager.Instance.RegisterPool(bossEnemies[i].enemyPrefab, 2);
            }
        }
    }
}
