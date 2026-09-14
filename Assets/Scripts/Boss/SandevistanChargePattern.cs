using UnityEngine;
using System.Collections;

public class SandevistanChargePattern : BossPattern
{
    private Transform player;
    private Rigidbody rb;
    private Coroutine chargeCoroutine;
    private EnemyBallController enemyBallController;
    private EnemyAnimationController animationController; 
    private MovementFacingController facingController;

    [SerializeField] private float chargeDelay;
    [SerializeField] private float chargeForce;
    [SerializeField] private float chargeDuration;
    [SerializeField] private SandevistanEffect effect;
    [SerializeField] private float minEchoSpeed = 30f;

    [Header("Prepare VFX")]
    [SerializeField] private GameObject prepareVfxPrefab;
    [SerializeField] private Transform prepareVfxPoint;

    protected override void Awake()
    {
        base.Awake();
        player = GameObject.FindWithTag("Player").transform;
        rb = GetComponent<Rigidbody>();
        enemyBallController = GetComponent<EnemyBallController>();
        animationController = GetComponent<EnemyAnimationController>();
        facingController = GetComponent<MovementFacingController>();
    }

    public override void UsePattern()
    {
        if (chargeCoroutine != null) return;
        Debug.Log("Sandevistan UsePattern 시작");
        enemyBallController.SetPatternMoving(true);

        animationController?.PlayPrepare();

        SoundManager.Instance?.PlaySFX(SoundId.RobotChargeStart);

        Transform parent = prepareVfxPoint != null ? prepareVfxPoint : transform;

        VFXManager.Instance.PlayAttached(prepareVfxPrefab, parent, Vector3.zero, Quaternion.identity, chargeDelay);

        chargeCoroutine = StartCoroutine(WaitCharge(chargeDelay));
    }

    private IEnumerator WaitCharge(float chargeDelay)
    {
        Debug.Log("차지 시작" + Time.time);
        float timer = 0f;

        while (timer < chargeDelay)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            Vector3 prepareDirection = player.position - transform.position;

            prepareDirection.y = 0f;

            // 준비하는 동안 움직이는 플레이어를 계속 바라봄
            facingController?.SetFacingOverride(prepareDirection);

            timer += Time.deltaTime;
            yield return null;
        }
        Debug.Log("차지 완료" + Time.time);

        Vector3 direction = (player.position - gameObject.transform.position); //플레이어 방향 게산
        direction.y = 0f;
        direction.Normalize();
        facingController?.SetFacingOverride(direction, true);

        effect.StartEcho(rb, minEchoSpeed);

        SoundManager.Instance?.PlaySFX(SoundId.RobotDash);

        rb.AddForce(direction * chargeForce, ForceMode.Impulse);
        facingController?.ClearFacingOverride();

        yield return new WaitForSeconds(chargeDuration);

        enemyBallController.SetPatternMoving(false);

        effect.StopEchoLoop();

        Debug.Log("Sandevistan EndPattern 직전");
        bossController.EndPattern();
        chargeCoroutine = null;
    }
    public override void CancelPattern()
    {
        facingController?.ClearFacingOverride();

        if (chargeCoroutine != null)
        {
            StopCoroutine(chargeCoroutine);
            chargeCoroutine = null;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        enemyBallController.SetPatternMoving(false);
        effect.StopEchoLoop();
    }
}
