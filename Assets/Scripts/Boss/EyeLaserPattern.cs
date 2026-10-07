using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EyeLaserPattern : BossPattern
{
    [Header("Reference")]
    [SerializeField] private Transform eyePoint;

    private Transform player;
    private Rigidbody rb;
    private EnemyBallController enemyBallController;
    private EnemyAnimationController animationController;
    private MovementFacingController facingController;

    [Header("Laser")]
    [SerializeField] private float warningDuration = 1f;
    [SerializeField] private float laserRange = 20f;
    [SerializeField] private float damage = 20f;
    [SerializeField] private float fanAngle = 48f;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private float laserHitRadius = 0.5f;

    [Header("Warning")]
    [SerializeField] private GameObject warningPrefab;

    [Header("Laser Visual")]
    [SerializeField] private LineRenderer[] laserLines;
    [SerializeField] private float laserDuration = 0.5f;

    [Header("Charge VFX")]
    [SerializeField] private GameObject chargeVfxPrefab;
    [SerializeField] private Transform chargeVfxPoint;

    private Coroutine laserCoroutine;

    private readonly List<GameObject> activeWarnings = new List<GameObject>();


    protected override void Awake()
    {
        base.Awake();

        player = GameObject.FindWithTag("Player").transform;

        rb = GetComponent<Rigidbody>();
        enemyBallController = GetComponent<EnemyBallController>();
        animationController = GetComponent<EnemyAnimationController>();
        facingController = GetComponent<MovementFacingController>();

        // 보스가 처음 등장했을 때
        for (int i = 0; i < laserLines.Length; i++)
        {
            if (laserLines[i] != null)
            {
                laserLines[i].enabled = false;
            }
        }
    }


    public override void UsePattern()
    {
        if (laserCoroutine != null) return;

        // 일반 이동 중지
        enemyBallController.SetPatternMoving(true);

        // 현재 이동 완전히 정지
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        animationController?.PlayPrepare();

        SoundManager.Instance?.PlaySFX(SoundId.HeroLaserCharge);

        laserCoroutine = StartCoroutine(LaserRoutine());
    }


    private IEnumerator LaserRoutine()
    {
        if (eyePoint == null || player == null)
        {
            EndLaserPattern();
            yield break;
        }
        Vector3 lookDirection = player.position - transform.position;

        lookDirection.y = 0f;

        // 레이저 방향이 어긋나지 않도록 즉시 회전
        facingController?.SetFacingOverride(lookDirection,true);

        Vector3 fireOrigin = eyePoint.position;    // 회전한 이후의 눈 위치를 사용, 패턴 시작 순간의 눈 위치 저장

        Vector3 centerDirection = player.position - fireOrigin;  // 패턴 시작 순간 플레이어 방향 저장

        centerDirection.y = 0f;
        centerDirection.Normalize();

        // 5갈래 방향 생성
        Vector3[] directions = CreateFanDirections(centerDirection);

        // Charge VFX 시작
        if (chargeVfxPrefab != null)
        {
            Transform point =
                chargeVfxPoint != null
                ? chargeVfxPoint
                : eyePoint;

            VFXManager.Instance.PlayAttached(chargeVfxPrefab, point, Vector3.zero, Quaternion.identity, warningDuration);
        }

        // 같은 순간 경고선 생성
        ShowWarnings(fireOrigin, directions);

        // 경고가 떠 있는 동안 보스를 계속 정지
        float timer = 0f;

        while (timer < warningDuration)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            timer += Time.deltaTime;
            yield return null;
        }


        // 경고 제거
        ClearWarnings();

        SoundManager.Instance?.PlaySFX(SoundId.HeroLaserFire);

        // 실제 레이저 발사
        FireLasers(fireOrigin, directions);


        // 레이저 잠시 유지
        yield return new WaitForSeconds(laserDuration);


        HideLasers();

        EndLaserPattern();
    }


    private Vector3[] CreateFanDirections(Vector3 centerDirection)
    {
        int count = laserLines.Length;

        Vector3[] directions = new Vector3[count];

        if (count == 0) return directions;

        float startAngle = -fanAngle * 0.5f;

        float angleStep =
            count > 1
                ? fanAngle / (count - 1)
                : 0f;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + angleStep * i;

            directions[i] =Quaternion.AngleAxis(angle, Vector3.up) * centerDirection;
        }

        return directions;
    }


    private void ShowWarnings(Vector3 fireOrigin, Vector3[] directions)
    {
        if (warningPrefab == null) return;

        for (int i = 0; i < directions.Length; i++)
        {
            // Warning은 바닥에 깔아야 하므로
            // Y 방향 제거
            Vector3 groundDirection = directions[i].normalized;


            Vector3 startPosition = fireOrigin;

            // 바닥 높이
            startPosition.y = 0.05f;


            Vector3 centerPosition = startPosition + groundDirection * (laserRange * 0.5f);


            GameObject warning = Instantiate(warningPrefab, centerPosition, Quaternion.LookRotation(groundDirection));


            Vector3 scale = warning.transform.localScale;

            scale.z = laserRange;

            warning.transform.localScale = scale;


            activeWarnings.Add(warning);
        }
    }


    private void FireLasers(Vector3 fireOrigin, Vector3[] directions)
    {
        bool playerDamaged = false;

        int count = Mathf.Min(laserLines.Length, directions.Length);


        for (int i = 0; i < count; i++)
        {
            LineRenderer line = laserLines[i];

            if (line == null) continue;


            Vector3 direction = directions[i].normalized;

            Vector3 end = fireOrigin + direction * laserRange;


            line.enabled = true;

            line.SetPosition(0, fireOrigin);
            line.SetPosition(1, end);


            // 이미 다른 레이저에 맞았다면
            // 추가 데미지는 주지 않음
            if (playerDamaged) continue;


            if (Physics.SphereCast(fireOrigin, laserHitRadius, direction, out RaycastHit hit, laserRange, targetLayer))
            {
                DamageManager.Instance.ApplyFixedDamage(hit.collider.gameObject, damage, DamageTextType.Normal);

                playerDamaged = true;
            }
        }
    }


    private void ClearWarnings()
    {
        for (int i = 0; i < activeWarnings.Count; i++)
        {
            if (activeWarnings[i] != null)
            {
                Destroy(activeWarnings[i]);
            }
        }

        activeWarnings.Clear();
    }


    private void HideLasers()
    {
        for (int i = 0; i < laserLines.Length; i++)
        {
            if (laserLines[i] != null)
            {
                laserLines[i].enabled = false;
            }
        }
    }


    private void EndLaserPattern()
    {
        facingController?.ClearFacingOverride();

        enemyBallController.SetPatternMoving(false);

        rb.WakeUp();

        laserCoroutine = null;

        bossController.EndPattern();
    }


    public override void CancelPattern()
    {
        facingController?.ClearFacingOverride();

        if (laserCoroutine != null)
        {
            StopCoroutine(laserCoroutine);
            laserCoroutine = null;
        }

        ClearWarnings();

        HideLasers();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (enemyBallController != null)
        {
            enemyBallController.SetPatternMoving(false);
        }
    }
}