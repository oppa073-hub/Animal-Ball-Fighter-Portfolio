using UnityEngine;
using System.Collections;

// 마녀 패턴: 나선 저주탄.
// 발밑에 보라색 마법진이 펼쳐지고 마녀가 살짝 떠오른 채, 여러 갈래의 독 탄환을 회전시키며 뿌린다 (탄막).
// 분노(2페이즈)에서는 갈래가 늘고 회전 방향이 중간에 한 번 뒤집힌다.
public class WitchSpiralHexPattern : BossPattern
{
    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;  //WitchBullet
    [SerializeField] private float projectileDamage = 5f;
    [SerializeField] private float projectileSpeed = 6.5f;
    [SerializeField] private float projectileLifeTime = 4f;
    [SerializeField] private StatusEffectData projectileStatusEffect;
    [SerializeField] private float fireHeight = 0.6f;

    [Header("Spiral")]
    [SerializeField] private float castDuration = 0.7f;  //마법진이 펼쳐지는 시간
    [SerializeField] private float fireDuration = 2.2f;
    [SerializeField] private float shotInterval = 0.14f;
    [SerializeField] private int arms = 4;
    [SerializeField] private int rageArms = 6;
    [SerializeField] private float spinSpeed = 120f;  //도/초

    [Header("Visual")]
    [SerializeField] private Material glowMaterial;  //가산 빔 (BossGlowBeam)
    [SerializeField] private Color circleColor = new Color(1f, 0.5f, 1f, 1f);
    [SerializeField] private float circleRadius = 2.2f;
    [SerializeField] private float levitateHeight = 0.45f;

    private Rigidbody rb;
    private EnemyBallController enemyBallController;
    private EnemyAnimationController animationController;
    private RangedEnemyAttack rangedAttack;
    private Transform visualBody;
    private Vector3 visualBaseLocalPosition;

    private Transform visualRoot;
    private MagicCircleVisual circle;
    private Coroutine patternCoroutine;

    protected override void Awake()
    {
        base.Awake();

        rb = GetComponent<Rigidbody>();
        enemyBallController = GetComponent<EnemyBallController>();
        animationController = GetComponent<EnemyAnimationController>();
        rangedAttack = GetComponent<RangedEnemyAttack>();

        Animator animator = GetComponentInChildren<Animator>(true);
        visualBody = animator != null ? animator.transform : null;
        if (visualBody != null) visualBaseLocalPosition = visualBody.localPosition;

        visualRoot = new GameObject("WitchSpiralVisual").transform;
        visualRoot.SetParent(transform.parent, false);
        circle = new MagicCircleVisual(visualRoot, "SpiralCircle", glowMaterial);

        if (projectilePrefab != null && ObjectPoolManager.Instance != null) ObjectPoolManager.Instance.EnsureCapacity(projectilePrefab, 60);
    }

    private void OnDestroy()
    {
        if (visualRoot != null) Destroy(visualRoot.gameObject);
    }

    private void OnDisable()
    {
        patternCoroutine = null;
        if (rangedAttack != null) rangedAttack.Suppressed = false;
        ResetVisual();
    }

    public override void UsePattern()
    {
        if (patternCoroutine != null || projectilePrefab == null)
        {
            bossController.EndPattern();
            return;
        }

        enemyBallController?.SetPatternMoving(true);
        if (rangedAttack != null) rangedAttack.Suppressed = true;  //패턴 중에는 기본 탄 멈춤
        StopBody();

        animationController?.PlayPrepare();
        SoundManager.Instance?.PlaySFX(SoundId.WitchCast);

        patternCoroutine = StartCoroutine(PatternRoutine());
    }

