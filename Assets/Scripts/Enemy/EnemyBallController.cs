using UnityEngine;

public class EnemyBallController : MonoBehaviour
{
    private EnemyController enemyController;

    private Rigidbody rb;
    private Collider enemyCollider;

    [Header("Ball Movement")]
    [SerializeField] private float baseMoveSpeed = 12f;
    [SerializeField] private float maxMoveSpeed = 35f;

    [Header("Launch")]
    [SerializeField] private float minLaunchForce = 8f;
    [SerializeField] private float maxLaunchForce = 22f;

    [Header("Bounce")]
    [SerializeField] private float wallBounceDamping = 0.95f;
    [SerializeField] private float characterCollisionDamping = 0.9f;

    [Header("Stuck Check")]
    [SerializeField] private float stuckCheckTime = 2f;

    private bool isDead;

    private Vector3 lastMoveDirection;

    private bool hasLaunched;
    private bool isPatternMoving;

    private EnemyAnimationController animationController;
    private MovementFacingController facingController;

    private void OnEnable()
    {
        PlayerBallController.OnPlayerLaunched += ShootEnemy;
        hasLaunched = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        isPatternMoving = false;
        isDead = false;

        if (enemyCollider != null) enemyCollider.enabled = true;

        facingController?.ResetFacing();
    }

    private void OnDisable()
    {
        PlayerBallController.OnPlayerLaunched -= ShootEnemy;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        enemyController = GetComponent<EnemyController>();
        animationController = GetComponent<EnemyAnimationController>();
        enemyCollider = GetComponent<Collider>();
        facingController = GetComponent<MovementFacingController>();
    }
    private void FixedUpdate()
    {
        if (isDead)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        if (GameManager.Instance.currentState == GameState.GameOver)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        if (isPatternMoving) return;

        Vector3 xzVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        if (xzVelocity.magnitude < 0.1f) return;

        lastMoveDirection = xzVelocity.normalized;

        if (xzVelocity.magnitude > maxMoveSpeed)
        {
            rb.linearVelocity = xzVelocity.normalized * maxMoveSpeed;
        }
        else rb.linearVelocity = xzVelocity.normalized * enemyController.MoveSpeed;
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (isDead) return;

        if (GameManager.Instance.currentState != GameState.Playing) return;

        if (collision.collider.CompareTag("Wall"))
        {
            if (CombatFeedbackManager.Instance != null)
            {
                CombatFeedbackManager.Instance.RequestSfx(SoundId.EnemyHit, priority: 0);
            }
            else
            {
                SoundManager.Instance?.PlaySFX(SoundId.EnemyHit);
            }

            float randomAngle = Random.Range(-18f, 18f);
            Quaternion randomRotation = Quaternion.Euler(0f, randomAngle, 0f);
            rb.linearVelocity = randomRotation * rb.linearVelocity; //벽과 충돌 시 약간의 랜덤한 방향으로 이동
        }

        if (collision.collider.CompareTag("Player"))
        {
            animationController?.PlayMeleeAttack();

            float currentSpeed = rb.linearVelocity.magnitude;

            DamageManager.Instance.ApplyDamage(collision.gameObject, enemyController.Attack, currentSpeed, DamageTextType.Normal, 1f, true);

            if (enemyController.StatusEffectOnHit != null)
            {
                StatusEffectReceiver statusEffectReceiver = collision.gameObject.GetComponent<StatusEffectReceiver>();
                if (statusEffectReceiver != null)
                {
                    statusEffectReceiver.ApplyStatus(enemyController.StatusEffectOnHit);
                }
            }

            float randomAngle = Random.Range(-18f, 18f);
            Quaternion randomRotation = Quaternion.Euler(0f, randomAngle, 0f);

            Vector3 velocity = rb.linearVelocity;
            velocity.y = 0f;

            rb.linearVelocity = randomRotation * velocity;  //플레이어와 충동 반사 방향 이동
        }
    }

    public void ShootEnemy()
    {
        if (isDead) return;

        if (GameManager.Instance.currentState == GameState.GameOver) return;
        if (hasLaunched == true) return;

        float randomAngle = Random.Range(0f, 360f);
        float radian = randomAngle * Mathf.Deg2Rad;
        Vector3 direction = new Vector3(Mathf.Cos(radian), 0, Mathf.Sin(radian));

        var randomForce = Random.Range(enemyController.MoveSpeed, maxLaunchForce);

        rb.AddForce(direction * randomForce, ForceMode.Impulse); //발사
        hasLaunched = true;
    }
    public void PrepareForPlayerRevive()
    {
        if (isDead) return;

        hasLaunched = false;
        isPatternMoving = false;
        lastMoveDirection = Vector3.zero;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
    public void SetDead(bool dead)
    {
        isDead = dead;

        if (dead)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (enemyCollider != null) enemyCollider.enabled = !dead;
    }

    public void SetPatternMoving(bool move)
    {
        isPatternMoving = move;

        if (isDead) return;

        // 패턴이 끝났는데 완전히 멈춰있으면 일반 이동 복구
        if (!move)
        {
            Vector3 xzVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

            if (xzVelocity.magnitude < 0.1f)
            {
                Vector3 direction = lastMoveDirection;

                // 이전 이동 방향이 없는 경우
                if (direction.sqrMagnitude < 0.01f)
                {
                    float randomAngle = Random.Range(0f, 360f);
                    float radian = randomAngle * Mathf.Deg2Rad;

                    direction = new Vector3(
                        Mathf.Cos(radian),
                        0f,
                        Mathf.Sin(radian)
                    );
                }

                rb.linearVelocity = direction.normalized * enemyController.MoveSpeed;
            }
        }
    }
}
