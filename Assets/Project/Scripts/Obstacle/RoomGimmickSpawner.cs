using System.Collections.Generic;
using UnityEngine;

// 방마다 무작위 맵 기믹 배치 (범퍼 / 폭발 통 / 포털 한 쌍). 모델이 없어 기본 도형으로 만든다.
// RoomManager가 벽 배치 뒤, 적 스폰 전에 SpawnForRoom을 호출한다 (적이 기믹 위에 생기지 않도록 Wall 레이어 사용).
public class RoomGimmickSpawner : MonoBehaviour
{
    private const float AreaMargin = 1.6f;
    private const float MinDistanceFromPlayer = 3.2f;
    private const float MinDistanceBetween = 2.6f;
    private const float ClearRadius = 1.1f;
    private const int MaxTries = 30;

    private static RoomGimmickSpawner instance;

    private BoxCollider spawnArea;
    private int wallLayer;
    private int blockMask;
    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<Vector3> placed = new List<Vector3>();

    public static RoomGimmickSpawner GetOrCreate()
    {
        if (instance == null) instance = new GameObject("RoomGimmickSpawner").AddComponent<RoomGimmickSpawner>();
        return instance;
    }

    private void Awake()
    {
        GameObject areaObject = GameObject.FindGameObjectWithTag("SpawnArea");
        if (areaObject != null) spawnArea = areaObject.GetComponent<BoxCollider>();

        wallLayer = LayerMask.NameToLayer("Wall");
        blockMask = LayerMask.GetMask("Wall", "Enemy");
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void Clear()
    {
        foreach (GameObject go in spawned)
        {
            if (go == null) continue;
            go.SetActive(false);  //Destroy는 프레임 끝에 되므로 이번 프레임 배치 판정에 걸리지 않게 먼저 끔
            Destroy(go);
        }
        spawned.Clear();
        placed.Clear();
    }

    public void SpawnForRoom(RoomType roomType, int roomNumber, Vector3 playerSpawnPosition)
    {
        Clear();
        if (spawnArea == null || roomNumber < 2) return;  //첫 방은 기본 규칙만

        Physics.SyncTransforms();  //방금 배치된 벽 위치를 겹침 판정에 반영

        int count = roomType switch
        {
            RoomType.Normal => Random.Range(1, 3),
            RoomType.Elite => Random.Range(1, 3),
            RoomType.Boss => Random.Range(0, 2),  //보스 방은 공간이 필요해서 적게
            _ => 0
        };

        for (int i = 0; i < count; i++)
        {
            int kind = roomType == RoomType.Boss ? 0 : Random.Range(0, 3);  //보스 방은 범퍼만

            switch (kind)
            {
                case 0: TrySpawn(playerSpawnPosition, CreateBumper); break;
                case 1: TrySpawn(playerSpawnPosition, CreateBarrel); break;
                default: TrySpawnPortals(playerSpawnPosition); break;
            }
        }

        Physics.SyncTransforms();  //적 스폰 위치 판정이 기믹을 피하도록
    }

    private void TrySpawn(Vector3 playerSpawnPosition, System.Func<Vector3, GameObject> create)
    {
        if (!TryFindPosition(playerSpawnPosition, out Vector3 position)) return;

        GameObject go = create(position);
        spawned.Add(go);
        placed.Add(position);
    }

    private void TrySpawnPortals(Vector3 playerSpawnPosition)
    {
        if (!TryFindPosition(playerSpawnPosition, out Vector3 first)) return;
        placed.Add(first);

        Vector3 second = Vector3.zero;
        bool found = false;
        for (int i = 0; i < MaxTries && !found; i++)  //두 포털은 멀리 떨어지게
        {
            if (TryFindPosition(playerSpawnPosition, out second) && Flat(second - first).magnitude > 6f) found = true;
        }

        if (!found)
        {
            placed.Remove(first);
            return;
        }

        placed.Add(second);

        RoomPortal a = CreatePortal(first, new Color(0.3f, 0.85f, 1f));
        RoomPortal b = CreatePortal(second, new Color(1f, 0.6f, 0.2f));
        a.Link(b);
        b.Link(a);
        spawned.Add(a.gameObject);
        spawned.Add(b.gameObject);
    }

    private bool TryFindPosition(Vector3 playerSpawnPosition, out Vector3 position)
    {
        Bounds bounds = spawnArea.bounds;

        for (int i = 0; i < MaxTries; i++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(bounds.min.x + AreaMargin, bounds.max.x - AreaMargin),
                playerSpawnPosition.y,
                Random.Range(bounds.min.z + AreaMargin, bounds.max.z - AreaMargin));

            if (Flat(candidate - playerSpawnPosition).magnitude < MinDistanceFromPlayer) continue;
            if (Physics.CheckSphere(candidate, ClearRadius, blockMask, QueryTriggerInteraction.Ignore)) continue;

            bool tooClose = false;
            foreach (Vector3 other in placed)
            {
                if (Flat(candidate - other).magnitude < MinDistanceBetween) { tooClose = true; break; }
            }
            if (tooClose) continue;

            position = candidate;
            return true;
        }

        position = Vector3.zero;
        return false;
    }

