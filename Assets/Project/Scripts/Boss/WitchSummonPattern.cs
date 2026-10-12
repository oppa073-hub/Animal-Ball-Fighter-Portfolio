using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 마녀 패턴2 (페이즈 2부터): 자기 주변(맵 안)에 일반 적 한 무리를 소환한다.
// 그 무리가 전부 죽으면 respawnDelay초 뒤에 다음 무리를 소환한다. 마녀가 죽으면 남은 소환수도 함께 죽어서 방이 클리어된다.
public class WitchSummonPattern : BossPattern
{
    [Header("Summon")]
    [SerializeField] private int summonCount = 3;
    [SerializeField] private float respawnDelay = 4f;  //무리가 전멸한 뒤 다음 소환까지 대기 시간
    [SerializeField] private float minRadius = 1.8f;
    [SerializeField] private float maxRadius = 3.5f;
    [SerializeField] private float minPlayerDistance = 2.5f;  //플레이어 바로 옆에 소환되지 않도록
    [SerializeField] private float castDuration = 0.8f;
    [SerializeField] private GameObject summonVfxPrefab;  //선택 (비어 있으면 생략)

    [Header("Visual")]
    [SerializeField] private Material glowMaterial;  //가산 빔 (BossGlowBeam), 비어 있으면 마법진 생략
    [SerializeField] private Color circleColor = new Color(1f, 0.5f, 1f, 1f);
    [SerializeField] private float circleRadius = 2.6f;

    private Transform visualRoot;
    private MagicCircleVisual castCircle;
    private readonly List<MagicCircleVisual> summonCircles = new List<MagicCircleVisual>();

    private Rigidbody rb;
    private Health health;
    private EnemySpawner spawner;
    private EnemyBallController enemyBallController;
    private EnemyAnimationController animationController;

    private Coroutine patternCoroutine;
    private bool isWaveAlive;
    private float nextWaveTime;
    private readonly HashSet<EnemyController> summons = new HashSet<EnemyController>();
    private readonly List<EnemyController> pruneBuffer = new List<EnemyController>();

    protected override void Awake()
    {
        base.Awake();

        rb = GetComponent<Rigidbody>();
        health = GetComponent<Health>();
        enemyBallController = GetComponent<EnemyBallController>();
        animationController = GetComponent<EnemyAnimationController>();

        if (glowMaterial != null)
        {
            visualRoot = new GameObject("WitchSummonVisual").transform;
            visualRoot.SetParent(transform.parent, false);
            castCircle = new MagicCircleVisual(visualRoot, "SummonCastCircle", glowMaterial);
        }

        if (summonVfxPrefab != null && ObjectPoolManager.Instance != null) ObjectPoolManager.Instance.EnsureCapacity(summonVfxPrefab, summonCount * 2);
    }

    private void OnDestroy()
    {
        if (visualRoot != null) Destroy(visualRoot.gameObject);
    }

    private void OnEnable()
    {
        if (health != null) health.OnDeath += KillAllSummons;

        isWaveAlive = false;
        nextWaveTime = 0f;
    }

    private void Update()
    {
        if (!isWaveAlive) return;
        if (AliveSummonCount() > 0) return;

        isWaveAlive = false;
        nextWaveTime = Time.time + respawnDelay;
    }

    private void OnDisable()
    {
        if (health != null) health.OnDeath -= KillAllSummons;

        StopPatternCoroutine();
        summons.Clear();
        HideCircles();
    }

    public override bool CanUse()  //base cooldown 대신 웨이브 전멸 + respawnDelay로 판단
    {
        return !isWaveAlive && patternCoroutine == null && Time.time >= nextWaveTime;
    }

    public override void UsePattern()
    {
        if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();

        if (patternCoroutine != null || spawner == null || RoomManager.Instance == null)
        {
            bossController.EndPattern();
            return;
        }

        enemyBallController?.SetPatternMoving(true);
        StopBody();

        animationController?.PlayPrepare();
        SoundManager.Instance?.PlaySFX(SoundId.WitchLaugh);

        patternCoroutine = StartCoroutine(PatternRoutine());
    }