    private IEnumerator PatternRoutine()
    {
        bool rage = bossController != null && bossController.CurrentPhase >= 2;
        int armCount = Mathf.Max(1, rage ? rageArms : arms);
        float rotation = Random.Range(0f, 360f);
        float circleSpin = 0f;

        // 1) 마법진이 펼쳐지며 떠오름
        float timer = 0f;
        while (timer < castDuration)
        {
            StopBody();
            float t = timer / castDuration;
            float ease = 1f - (1f - t) * (1f - t);

            circleSpin += 90f * Time.deltaTime;
            Color color = circleColor;
            color.a *= ease;
            circle.Show(transform.position, circleRadius * ease, circleSpin, color);
            SetLevitate(levitateHeight * ease, timer);

            timer += Time.deltaTime;
            yield return null;
        }

        // 2) 나선 탄막
        timer = 0f;
        float shotTimer = 0f;
        float direction = 1f;
        int shotCount = 0;

        while (timer < fireDuration)
        {
            StopBody();

            if (rage && direction > 0f && timer > fireDuration * 0.5f) direction = -1f;  //분노: 회전 반대로

            rotation += spinSpeed * direction * Time.deltaTime;
            circleSpin += 200f * direction * Time.deltaTime;

            float pulse = 0.85f + 0.15f * Mathf.Sin(timer * 20f);
            Color color = circleColor;
            color.a *= pulse;
            circle.Show(transform.position, circleRadius * pulse, circleSpin, color);
            SetLevitate(levitateHeight, timer);

            shotTimer -= Time.deltaTime;
            if (shotTimer <= 0f)
            {
                shotTimer = shotInterval;
                FireArms(rotation, armCount);

                if (shotCount++ % 3 == 0 && CombatFeedbackManager.Instance != null)
                {
                    CombatFeedbackManager.Instance.RequestSfx(SoundId.EnemyShoot, priority: 0);
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // 3) 마법진이 사라지며 내려옴
        timer = 0f;
        const float closeTime = 0.3f;
        while (timer < closeTime)
        {
            float t = 1f - timer / closeTime;
            Color color = circleColor;
            color.a *= t;
            circle.Show(transform.position, circleRadius * (1f + (1f - t) * 0.4f), circleSpin, color);
            SetLevitate(levitateHeight * t, timer);

            timer += Time.deltaTime;
            yield return null;
        }

        ResetVisual();

        enemyBallController?.SetPatternMoving(false);
        if (rangedAttack != null) rangedAttack.Suppressed = false;
        rb?.WakeUp();

        patternCoroutine = null;
        bossController.EndPattern();
    }

    private void FireArms(float rotation, int armCount)
    {
        if (ObjectPoolManager.Instance == null) return;

        Vector3 origin = transform.position + Vector3.up * fireHeight;
        origin.y = Mathf.Max(origin.y, fireHeight);

        for (int i = 0; i < armCount; i++)
        {
            float angle = (rotation + i * 360f / armCount) * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            GameObject bullet = ObjectPoolManager.Instance.GetObject(projectilePrefab, origin + direction * 0.6f, Quaternion.LookRotation(direction));
            if (bullet == null) continue;

            EnemyProjectile projectile = bullet.GetComponent<EnemyProjectile>();
            if (projectile == null)
            {
                bullet.GetComponent<PooledObject>()?.ReturnToPool();
                continue;
            }

            projectile.Initialize(direction, projectileDamage, projectileSpeed, projectileLifeTime, projectileStatusEffect);
        }
    }

    private void SetLevitate(float height, float time)
    {
        if (visualBody == null) return;

        float bob = height > 0.01f ? Mathf.Sin(time * 4f) * 0.06f : 0f;
        visualBody.localPosition = visualBaseLocalPosition + Vector3.up * (height + bob);
    }

    private void ResetVisual()
    {
        circle?.Hide();
        if (visualBody != null) visualBody.localPosition = visualBaseLocalPosition;
    }

    private void StopBody()
    {
        if (rb == null || rb.isKinematic) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public override void CancelPattern()
    {
        if (patternCoroutine != null)
        {
            StopCoroutine(patternCoroutine);
            patternCoroutine = null;
        }

        ResetVisual();
        StopBody();
        enemyBallController?.SetPatternMoving(false);
        if (rangedAttack != null) rangedAttack.Suppressed = false;
    }
}
