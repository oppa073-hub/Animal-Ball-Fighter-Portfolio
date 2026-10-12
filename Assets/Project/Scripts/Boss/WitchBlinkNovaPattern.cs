using UnityEngine;
using System.Collections;

// 마녀 패턴 (2페이즈): 순간이동 + 저주 고리탄.
// 보라색 불꽃과 함께 사라짐 → 나타날 자리에 마법진이 먼저 그려짐(예고) → 나타나며 사방으로 고리탄 발사.
// 이것을 여러 번 연달아 해서 마녀가 전장을 휘젓고 다니는 느낌을 준다.
public class WitchBlinkNovaPattern : BossPattern
{
    [Header("Blink")]
    [SerializeField] private int blinkCount = 3;
    [SerializeField] private float vanishDuration = 0.18f;
    [SerializeField] private float hiddenDuration = 0.45f;  //사라진 동안 다음 자리에 마법진 예고
    [SerializeField] private float appearDuration = 0.15f;
    [SerializeField] private float pauseAfterNova = 0.35f;
    [SerializeField] private float minPlayerDistance = 4f;
    [SerializeField] private float minMoveDistance = 3f;
    [SerializeField] private GameObject blinkVfxPrefab;

    [Header("Nova")]
    [SerializeField] private GameObject projectilePrefab;  //WitchBullet
    [SerializeField] private int novaBulletCount = 14;
    [SerializeField] private float projectileDamage = 5f;
    [SerializeField] private float projectileSpeed = 7f;
    [SerializeField] private float projectileLifeTime = 4f;
    [SerializeField] private StatusEffectData projectileStatusEffect;
    [SerializeField] private float fireHeight = 0.6f;

    [Header("Visual")]
    [SerializeField] private Material glowMaterial;  //가산 빔 (BossGlowBeam)
    [SerializeField] private Color circleColor = new Color(1f, 0.5f, 1f, 1f);
    [SerializeField] private float circleRadius = 1.4f;

    private Rigidbody rb;
    private Collider bodyCollider;
    private Transform player;
    private BoxCollider spawnArea;
    private EnemyBallController enemyBallController;
    private EnemyAnimationController animationController;
    private RangedEnemyAttack rangedAttack;
    private Transform visualBody;
    private Vector3 visualBaseScale = Vector3.one;

    private Transform visualRoot;
    private MagicCircleVisual circle;
    private Coroutine patternCoroutine;
    private bool isHidden;

    protected override void Awake()
    {
        base.Awake();

        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
        enemyBallController = GetComponent<EnemyBallController>();
        animationController = GetComponent<EnemyAnimationController>();
        rangedAttack = GetComponent<RangedEnemyAttack>();

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null) player = playerObject.transform;

        GameObject spawnAreaObject = GameObject.FindWithTag("SpawnArea");
        if (spawnAreaObject != null) spawnArea = spawnAreaObject.GetComponent<BoxCollider>();

        Animator animator = GetComponentInChildren<Animator>(true);
        visualBody = animator != null ? animator.transform : null;
        if (visualBody != null) visualBaseScale = visualBody.localScale;

        visualRoot = new GameObject("WitchBlinkVisual").transform;
        visualRoot.SetParent(transform.parent, false);
        circle = new MagicCircleVisual(visualRoot, "BlinkCircle", glowMaterial);

