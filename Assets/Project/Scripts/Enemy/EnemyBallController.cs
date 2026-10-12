using UnityEngine;

public class EnemyBallController : MonoBehaviour
{
    private EnemyController enemyController;

    private Rigidbody rb;
    private Collider enemyCollider;

    [Header("Ball Movement")]
    [SerializeField] private float baseMoveSpeed = 12f;
    [SerializeField] private float maxMoveSpeed = 35f;
    [SerializeField] private float patternMaxSpeed = 70f;  //보스 패턴 이동 중 상한 (로봇 돌진 속도 60보다 약간 높게)

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
    private IEnemyContactOverride contactOverride;

    // 보스 산데비스탄으로 이 적만 가속 중일 때의 속도 보정 배율 (rb 속도에 곱해져 있음)
    private float appliedTimeCompensation = 1f;
    public float TimeCompensation => appliedTimeCompensation;

    public void ResetTimeCompensation()  //감속이 갑자기 풀렸을 때 지금 넣은 속도를 그대로 쓰도록 (다시 나누지 않음)
    {
        appliedTimeCompensation = 1f;
    }

    private void OnEnable()
    {
        PlayerBallController.OnPlayerLaunched += ShootEnemy;
        hasLaunched = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        isPatternMoving = false;
        isDead = false;
        appliedTimeCompensation = 1f;

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
        contactOverride = GetComponent<IEnemyContactOverride>();
    }
    private void FixedUpdate()
    {
        if (rb.isKinematic) return;  //보스 도약 등 패턴이 위치를 직접 움직이는 중

        if (isDead)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            appliedTimeCompensation = 1f;
            return;
        }

        if (GameManager.Instance.currentState == GameState.GameOver)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            appliedTimeCompensation = 1f;
            return;
        }

        // 보스 산데비스탄: 이 보스만 원래 속도로 움직이도록 보정 (배율이 바뀐 만큼만 다시 곱함)
        float compensation = TimeSlowController.GetEnemyCompensation(transform);
        if (!Mathf.Approximately(compensation, appliedTimeCompensation))
        {
            float ratio = compensation / appliedTimeCompensation;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x * ratio, rb.linearVelocity.y, rb.linearVelocity.z * ratio);
            appliedTimeCompensation = compensation;
        }

        if (isPatternMoving)
        {
            // 패턴(로봇 돌진 등) 중에도 속도 상한은 둔다. 산데비스탄 중인 플레이어(시간 보정으로 매우 빠름)와 부딪히면
            // 순간 속도가 너무 커져 한 물리 스텝에 벽을 뚫고 맵 밖으로 날아가던 문제 방지
            Vector3 patternVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            float patternLimit = patternMaxSpeed * appliedTimeCompensation;
            if (patternVelocity.magnitude > patternLimit)
            {
                rb.linearVelocity = patternVelocity.normalized * patternLimit + Vector3.up * rb.linearVelocity.y;
            }
            return;
        }

        Vector3 xzVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        if (xzVelocity.magnitude < 0.1f) return;

        lastMoveDirection = xzVelocity.normalized;

        if (xzVelocity.magnitude > maxMoveSpeed * appliedTimeCompensation)
        {
            rb.linearVelocity = xzVelocity.normalized * maxMoveSpeed * appliedTimeCompensation;
        }
        else rb.linearVelocity = xzVelocity.normalized * enemyController.MoveSpeed * appliedTimeCompensation;
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
            if (contactOverride != null && contactOverride.HandlePlayerContact(collision)) return;  //산데비스탄 충돌 등 패턴이 직접 처리

            animationController?.PlayMeleeAttack();

            // 일반 스테이지: 몸통 피해는 적 공격력 × 배율로 고정 (예전엔 속도가 피해 대부분을 차지해 일반 적도 12~20씩 아팠음)
            //   맞은 뒤 잠깐은 몸통 피해 무적이라 한 번 쏴서 적 무리를 지나가도 연달아 맞지 않음
            // 무한 모드: 랭킹 기록이 이어지도록 예전 공식(공격력 + 속도) 그대로, 무적 없음
            bool endless = GameSessionData.IsEndless;
            if (endless || PlayerDamageRules.TryConsumeContactHit())
            {
                if (endless)
                {
                    float currentSpeed = rb.linearVelocity.magnitude / appliedTimeCompensation;  //시간 보정으로 빨라진 만큼은 피해에 넣지 않음
                    DamageManager.Instance.ApplyDamage(collision.gameObject, enemyController.Attack, currentSpeed, DamageTextType.Normal, 1f, true);
                }
                else
                {
                    DamageManager.Instance.ApplyFixedDamage(collision.gameObject, enemyController.Attack * PlayerDamageRules.ContactDamageMultiplier, DamageTextType.Normal);
                }

                if (enemyController.StatusEffectOnHit != null)
                {
                    StatusEffectReceiver statusEffectReceiver = collision.gameObject.GetComponent<StatusEffectReceiver>();
                    if (statusEffectReceiver != null)
                    {
                        statusEffectReceiver.ApplyStatus(enemyController.StatusEffectOnHit);
                    }
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

        if (isDead || rb.isKinematic) return;

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

                rb.linearVelocity = direction.normalized * enemyController.MoveSpeed * appliedTimeCompensation;
            }
        }
    }
}

// 플레이어와 부딪혔을 때 기본 접촉 피해 대신 직접 처리하고 싶은 보스 패턴이 구현 (true를 돌려주면 기본 처리 생략)
public interface IEnemyContactOverride
{
    bool HandlePlayerContact(Collision collision);
}
