using System;
using System.Collections.Generic;
using UnityEngine;

// 전설 증강 효과 (공의 규칙을 바꿈). AugmentManager가 플레이어에 붙이고 Enable로 켠다. 판이 끝나면 씬과 함께 사라짐.
// 피해 수치는 플레이어 공격력 기준이며 범위/피해는 일부러 작게 잡았다 (기본 충돌이 주력).
public class LegendaryEffects : MonoBehaviour
{
    [Header("폭발 반사")]
    [SerializeField] private float explosionRadius = 1.6f;
    [SerializeField] private float explosionDamageRatio = 0.7f;  //공격력 대비
    [SerializeField] private float explosionCooldown = 0.25f;

    [Header("분열탄")]
    [SerializeField] private float splitDamageRatio = 0.6f;
    [SerializeField] private float splitSpeed = 17f;
    [SerializeField] private float splitLifeTime = 0.9f;
    [SerializeField] private float splitCooldown = 0.3f;
    [SerializeField] private float splitSearchRange = 12f;

    [Header("자석")]
    [SerializeField] private float magnetRange = 6f;
    [SerializeField] private float magnetTurnSpeed = 110f;  //초당 회전 각도 (게임 시간)
    [SerializeField] private float magnetMaxAngle = 75f;    //진행 방향 기준 이 각도 안의 적만

    [Header("퍼펙트 번개")]
    [SerializeField] private float thunderDamageRatio = 1.5f;
    [SerializeField] private float thunderRange = 12f;
    [SerializeField] private int thunderTargets = 2;

    [Header("연쇄 폭발")]
    [SerializeField] private float chainRadius = 1.8f;
    [SerializeField] private float chainDamageRatio = 0.8f;
    [SerializeField] private int chainMaxPerWindow = 4;  //연쇄가 끝없이 이어지지 않게
    [SerializeField] private float chainWindow = 0.4f;

    public static event Action<Vector3> OnThunder;            //번개 맞은 위치

    private readonly HashSet<AugmentType> active = new HashSet<AugmentType>();
    private readonly Collider[] buffer = new Collider[24];

    private PlayerBallController controller;
    private PlayerStats stats;
    private Health health;
    private Rigidbody rb;
    private int enemyMask;

    private float nextExplosionTime;
    private float nextSplitTime;
    private float chainWindowStart;
    private int chainCount;

    public bool Has(AugmentType type) => active.Contains(type);

    public static bool IsLegendary(AugmentType type)  //나중에 일반 증강이 enum 끝에 추가돼도 섞이지 않도록 명시적으로
    {
        switch (type)
        {
            case AugmentType.ExplosiveRebound:
            case AugmentType.SplitShot:
            case AugmentType.Magnet:
            case AugmentType.PerfectThunder:
            case AugmentType.ChainBlast:
                return true;
            default:
                return false;
        }
    }

    private void Awake()
    {
        controller = GetComponent<PlayerBallController>();
        stats = GetComponent<PlayerStats>();
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        enemyMask = LayerMask.GetMask("Enemy");
    }

    private void OnEnable()
    {
        PlayerBallController.OnPlayerHitWall += HandleWallHit;
        PlayerPerfectBounce.OnPerfect += HandlePerfect;
        GameEvents.EnemyKilled += HandleEnemyKilled;
    }

    private void OnDisable()
    {
        PlayerBallController.OnPlayerHitWall -= HandleWallHit;
        PlayerPerfectBounce.OnPerfect -= HandlePerfect;
        GameEvents.EnemyKilled -= HandleEnemyKilled;
    }

    public void Enable(AugmentType type)
    {
        if (IsLegendary(type)) active.Add(type);
    }

    private float Attack => stats != null ? stats.Attack : 10f;

    // ───────── 벽 충돌: 폭발 반사 / 분열탄 ─────────

    private void HandleWallHit()
    {
        Vector3 position = transform.position;

        if (Has(AugmentType.ExplosiveRebound) && Time.time >= nextExplosionTime)
        {
            nextExplosionTime = Time.time + explosionCooldown;
            Explode(position, explosionRadius, Attack * explosionDamageRatio);
        }

        if (Has(AugmentType.SplitShot) && Time.time >= nextSplitTime)
        {
            nextSplitTime = Time.time + splitCooldown;
            FireSplitShot(position);
        }
    }