        if (ObjectPoolManager.Instance != null)
        {
            if (projectilePrefab != null) ObjectPoolManager.Instance.EnsureCapacity(projectilePrefab, 60);
            if (blinkVfxPrefab != null) ObjectPoolManager.Instance.EnsureCapacity(blinkVfxPrefab, 4);
        }
    }

    private void OnDestroy()
    {
        if (visualRoot != null) Destroy(visualRoot.gameObject);
    }

    private void OnDisable()
    {
        patternCoroutine = null;
        if (rangedAttack != null) rangedAttack.Suppressed = false;
        RestoreBody();
        circle?.Hide();
    }

    public override void UsePattern()
    {
        if (patternCoroutine != null || spawnArea == null || player == null)
        {
            bossController.EndPattern();
            return;
        }

        enemyBallController?.SetPatternMoving(true);
        if (rangedAttack != null) rangedAttack.Suppressed = true;  //패턴 중에는 기본 탄 멈춤
        StopBody();

        patternCoroutine = StartCoroutine(PatternRoutine());
    }

    private IEnumerator PatternRoutine()
    {
        animationController?.PlayPrepare();
        SoundManager.Instance?.PlaySFX(SoundId.WitchCast);

        for (int i = 0; i < Mathf.Max(1, blinkCount); i++)
        {
            // 1) 사라짐
            PlayBlinkVfx(transform.position);
            float timer = 0f;
            while (timer < vanishDuration)
            {
                StopBody();
                SetVisualScale(1f - timer / vanishDuration);
                timer += Time.deltaTime;
                yield return null;
            }

            Hide();

            // 2) 다음 자리 예고 (마법진이 그려짐)
            Vector3 destination = PickDestination();
            timer = 0f;
            float spin = Random.Range(0f, 360f);
            while (timer < hiddenDuration)
            {
                float t = timer / hiddenDuration;
                spin += 240f * Time.deltaTime;
                Color color = circleColor;
                color.a *= t;
                circle.Show(destination, circleRadius * Mathf.Lerp(0.3f, 1f, t), spin, color);

                timer += Time.deltaTime;
                yield return null;
            }

            // 3) 나타남 + 고리탄
            Appear(destination);
            PlayBlinkVfx(destination);

            timer = 0f;
            while (timer < appearDuration)
            {
                StopBody();
                SetVisualScale(Mathf.Lerp(0.2f, 1.15f, timer / appearDuration));
                timer += Time.deltaTime;
                yield return null;
            }

            SetVisualScale(1f);
            FireNova(i);

            if (CombatFeedbackManager.Instance != null)
            {
                CombatFeedbackManager.Instance.RequestImpact(SoundId.HeroPoisonSpawn, 0.12f, 0.15f, priority: 3);
            }
            else
            {
                SoundManager.Instance?.PlaySFX(SoundId.HeroPoisonSpawn);
            }

            // 마법진이 퍼지며 사라짐
            timer = 0f;
            while (timer < pauseAfterNova)
            {
                StopBody();
                float t = timer / pauseAfterNova;
                Color color = circleColor;
                color.a *= 1f - t;
                circle.Show(destination, circleRadius * (1f + t * 0.8f), spin, color);
                timer += Time.deltaTime;
                yield return null;
            }

            circle.Hide();
        }

        enemyBallController?.SetPatternMoving(false);
        if (rangedAttack != null) rangedAttack.Suppressed = false;
        rb?.WakeUp();

        patternCoroutine = null;
        bossController.EndPattern();
    }

    private Vector3 PickDestination()
    {
        Bounds bounds = spawnArea.bounds;
        Vector3 current = transform.position;
        Vector3 best = current;
        float bestScore = float.MinValue;

        for (int i = 0; i < 20; i++)
        {
            Vector3 candidate = new Vector3(Random.Range(bounds.min.x, bounds.max.x), current.y, Random.Range(bounds.min.z, bounds.max.z));

            Vector3 toPlayer = candidate - player.position; toPlayer.y = 0f;
            Vector3 toCurrent = candidate - current; toCurrent.y = 0f;

            if (toPlayer.magnitude >= minPlayerDistance && toCurrent.magnitude >= minMoveDistance) return candidate;

            float score = Mathf.Min(toPlayer.magnitude, toCurrent.magnitude);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private void FireNova(int blinkIndex)
    {
        if (projectilePrefab == null || ObjectPoolManager.Instance == null) return;

        int count = Mathf.Max(3, novaBulletCount);
        Vector3 origin = transform.position;
        origin.y = Mathf.Max(origin.y, fireHeight);

        // 첫 탄은 플레이어 쪽, 연속 순간이동마다 반 칸씩 엇갈림
        Vector3 toPlayer = player.position - transform.position; toPlayer.y = 0f;
        float baseAngle = Mathf.Atan2(toPlayer.z, toPlayer.x) * Mathf.Rad2Deg + (blinkIndex % 2) * (180f / count);

        for (int i = 0; i < count; i++)
        {
            float angle = (baseAngle + i * 360f / count) * Mathf.Deg2Rad;
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

    private void PlayBlinkVfx(Vector3 position)
    {
        if (blinkVfxPrefab == null || VFXManager.Instance == null) return;

        VFXManager.Instance.Play(blinkVfxPrefab, new Vector3(position.x, 0.1f, position.z), Quaternion.identity, 1f);
    }

    private void Hide()
    {
        isHidden = true;
        SetVisualScale(0f);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (bodyCollider != null) bodyCollider.enabled = false;  //사라진 동안은 맞지 않음
    }

    private void Appear(Vector3 destination)
    {
        transform.position = destination;
        RestoreBody();
        SetVisualScale(0.2f);
    }

    private void RestoreBody()
    {
        if (isHidden)
        {
            isHidden = false;

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
            }

            if (bodyCollider != null) bodyCollider.enabled = true;
        }

        SetVisualScale(1f);
    }

    private void SetVisualScale(float scale)
    {
        if (visualBody == null) return;

        // 가로로 좁아지며 세로로 늘어나 '빨려 들어가는' 느낌
        float vertical = scale <= 0f ? 0f : Mathf.Lerp(1.6f, 1f, scale) * scale;
        visualBody.localScale = new Vector3(visualBaseScale.x * scale, visualBaseScale.y * Mathf.Max(vertical, scale), visualBaseScale.z * scale);
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

        RestoreBody();
        circle?.Hide();
        StopBody();
        enemyBallController?.SetPatternMoving(false);
        if (rangedAttack != null) rangedAttack.Suppressed = false;
    }
}