    // ───────── 기믹 생성 ─────────

    private GameObject CreateBumper(Vector3 position)
    {
        Transform body = RuntimeProps.Create(PrimitiveType.Cylinder, null, new Color(0.25f, 0.95f, 0.6f), keepCollider: true);
        body.name = "Bumper";
        body.position = position;
        body.localScale = new Vector3(1.3f, 0.7f, 1.3f);
        body.gameObject.layer = wallLayer;
        body.gameObject.tag = "Wall";  //벽처럼 튕김 (퍼펙트 반사/반동 충전도 그대로 동작)

        Transform cap = RuntimeProps.Create(PrimitiveType.Cylinder, body, Color.white);
        cap.localPosition = new Vector3(0f, 1.02f, 0f);
        cap.localScale = new Vector3(0.6f, 0.05f, 0.6f);

        body.gameObject.AddComponent<RoomBumper>();
        return body.gameObject;
    }

    private GameObject CreateBarrel(Vector3 position)
    {
        Transform body = RuntimeProps.Create(PrimitiveType.Cylinder, null, new Color(0.9f, 0.2f, 0.15f), keepCollider: true);
        body.name = "ExplosiveBarrel";
        body.position = position;
        body.localScale = new Vector3(0.9f, 0.6f, 0.9f);
        body.gameObject.layer = wallLayer;

        Transform band = RuntimeProps.Create(PrimitiveType.Cylinder, body, new Color(1f, 0.85f, 0.2f));  //노란 띠
        band.localScale = new Vector3(1.04f, 0.12f, 1.04f);

        body.gameObject.AddComponent<RoomExplosiveBarrel>();
        return body.gameObject;
    }

    private RoomPortal CreatePortal(Vector3 position, Color color)
    {
        Transform ring = RuntimeProps.Create(PrimitiveType.Cylinder, null, color);
        ring.name = "Portal";
        ring.position = position - Vector3.up * 0.45f;  //바닥에 깔림
        ring.localScale = new Vector3(1.5f, 0.03f, 1.5f);
        ring.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");  //적 스폰은 막지 않되 겹침 판정은 트리거로

        Transform core = RuntimeProps.Create(PrimitiveType.Cylinder, ring, new Color(0.05f, 0.05f, 0.15f));
        core.localPosition = new Vector3(0f, 0.6f, 0f);
        core.localScale = new Vector3(0.7f, 1f, 0.7f);

        SphereCollider trigger = ring.gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.45f;
        trigger.center = new Vector3(0f, 15f, 0f);  //납작한 스케일(0.03) 보정: 공 높이에 오도록

        RuntimeProps.SetColor(ring.GetComponent<Renderer>(), color, 1.5f);
        return ring.gameObject.AddComponent<RoomPortal>();
    }

    private static Vector3 Flat(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }
}

// 기믹 연출 이벤트 (CombatJuice가 받아서 그림)
public static class RoomGimmickEvents
{
    public static event System.Action<Vector3> OnBumper;
    public static event System.Action<Vector3, Vector3> OnPortal;  //들어간 위치, 나온 위치

    public static void RaiseBumper(Vector3 position) => OnBumper?.Invoke(position);
    public static void RaisePortal(Vector3 from, Vector3 to) => OnPortal?.Invoke(from, to);
}