    private IEnumerator PatternRoutine()
    {
        // 발밑 마법진이 크게 펼쳐지며 빠르게 회전
        float timer = 0f;
        float spin = 0f;
        while (timer < castDuration)
        {
            StopBody();

            float t = timer / castDuration;
            spin += Mathf.Lerp(60f, 420f, t) * Time.deltaTime;
            Color color = circleColor;
            color.a *= Mathf.Clamp01(t * 2f);
            castCircle?.Show(transform.position, circleRadius * (1f - (1f - t) * (1f - t)), spin, color);

            timer += Time.deltaTime;
            yield return null;
        }

        int roomNumber = RoomManager.Instance.CurrentRoom;
        List<Vector3> summonPositions = new List<Vector3>();

        for (int i = 0; i < summonCount; i++)
        {
            EnemyController summon = spawner.SpawnNormalEnemyNear(transform.position, minRadius, maxRadius, minPlayerDistance, roomNumber);
            if (summon == null) continue;

            summons.Add(summon);
            summonPositions.Add(summon.transform.position);

            if (summonVfxPrefab != null) VFXManager.Instance?.Play(summonVfxPrefab, summon.transform.position, Quaternion.identity, 1.5f);
        }

        if (castCircle != null && summonPositions.Count > 0) StartCoroutine(SummonFlashRoutine(summonPositions, spin));
        else castCircle?.Hide();

        // 한 마리도 못 띄웠으면(자리/풀 부족) 대기 후 재시도
        isWaveAlive = summons.Count > 0;
        if (isWaveAlive) SoundManager.Instance?.PlaySFX(SoundId.HeroPoisonSpawn);
        else nextWaveTime = Time.time + respawnDelay;

        enemyBallController?.SetPatternMoving(false);
        rb?.WakeUp();

        patternCoroutine = null;
        bossController.EndPattern();
    }

    private int AliveSummonCount()
    {
        pruneBuffer.Clear();

        foreach (EnemyController summon in summons)
        {
            if (summon == null || summon.IsDead || !summon.gameObject.activeInHierarchy) pruneBuffer.Add(summon);
        }

        foreach (EnemyController dead in pruneBuffer) summons.Remove(dead);

        return summons.Count;
    }

    private void KillAllSummons()  //마녀 사망 시
    {
        StopPatternCoroutine();
        AliveSummonCount();

        pruneBuffer.Clear();
        pruneBuffer.AddRange(summons);
        summons.Clear();

        foreach (EnemyController summon in pruneBuffer)
        {
            Health summonHealth = summon.GetComponent<Health>();
            if (summonHealth != null && summonHealth.CurrentHp > 0) summonHealth.TakeDamage(summonHealth.CurrentHp);
        }

        pruneBuffer.Clear();
    }

    // 소환된 자리마다 작은 마법진이 번쩍 퍼지고, 발밑 마법진은 사라짐
    private IEnumerator SummonFlashRoutine(List<Vector3> positions, float spin)
    {
        while (summonCircles.Count < positions.Count)
        {
            summonCircles.Add(new MagicCircleVisual(visualRoot, "SummonCircle", glowMaterial));
        }

        const float duration = 0.6f;
        float timer = 0f;

        while (timer < duration)
        {
            float t = timer / duration;
            spin += 300f * Time.deltaTime;

            Color fade = circleColor;
            fade.a *= 1f - t;

            castCircle.Show(transform.position, circleRadius * (1f + t * 0.5f), spin, fade);

            for (int i = 0; i < positions.Count; i++)
            {
                summonCircles[i].Show(positions[i], Mathf.Lerp(0.4f, 1.3f, 1f - (1f - t) * (1f - t)), -spin, fade);
            }

            timer += Time.deltaTime;
            yield return null;
        }

        HideCircles();
    }

    private void HideCircles()
    {
        castCircle?.Hide();
        for (int i = 0; i < summonCircles.Count; i++) summonCircles[i].Hide();
    }

    private void StopBody()
    {
        if (rb == null || rb.isKinematic) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void StopPatternCoroutine()
    {
        if (patternCoroutine == null) return;

        StopCoroutine(patternCoroutine);
        patternCoroutine = null;
    }

    public override void CancelPattern()  //게임오버/풀 반환 시: 소환수는 그대로 두고 진행 중인 시전만 취소
    {
        StopPatternCoroutine();
        StopAllCoroutines();  //소환 마법진 연출
        HideCircles();
        StopBody();
        enemyBallController?.SetPatternMoving(false);
    }
}
