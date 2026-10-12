using System;
using UnityEngine;

// 일반 특수 적 역할 (EnemyData.role). EnemyController.Initialize에서 Apply로 켜고, 풀에서 다른 종류로 다시 쓰이면 None으로 꺼진다.
// 모델이 따로 없어서 방패/폭탄/가시는 기본 도형으로 만든 임시 부착물이다 (나중에 모델로 교체 가능).
// - 방패: 앞쪽 방패가 플레이어 쪽으로 천천히 돈다. 정면 충돌은 피해 감소 (PlayerBallController가 BlocksHitFrom으로 확인)
// - 폭탄: 죽을 때 폭발해 주변 적에게 피해
// - 치유: 주기적으로 주변 적 회복
// - 가시: 몸으로 부딪히면 플레이어도 피해 (퍼펙트 반사 충전 중엔 안전)
public class EnemyRoleController : MonoBehaviour, IEnemyDeathHook
{
    [Header("Shield")]
    [SerializeField] private float shieldTurnSpeed = 100f;     //초당 회전 각도
    [SerializeField] private float shieldBlockAngle = 60f;     //방패 정면 기준 이 각도 안에서 들어온 충돌은 막음
    public const float ShieldDamageMultiplier = 0.15f;

    [Header("Bomber")]
    [SerializeField] private float bombRadius = 2.4f;
    [SerializeField] private float bombDamage = 35f;

    [Header("Healer")]
    [SerializeField] private float healInterval = 3f;
    [SerializeField] private float healRadius = 4.5f;
    [SerializeField] private float healRatio = 0.15f;  //대상 최대 체력 대비

    [Header("Spiky")]
    public const int SpikeDamage = 5;

    public static event Action<Vector3, float> OnHealPulse;  //연출용 (CombatJuice)

    private static readonly Collider[] buffer = new Collider[24];

    public EnemyRole Role { get; private set; }

