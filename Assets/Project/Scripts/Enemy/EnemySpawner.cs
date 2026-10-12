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

    [SerializeField] private float normalCountMultiplier = 1.5f;  //일반 적 수 배율 (대신 Ranged Enemy 데이터의 체력/공격력을 조금 낮춤)
    [SerializeField] private float summonHpMultiplier = 1.2f;  //마녀 소환/엘리트 분열 적은 수가 늘지 않으므로 원래 세기로 (20→24)
    [SerializeField] private float summonAttackMultiplier = 1.2f;  //(5→6, 탄환 6→7.2)

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

        if (TutorialMode.IsActive) return SpawnTutorialRoom(roomNumber);

        if (GameSessionData.IsEndless)
        {
            spawnCount = ScaleNormalCount(CalculateEndlessSpawnCount(roomNumber));
        }
        else
        {
            spawnCount = ScaleNormalCount(Mathf.Clamp(2 + roomNumber, 3, 10));
            if (roomNumber <= 2 && PlayerDamageRules.IsEarlyStage(2)) spawnCount -= 1;  //1~2스테이지 첫 두 방은 한 마리 적게 (캐주얼 난이도)
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
    private int ScaleNormalCount(int baseCount)  //일반 적은 수를 늘리고 대신 능력치를 낮춘 데이터 사용 (Ranged Enemy)
    {
        return Mathf.CeilToInt(baseCount * normalCountMultiplier);
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
    // 일반 적: 2번 방부터 특수 적(방패/폭탄/치유/가시) 몇 마리 + 나머지는 근접형/원거리형 반반
    // (홀수면 남는 1마리는 무작위, 한쪽 데이터가 없으면 전부 다른 쪽)
    private void SpawnMixedNormalEnemies(int count, int roomNumber)
    {
        List<EnemyData> specialEnemies = normalEnemies.FindAll(data => data.role != EnemyRole.None);
        List<EnemyData> basicEnemies = normalEnemies.FindAll(data => data.role == EnemyRole.None);

        int specialCount = specialEnemies.Count > 0 ? GetSpecialEnemyCount(count, roomNumber) : 0;
        if (specialCount > 0)
        {
            SpawnRandomEnemyFromList(specialEnemies, specialCount, roomNumber);
            count -= specialCount;
        }

        List<EnemyData> meleeEnemies = basicEnemies.FindAll(data => data.attackType == EnemyAttackType.Melee);
        List<EnemyData> rangedEnemies = basicEnemies.FindAll(data => data.attackType != EnemyAttackType.Melee);

        if (meleeEnemies.Count == 0 || rangedEnemies.Count == 0)
        {
            SpawnRandomEnemyFromList(basicEnemies.Count > 0 ? basicEnemies : normalEnemies, count, roomNumber);
            return;
        }

        int meleeCount = count / 2;
        if (count % 2 == 1 && UnityEngine.Random.value < 0.5f) meleeCount++;

        SpawnRandomEnemyFromList(meleeEnemies, meleeCount, roomNumber);
        SpawnRandomEnemyFromList(rangedEnemies, count - meleeCount, roomNumber);
    }
    private int GetSpecialEnemyCount(int totalCount, int roomNumber)  //첫 방은 없음, 이후 전체의 1/4 정도 (1~3, 승천 단계에 따라 추가)
    {
        if (roomNumber < 2 || totalCount < 3) return 0;

        int count = Mathf.Clamp(totalCount / 4, 1, 3) + RunState.AscensionSpecialBonus;
        return Mathf.Min(count, totalCount - 1);  //기본 적이 최소 1마리는 남게
    }

    public void SpawnNormalRoom(int roomNumber)
    {
        SpawnMixedNormalEnemies(spawnCount, roomNumber);
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

        EnemyController elite = SpawnRandomEnemyFromList(eliteEnemies, 1, roomNumber);
        EliteTraitController.ApplyRandom(elite, roomNumber, this);  //분열/순간이동/재생/질주 중 하나

        SpawnMixedNormalEnemies(ScaleNormalCount(normalCount), roomNumber);
    }
    private static readonly BossId[] EndlessBossRotation = { BossId.Robot, BossId.Witch, BossId.Hero };  //스테이지 1~3 보스 순서와 같게

    public void SpawnBossRoom(int roomNumber)
    {
        EnemyController boss;

        if (GameSessionData.IsEndless)
        {
            int bossIndex = roomNumber / 5;

            // 보스 방마다 순서대로 순환 (5방 로봇, 10방 마녀, 15방 영웅, ...)
            BossId targetBossId = EndlessBossRotation[(Mathf.Max(1, bossIndex) - 1) % EndlessBossRotation.Length];
#if UNITY_EDITOR
            if (DebugTestSettings.BossOverride >= 0) targetBossId = (BossId)DebugTestSettings.BossOverride;  //Tools > BounceSquad Debug
#endif

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
#if UNITY_EDITOR
            if (DebugTestSettings.BossOverride >= 0) targetBossId = (BossId)DebugTestSettings.BossOverride;  //Tools > BounceSquad Debug
#endif

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

        if (TutorialMode.IsActive) boss.ApplyStatMultiplier(TutorialBossHpMultiplier, 1f);

        Health bossHealth = boss.GetComponent<Health>();

        if (bossHealth == null) return;

        UIManager.Instance.ShowBossHealth(bossHealth, boss);
    }

    // 보스 소환 패턴용: center 주변 고리(minRadius~maxRadius) 안의 빈 자리에 일반 적 1마리 스폰 (맵 밖으로 나가지 않게 스폰 영역으로 제한)
    public EnemyController SpawnNormalEnemyNear(Vector3 center, float minRadius, float maxRadius, float minPlayerDistance, int roomNumber)
    {
        if (normalEnemies.Count == 0) return null;

        Bounds bounds = spawnAreaCollider.bounds;

        for (int i = 0; i < maxSpawnTryCount; i++)
        {
            Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
            float distance = UnityEngine.Random.Range(minRadius, maxRadius);

            Vector3 position = new Vector3(
                Mathf.Clamp(center.x + direction.x * distance, bounds.min.x, bounds.max.x),
                player.position.y,
                Mathf.Clamp(center.z + direction.y * distance, bounds.min.z, bounds.max.z));

            if (Vector3.Distance(position, player.position) < minPlayerDistance) continue;
            if (Physics.CheckSphere(position, spawnCheckRadius, obstacleLayer)) continue;
            if (Physics.CheckSphere(position, spawnCheckRadius, enemyLayer)) continue;

            List<EnemyData> summonPool = normalEnemies.FindAll(candidate => candidate.role == EnemyRole.None);  //소환/분열은 기본 적만
            if (summonPool.Count == 0) summonPool = normalEnemies;
            EnemyData data = summonPool[UnityEngine.Random.Range(0, summonPool.Count)];
            if (data.enemyPrefab == null) continue;

            GameObject enemyObject = ObjectPoolManager.Instance.GetObject(data.enemyPrefab, position, Quaternion.identity);
            if (enemyObject == null) return null;  //풀 부족

            EnemyController enemy = enemyObject.GetComponent<EnemyController>();
            enemy?.Initialize(data, roomNumber);
            enemy?.ApplyStatMultiplier(summonHpMultiplier, summonAttackMultiplier);  //소환/분열 적은 일반 적 하향 전 세기로

            // 적은 플레이어 발사 순간에 한꺼번에 출발하므로, 전투 중 소환된 적은 바로 무작위 방향으로 출발시킴
            if (enemy != null && GameManager.Instance != null && GameManager.Instance.currentState == GameState.Playing)
                enemy.GetComponent<EnemyBallController>()?.ShootEnemy();

            return enemy;
        }

        return null;
    }

    // ───────── 전투 튜토리얼 (정해진 구성) ─────────
    // 1방: 기본 근접 적 2 (발사/대쉬) / 2방: 원거리 + 방패 + 가시 + 폭탄 (적 소개, 스킬) / 3방: 엘리트 + 기본 2 / 4방: 체력을 줄인 보스
    public const float TutorialBossHpMultiplier = 0.4f;

    private bool SpawnTutorialRoom(int roomNumber)
    {
        List<EnemyData> basicMelee = normalEnemies.FindAll(data => data.role == EnemyRole.None && data.attackType == EnemyAttackType.Melee);
        if (basicMelee.Count == 0) basicMelee = normalEnemies;

        switch (RoomManager.Instance.RoomType)
        {
            case RoomType.Boss:
                SpawnBossRoom(roomNumber);  //체력 감소는 SpawnBossRoom 안에서 (체력바 연결 전에)
                break;

            case RoomType.Elite:
                EnemyController elite = SpawnRandomEnemyFromList(eliteEnemies, 1, roomNumber);
                EliteTraitController.ApplyRandom(elite, roomNumber, this);
                SpawnRandomEnemyFromList(basicMelee, 2, roomNumber);
                break;

            default:
                if (roomNumber <= 1)
                {
                    SpawnRandomEnemyFromList(basicMelee, 2, roomNumber);
                    break;
                }

                List<EnemyData> ranged = normalEnemies.FindAll(data => data.role == EnemyRole.None && data.attackType != EnemyAttackType.Melee);
                SpawnRandomEnemyFromList(ranged.Count > 0 ? ranged : basicMelee, 1, roomNumber);
                foreach (EnemyRole role in TutorialRoles)
                {
                    EnemyData data = normalEnemies.Find(candidate => candidate.role == role);
                    if (data != null) SpawnRandomEnemyFromList(new List<EnemyData> { data }, 1, roomNumber);
                }
                break;
        }

        return RoomManager.Instance.HasActiveEnemies;
    }

    public static readonly EnemyRole[] TutorialRoles = { EnemyRole.Shield, EnemyRole.Spiky, EnemyRole.Bomber };

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
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            OnEnemyDataLoadFailed?.Invoke();
            return;
        }

        // 로드 성공 알림은 try 밖에서: 받는 쪽(방 시작 등)에서 난 예외를 '로드 실패'로 잘못 처리해 로비로 튕기지 않게
        OnEnemyDataLoaded?.Invoke();
    }
    private void RegisterEnemyPools()  //적 프리팹 풀 등록
    {
        for (int i = 0; i < normalEnemies.Count; i++)
        {
            if (normalEnemies[i].enemyPrefab != null)
            {
                ObjectPoolManager.Instance.RegisterPool(normalEnemies[i].enemyPrefab, 28);  //무한 모드 최대 15마리 + 분열/소환 여유
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

        // 씬 풀 목록에 없는 투사체(예: 마녀 초록 총알)를 전투 중이 아닌 로딩 때 미리 생성 (이미 있으면 바로 반환)
        RegisterProjectilePools(normalEnemies);
        RegisterProjectilePools(eliteEnemies);
        RegisterProjectilePools(bossEnemies);
    }

    private void RegisterProjectilePools(List<EnemyData> enemyDatas)
    {
        for (int i = 0; i < enemyDatas.Count; i++)
        {
            if (enemyDatas[i].projectilePrefab != null)
            {
                ObjectPoolManager.Instance.RegisterPool(enemyDatas[i].projectilePrefab, 35);  //일반 적 1.5배에 맞춘 여유 (씬 풀 목록에 이미 있으면 그 크기 사용)
            }
        }
    }
}