    private void FireSplitShot(Vector3 position)
    {
        Collider target = FindNearestEnemy(position, splitSearchRange, Vector3.zero, 180f);

        Vector3 direction;
        if (target != null) direction = target.transform.position - position;
        else
        {
            Vector3 velocity = rb != null ? rb.linearVelocity : transform.forward;
            direction = Quaternion.Euler(0f, UnityEngine.Random.Range(-30f, 30f), 0f) * velocity;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;

        CombatUiStyle style = CombatUiStyle.Get();
        Material glow = style != null ? style.trailMaterial : null;
        if (glow == null) return;

        SkillProjectile.Spawn(position + Vector3.up * 0.3f, direction, splitSpeed, Attack * splitDamageRatio, splitLifeTime, 1,
            enemyMask, glow, glow, new Color(0.45f, 0.9f, 1f), 0.5f, stats, health);
    }

    // ───────── 자석 ─────────

    private void FixedUpdate()
    {
        if (!Has(AugmentType.Magnet) || rb == null) return;
        if (GameManager.Instance == null || GameManager.Instance.currentState != GameState.Playing) return;
        if (controller != null && controller.IsInSkillMotion) return;  //돌진/점프/대쉬 중엔 방향을 건드리지 않음

        Vector3 velocity = rb.linearVelocity;
        float y = velocity.y;
        velocity.y = 0f;
        if (velocity.sqrMagnitude < 0.25f) return;

        Collider target = FindNearestEnemy(transform.position, magnetRange, velocity.normalized, magnetMaxAngle);
        if (target == null) return;

        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;

        Vector3 steered = Vector3.RotateTowards(velocity, toTarget.normalized * velocity.magnitude, magnetTurnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
        steered.y = y;
        rb.linearVelocity = steered;
    }

    // ───────── 퍼펙트 번개 ─────────

    private void HandlePerfect(Vector3 position, int chain)
    {
        if (!Has(AugmentType.PerfectThunder)) return;

        int count = Physics.OverlapSphereNonAlloc(position, thunderRange, buffer, enemyMask, QueryTriggerInteraction.Ignore);
        if (count == 0) return;

        Array.Sort(buffer, 0, count, Comparer<Collider>.Create((a, b) =>
            (a.transform.position - position).sqrMagnitude.CompareTo((b.transform.position - position).sqrMagnitude)));

        List<EnemyController> targets = new List<EnemyController>(thunderTargets);  //피해 중 연쇄 폭발이 buffer를 다시 쓰므로 먼저 골라 둠
        for (int i = 0; i < count && targets.Count < thunderTargets; i++)
        {
            EnemyController enemy = buffer[i].GetComponentInParent<EnemyController>();
            if (enemy != null && !enemy.IsDead && !targets.Contains(enemy)) targets.Add(enemy);
        }

        foreach (EnemyController enemy in targets)
        {
            if (enemy == null || enemy.IsDead) continue;

            Vector3 strikePosition = enemy.transform.position;
            DamageManager.Instance?.ApplyFixedDamage(enemy.gameObject, Attack * thunderDamageRatio, DamageTextType.Critical);
            OnThunder?.Invoke(strikePosition);
        }

        if (targets.Count > 0) SoundManager.Instance?.PlaySFX(SoundId.LightningSkill);
    }

    // ───────── 연쇄 폭발 ─────────

    private void HandleEnemyKilled(EnemyController enemy, bool isBoss)
    {
        if (!Has(AugmentType.ChainBlast) || enemy == null || isBoss) return;

        if (Time.time - chainWindowStart > chainWindow)
        {
            chainWindowStart = Time.time;
            chainCount = 0;
        }

        if (chainCount >= chainMaxPerWindow) return;
        chainCount++;

        AreaDamage.Explode(enemy.transform.position, chainRadius, Attack * chainDamageRatio, AreaDamage.FireColor, enemy);  //죽는 적 자신은 제외
    }

    // ───────── 공용 ─────────

    private static void Explode(Vector3 position, float radius, float damage)
    {
        AreaDamage.Explode(position, radius, damage, AreaDamage.FireColor);
    }

    private Collider FindNearestEnemy(Vector3 position, float range, Vector3 forward, float maxAngle)
    {
        int count = Physics.OverlapSphereNonAlloc(position, range, buffer, enemyMask, QueryTriggerInteraction.Ignore);

        Collider best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            EnemyController enemy = buffer[i].GetComponentInParent<EnemyController>();
            if (enemy == null || enemy.IsDead) continue;

            Vector3 offset = buffer[i].transform.position - position;
            offset.y = 0f;

            if (forward.sqrMagnitude > 0.01f && Vector3.Angle(forward, offset) > maxAngle) continue;

            float distance = offset.sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = buffer[i];
            }
        }

        return best;
    }
}