    private EnemyController enemy;
    private Transform player;
    private Transform shieldPivot;
    private Transform bombVisual;
    private Transform spikeVisual;
    private Vector3 shieldDirection = Vector3.forward;
    private float healTimer;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) player = playerObject.transform;
    }

    public static void Apply(EnemyController target, EnemyRole role)  //None이면 붙어 있던 역할만 끔 (새로 붙이지 않음)
    {
        if (target == null) return;

        EnemyRoleController controller = target.GetComponent<EnemyRoleController>();
        if (controller == null)
        {
            if (role == EnemyRole.None) return;
            controller = target.gameObject.AddComponent<EnemyRoleController>();
        }

        controller.SetRole(role);
    }

    private void SetRole(EnemyRole role)
    {
        Role = role;
        healTimer = healInterval * 0.5f;

        if (role == EnemyRole.Shield)
        {
            EnsureShield();
            shieldDirection = player != null ? Flat(player.position - transform.position) : transform.forward;
        }
        if (role == EnemyRole.Bomber) EnsureBomb();
        if (role == EnemyRole.Spiky) EnsureSpikes();

        if (shieldPivot != null) shieldPivot.gameObject.SetActive(role == EnemyRole.Shield);
        if (bombVisual != null) bombVisual.gameObject.SetActive(role == EnemyRole.Bomber);
        if (spikeVisual != null) spikeVisual.gameObject.SetActive(role == EnemyRole.Spiky);
    }

    // ───────── 판정 (PlayerBallController에서 호출) ─────────

    public bool BlocksHitFrom(Vector3 attackerPosition)
    {
        if (Role != EnemyRole.Shield || enemy == null || enemy.IsDead) return false;

        Vector3 toAttacker = Flat(attackerPosition - transform.position);
        return toAttacker.sqrMagnitude > 0.001f && Vector3.Angle(shieldDirection, toAttacker) <= shieldBlockAngle;
    }

    public bool IsSpiky => Role == EnemyRole.Spiky && enemy != null && !enemy.IsDead;

    // ───────── 매 프레임 ─────────

    private void Update()
    {
        if (Role == EnemyRole.None || enemy == null || enemy.IsDead) return;

        if (Role == EnemyRole.Shield) UpdateShield();
        if (Role == EnemyRole.Bomber && bombVisual != null) bombVisual.localScale = Vector3.one * (0.42f + 0.05f * Mathf.Sin(Time.time * 10f));  //째깍째깍

        if (Role == EnemyRole.Healer && GameManager.Instance != null && GameManager.Instance.currentState == GameState.Playing)
        {
            healTimer -= Time.deltaTime;
            if (healTimer <= 0f)
            {
                healTimer = healInterval;
                HealNearby();
            }
        }
    }

    private void UpdateShield()
    {
        if (player != null)
        {
            Vector3 toPlayer = Flat(player.position - transform.position);
            if (toPlayer.sqrMagnitude > 0.001f)
            {
                shieldDirection = Vector3.RotateTowards(shieldDirection, toPlayer.normalized, shieldTurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
            }
        }

        if (shieldPivot != null && shieldDirection.sqrMagnitude > 0.001f)
        {
            shieldPivot.SetPositionAndRotation(transform.position, Quaternion.LookRotation(shieldDirection, Vector3.up));  //몸 회전과 상관없이 방패 방향 유지
        }
    }

    private void HealNearby()
    {
        int mask = 1 << gameObject.layer;
        int count = Physics.OverlapSphereNonAlloc(transform.position, healRadius, buffer, mask, QueryTriggerInteraction.Ignore);
        bool healedAny = false;

        for (int i = 0; i < count; i++)
        {
            EnemyController other = buffer[i].GetComponentInParent<EnemyController>();
            if (other == null || other == enemy || other.IsDead) continue;

            Health health = other.GetComponent<Health>();
            if (health == null || health.CurrentHp >= health.MaxHp) continue;

            float amount = health.MaxHp * healRatio;
            health.Heal(amount);
            DamageTextManager.Instance?.ShowDamage(Mathf.RoundToInt(amount), other.transform.position + Vector3.up, DamageTextType.Heal);
            healedAny = true;
        }

        OnHealPulse?.Invoke(transform.position, healRadius);  //회복 대상이 없어도 기를 모으는 모습은 보여 줌
        if (healedAny) SoundManager.Instance?.PlaySFX(SoundId.HealSkill);
    }

    // ───────── 죽음: 폭탄 ─────────

    public void OnBeforeEnemyDeath(EnemyController dying)
    {
        if (Role != EnemyRole.Bomber) return;

        if (bombVisual != null) bombVisual.gameObject.SetActive(false);
        SoundManager.Instance?.PlaySFX(SoundId.RobotMissileImpact);
        AreaDamage.Explode(transform.position, bombRadius, bombDamage, new Color(1f, 0.35f, 0.15f), enemy);
    }

    // ───────── 임시 부착물 (기본 도형) ─────────

    private void EnsureShield()
    {
        if (shieldPivot != null) return;

        shieldPivot = new GameObject("RoleShield").transform;
        shieldPivot.SetParent(transform, false);

        Transform plate = CreatePrimitive(PrimitiveType.Cube, shieldPivot, new Color(0.45f, 0.7f, 1f));
        plate.localPosition = new Vector3(0f, 0.15f, 0.72f);
        plate.localScale = new Vector3(1.15f, 1.05f, 0.16f);

        Transform rim = CreatePrimitive(PrimitiveType.Cube, plate, new Color(1f, 0.85f, 0.35f));  //가운데 금색 장식
        rim.localPosition = new Vector3(0f, 0f, 0.55f);
        rim.localScale = new Vector3(0.3f, 0.3f, 0.6f);
    }

    private void EnsureBomb()
    {
        if (bombVisual != null) return;

        bombVisual = CreatePrimitive(PrimitiveType.Sphere, transform, new Color(0.15f, 0.12f, 0.12f));
        bombVisual.localPosition = new Vector3(0f, 1.15f, 0f);  //머리 위
        bombVisual.localScale = Vector3.one * 0.42f;

        Transform fuse = CreatePrimitive(PrimitiveType.Cube, bombVisual, new Color(1f, 0.45f, 0.1f));
        fuse.localPosition = new Vector3(0f, 0.6f, 0f);
        fuse.localScale = new Vector3(0.18f, 0.45f, 0.18f);
    }

    private void EnsureSpikes()
    {
        if (spikeVisual != null) return;

        spikeVisual = new GameObject("RoleSpikes").transform;
        spikeVisual.SetParent(transform, false);

        const int spikeCount = 8;
        for (int i = 0; i < spikeCount; i++)
        {
            float angle = i * 360f / spikeCount;
            Transform spike = CreatePrimitive(PrimitiveType.Cube, spikeVisual, new Color(0.85f, 0.4f, 1f));
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            spike.localPosition = direction * 0.55f + Vector3.up * 0.15f;
            spike.localRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 0f, 45f);
            spike.localScale = new Vector3(0.16f, 0.16f, 0.45f);
        }
    }

    private static Transform CreatePrimitive(PrimitiveType type, Transform parent, Color color) => RuntimeProps.Create(type, parent, color);

    private static Vector3 Flat(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }
}
