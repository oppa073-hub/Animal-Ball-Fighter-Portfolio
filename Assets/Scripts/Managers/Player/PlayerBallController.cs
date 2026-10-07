using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerBallController : MonoBehaviour
{
    private PlayerStats playerStats;


    private Rigidbody rb;
    private Vector3 dragStartPos;
    private bool isDragging = false;
    private Health health;
    private StatusEffectHandler handler;

    public static event Action OnPlayerLaunched;

    [Header("Ball Movement")]
    [SerializeField] private float baseMoveSpeed = 12f;
    [SerializeField] private float maxMoveSpeed = 35f;
    [SerializeField] private float minMoveSpeed = 6f;

    [Header("Launch")]
    [SerializeField] private float maxLaunchForce = 22f;

    [Header("Stuck Check")]
    [SerializeField] private float stuckCheckTime = 2f;

    [Header("Drag")]
    [SerializeField] private float maxDragDistance = 300f;
    [SerializeField] private float minDragDistance = 30f;

    [Header("Boost")]
    [SerializeField] private float boostDuration = 1.2f;
    private float boostTimer;

    [Header("Skill Timers")]
    private float speedBoostTimer;
    private float speedBoostMultiplier = 1f;

    [SerializeField] private PlayerLaunchFeedback launchFeedback;
    [SerializeField] private MovementFacingController facingController;
    [SerializeField] private PlayerAnimationController animationController;

    private Vector3 lastMoveDirection;

    private float CurrentMoveSpeed => playerStats != null ? playerStats.MoveSpeed : baseMoveSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<Health>();
        playerStats = GetComponent<PlayerStats>();
        handler = GetComponent<StatusEffectHandler>();
        if (facingController == null)
        {
            facingController = GetComponent<MovementFacingController>();
        }
        if (animationController == null)
        {
            animationController = GetComponent<PlayerAnimationController>();
        }
    }

    private void Start()
    {
        if (health != null) health.Initialize(playerStats.MaxHp);
    }
    private void OnEnable()
    {
        if (health != null)
            health.OnDeath += HandlePlayerDeath;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDeath -= HandlePlayerDeath;

        isDragging = false;
        launchFeedback?.ResetImmediate();

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopChargeSFX();
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.currentState == GameState.Paused)
        {
            if (isDragging)
            {
                isDragging = false;
                launchFeedback?.Cancel();
                SoundManager.Instance?.StopChargeSFX();
            }

            return;
        }

        if (Input.GetMouseButtonDown(0) && GameManager.Instance.currentState == GameState.Ready && !IsPointerOverUI() && IsPointerOnPlayer())  //드래그 시작
        {
            dragStartPos = Input.mousePosition;
            isDragging = true;

            SoundManager.Instance?.PlayChargeSFX(SoundId.PlayerCharge);
        }

        if (isDragging && Input.GetMouseButton(0))
        {
            Vector3 dragVector = dragStartPos - Input.mousePosition;

            float distance = dragVector.magnitude;

            if (distance > 0.01f)
            {
                Vector3 shootDirection = GetShootDirection(dragVector);

                float pullRatio = Mathf.Clamp01(distance / maxDragDistance);

                launchFeedback?.ShowPull(transform.position, shootDirection, pullRatio);
            }
            else
            {
                launchFeedback?.Cancel();
            }
        }

        if (Input.GetMouseButtonUp(0) && isDragging && GameManager.Instance.currentState == GameState.Ready)  //드래그 종료
        {
            Vector3 dragVector = dragStartPos - Input.mousePosition;  //드래그 벡터 계산

            if (dragVector.magnitude < minDragDistance)
            {
                isDragging = false;
                launchFeedback?.Cancel();
                SoundManager.Instance?.StopChargeSFX();

                return;
            }

            Vector3 shootDirection = GetShootDirection(dragVector); //드래그 벡터 카메라 방향 전환
            float force = GetLaunchForce(dragVector.magnitude); //드래그 거리 기반 발사력 계산

            if (force > CurrentMoveSpeed)
            {
                boostTimer = boostDuration;
            }
            SoundManager.Instance?.StopChargeSFX();
            SoundManager.Instance?.PlaySFX(SoundId.PlayerLaunch);

            GameManager.Instance.currentState = GameState.Playing;
            isDragging = false;
            launchFeedback?.Release();
            rb.AddForce(shootDirection * force, ForceMode.Impulse); //발사
            OnPlayerLaunched?.Invoke();
        }
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance.currentState != GameState.Playing)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        if (speedBoostTimer > 0f)  //돼지 돌진등의 스킬 사용중 속도 유지
        {
            speedBoostTimer -= Time.fixedDeltaTime;

            Vector3 direction = rb.linearVelocity.normalized;

            if (direction.sqrMagnitude > 0.01f)
            {
                float boostedSpeed = Mathf.Min(CurrentMoveSpeed * speedBoostMultiplier, maxMoveSpeed);

                rb.linearVelocity = direction * boostedSpeed;
            }

            if (speedBoostTimer <= 0f)
            {
                speedBoostMultiplier = 1f;
            }

            return;
        }

        Vector3 xzVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);


        if (GameManager.Instance.currentState == GameState.Playing && xzVelocity.magnitude < 0.1f)
        {
            if (lastMoveDirection.sqrMagnitude > 0.01f)
            {
                rb.linearVelocity = lastMoveDirection * CurrentMoveSpeed;
            }
            return;
        }

        lastMoveDirection = xzVelocity.normalized;  

        if (boostTimer > 0f)
        {
            boostTimer -= Time.fixedDeltaTime;

            if (xzVelocity.magnitude > maxMoveSpeed)  
            {
                rb.linearVelocity = xzVelocity.normalized * maxMoveSpeed;
            }

            return;
        }

        rb.linearVelocity = xzVelocity.normalized * CurrentMoveSpeed;
        //Debug.Log(rb.linearVelocity);
    }

    private void OnCollisionEnter(Collision collision)
    {
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

            float randomAngle = UnityEngine.Random.Range(-18f, 18f);

            Quaternion randomRotation = Quaternion.Euler(0f, randomAngle, 0f);

            rb.linearVelocity = randomRotation * rb.linearVelocity; //벽과 충돌 시 약간의 랜덤한 방향으로 이동
        }
        if (collision.collider.CompareTag("Enemy"))
        {
            float currentSpeed = rb.linearVelocity.magnitude;

            bool isCritical = UnityEngine.Random.value < playerStats.CriticalChance;

            DamageTextType textType = isCritical ? DamageTextType.Critical : DamageTextType.Normal;

            float damageMultiplier = playerStats.FinalCollisionDamageMultiplier;  //증강 배율 + 캐릭터 스킬 임시 배율 합친 최종 피해 배율

            if (isCritical)
            {
                damageMultiplier *= playerStats.CriticalDamage;
            }

            int dealtDamage = DamageManager.Instance.ApplyDamage(collision.gameObject,playerStats.Attack, currentSpeed, textType, damageMultiplier, true);

            playerStats.ApplyLifeSteal(dealtDamage, health);

            float randomAngle = UnityEngine.Random.Range(-18f, 18f);
            Quaternion randomRotation = Quaternion.Euler(0f, randomAngle, 0f);

            Vector3 velocity = rb.linearVelocity;
            velocity.y = 0f;

            rb.linearVelocity = randomRotation * velocity;  //적과 충동 반사 방향 이동
        }
    }

    private Vector3 GetShootDirection(Vector3 dragVector) //드래그 벡터를 카메라의 방향에 맞게 변환
    {
        Transform cam = Camera.main.transform;

        Vector3 camForward = cam.forward;
        Vector3 camRight = cam.right;

        camForward.y = 0f;
        camRight.y = 0f;

        camForward.Normalize();
        camRight.Normalize();

        Vector3 shootDirection =
            camRight * dragVector.x +
            camForward * dragVector.y;

        return shootDirection.normalized;
    }
    private float GetLaunchForce(float dragDistance)  //드래그 거리에 따라 발사력을 계산
    {
        float dragRatio = Mathf.Clamp01(dragDistance / maxDragDistance);

        return Mathf.Lerp(CurrentMoveSpeed, maxLaunchForce, dragRatio);
    }
    public void HandlePlayerDeath()
    {
        animationController?.PlayDie();
        GameManager.Instance.GameOver();
    }

    public void ResetForNewRoom(Vector3 position)  //라운드 시작때 플레이어 초기화
    {
        rb.position = position;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        boostTimer = 0f;
        isDragging = false;
        lastMoveDirection = Vector3.zero;

        speedBoostTimer = 0f;
        speedBoostMultiplier = 1f;

        playerStats.ResetTemporaryCollisionMultiplier();

        handler.ClearAllEffects();
        launchFeedback?.ResetImmediate();
        facingController?.ResetFacing();
        animationController?.ResetAnimation();
    }
    public void StartSpeedBoost(float multiplier, float duration)  //일정 시간 동안 플레이어의 이동속도 증가
    {
        speedBoostMultiplier = multiplier;
        speedBoostTimer = duration;

        Vector3 direction = rb.linearVelocity.normalized;

        if (direction.sqrMagnitude < 0.01f)
            direction = lastMoveDirection;

        if (direction.sqrMagnitude > 0.01f)
        {
            float boostedSpeed = Mathf.Min(CurrentMoveSpeed * speedBoostMultiplier, maxMoveSpeed);

            rb.linearVelocity = direction * boostedSpeed;
        }
    }
    private bool IsPointerOnPlayer()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null) return false;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return hit.collider.GetComponentInParent<PlayerBallController>() == this;
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;

        if (Input.touchCount > 0)
        {
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        }

        return EventSystem.current.IsPointerOverGameObject();
    }
}
