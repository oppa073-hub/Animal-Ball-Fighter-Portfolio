using System.Collections.Generic;
using UnityEngine;

// 방이 시작될 때 BreakableWall을 랜덤 배치하고, 다음 방이 시작되면 이전 벽을 회수한다.
// RoomManager가 적 스폰 전에 SpawnForRoom을 호출한다 (적이 벽 안에 생기지 않도록).
public class RoomObstacleSpawner : MonoBehaviour
{
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private int poolSize = 6;

    [Header("Count (min, max inclusive)")]
    // 적이 설 자리가 부족하지 않도록 (뒤쪽 방 적 최대 10마리) 개수를 보수적으로 둔다. 보스 방은 보스 크기/돌진 패턴 때문에 0
    [SerializeField] private Vector2Int normalRoomCount = new Vector2Int(2, 3);
    [SerializeField] private Vector2Int eliteRoomCount = new Vector2Int(1, 2);
    [SerializeField] private Vector2Int bossRoomCount = new Vector2Int(0, 0);

    [Header("Placement")]
    [SerializeField] private float areaMargin = 0.3f;  //스폰 영역 가장자리 여백 (벽 반길이에 더해짐)
    [SerializeField] private float minDistanceFromPlayerSpawn = 3.5f;
    [SerializeField] private float minDistanceBetweenWalls = 2.8f;
    [SerializeField] private Vector2 lengthScaleRange = new Vector2(0.8f, 1.35f);  //벽 길이(X 스케일) 변화
    [SerializeField] private int maxPlacementTries = 30;

    private BoxCollider spawnArea;
    private readonly List<BreakableWall> activeWalls = new List<BreakableWall>();
    private readonly List<Vector3> placedPositions = new List<Vector3>();

    private void Awake()
    {
        GameObject spawnAreaObject = GameObject.FindGameObjectWithTag("SpawnArea");
        if (spawnAreaObject != null) spawnArea = spawnAreaObject.GetComponent<BoxCollider>();
    }

    public void SpawnForRoom(RoomType roomType, Vector3 playerSpawnPosition)
    {
        ClearWalls();

        if (wallPrefab == null || spawnArea == null || ObjectPoolManager.Instance == null)
        {
            Debug.LogWarning("[Obstacle] 벽 프리팹/스폰 영역/풀 매니저가 없어 벽을 배치하지 않습니다.");
            return;
        }

        ObjectPoolManager.Instance.RegisterPool(wallPrefab, poolSize);  //이미 등록돼 있으면 바로 반환

        int count = GetWallCount(roomType);
        Bounds bounds = spawnArea.bounds;
        float baseHalfLength = GetPrefabHalfLength();

        for (int i = 0; i < count; i++)
        {
            float lengthScale = Random.Range(lengthScaleRange.x, lengthScaleRange.y);

            // 회전에 상관없이 벽 끝이 스폰 영역 안에 들도록 반길이만큼 여백 추가
            if (!TryFindPosition(bounds, playerSpawnPosition, areaMargin + baseHalfLength * lengthScale, out Vector3 position)) break;

            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 180f), 0f);
            GameObject wallObject = ObjectPoolManager.Instance.GetObject(wallPrefab, position, rotation);

            if (wallObject == null) break;  //풀 부족

            BreakableWall wall = wallObject.GetComponent<BreakableWall>();
            wall.SetLength(lengthScale);

            activeWalls.Add(wall);
            placedPositions.Add(position);
        }

        Physics.SyncTransforms();  //직후 적 스폰 위치 검사(CheckSphere)가 새 벽 위치를 보도록
    }

    public void ClearWalls()
    {
        foreach (BreakableWall wall in activeWalls)
        {
            if (wall != null && wall.gameObject.activeInHierarchy) wall.ReturnToPool();
        }

        activeWalls.Clear();
        placedPositions.Clear();
    }

    private int GetWallCount(RoomType roomType)
    {
#if UNITY_EDITOR
        int overrideCount = DebugTestSettings.WallCountOverride;  //Tools > BounceSquad Debug
        if (overrideCount >= 0) return Mathf.Min(overrideCount, poolSize);
#endif
        Vector2Int range = roomType switch
        {
            RoomType.Elite => eliteRoomCount,
            RoomType.Boss => bossRoomCount,
            _ => normalRoomCount
        };

        return Mathf.Min(Random.Range(range.x, range.y + 1), poolSize);
    }

    private float GetPrefabHalfLength()
    {
        BoxCollider box = wallPrefab.GetComponent<BoxCollider>();
        return box != null ? box.size.x * 0.5f : 1f;
    }

    private bool TryFindPosition(Bounds bounds, Vector3 playerSpawnPosition, float margin, out Vector3 position)
    {
        float minX = bounds.min.x + margin;
        float maxX = bounds.max.x - margin;
        float minZ = bounds.min.z + margin;
        float maxZ = bounds.max.z - margin;

        if (minX > maxX || minZ > maxZ)
        {
            position = Vector3.zero;
            return false;
        }

        for (int i = 0; i < maxPlacementTries; i++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(minX, maxX),
                spawnArea.transform.position.y,  //바닥 높이
                Random.Range(minZ, maxZ));

            if (FlatDistance(candidate, playerSpawnPosition) < minDistanceFromPlayerSpawn) continue;
            if (IsTooCloseToOtherWalls(candidate)) continue;

            position = candidate;
            return true;
        }

        position = Vector3.zero;
        return false;
    }

    private bool IsTooCloseToOtherWalls(Vector3 candidate)
    {
        foreach (Vector3 placed in placedPositions)
        {
            if (FlatDistance(candidate, placed) < minDistanceBetweenWalls) return true;
        }

        return false;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
