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
    public static event Action<GameObject> OnPlayerHitEnemy;  //플레이어가 몸으로 적과 부딪힘 (돼지 돌진 충격파 등)
    public static event Action OnPlayerHitWall;  //전투 연출용
    public static event Action<Vector3> OnShieldBlocked;  //방패병 정면에 막힘 (연출용)
    public static event Action<Vector3> OnSpikeHurt;      //가시 적에 찔림 (연출용)
    public static bool LastHitWasCritical { get; private set; }  //OnPlayerHitEnemy 처리 중 크리티컬 여부 (전투 연출용)

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
    [SerializeField] private float touchRadius = 1f;  //드래그 시작 판정 반경 (월드 단위, 공 중심 기준). 엄지로 잡기 쉽도록 콜라이더(0.4)보다 넓게

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
    private PlayerPerfectBounce perfectBounce;

    // 토끼 점프 등: 수평 이동은 그대로 두고 높이만 포물선으로 올렸다 내림 (점프 중에만 Y 고정 해제)
    private bool isJumping;
    private float jumpTimer;
    private float jumpDuration;
    private float jumpHeight;
    private float jumpGroundY;
    private Action jumpLandedCallback;

    public bool IsJumping => isJumping;
    public bool IsInSkillMotion => isHeld || isJumping || speedBoostTimer > 0f;  //스킬 준비/점프/돌진(가속) 중

    // 시간 감속(TimeSlowController) 중 플레이어만 원래 속도로 움직이도록 수평 속도에 곱하는 보정값. 직전 스텝에 적용한 값을 기억해 되돌린다.
    private float appliedTimeCompensation = 1f;
    private float realFixedStep;  //실제 시간 기준 FixedUpdate 한 스텝 (감속 중에도 스킬 타이머가 실제 시간으로 흐르게)

    private bool isHeld;  //스킬 준비 동작 등으로 제자리에 멈춤

    private float CurrentMoveSpeed => (playerStats != null ? playerStats.MoveSpeed : baseMoveSpeed) * GetSlowMultiplier();

    // 장판 등 외부 감속 (출처별로 등록, 겹치면 가장 강한 감속 하나만 적용)
    private readonly System.Collections.Generic.Dictionary<object, float> slowSources = new System.Collections.Generic.Dictionary<object, float>();

    public void ApplySlow(object source, float speedMultiplier)
    {
        if (source == null) return;
        slowSources[source] = Mathf.Clamp(speedMultiplier, 0.1f, 1f);
    }

    public void RemoveSlow(object source)
    {
        if (source == null) return;
        slowSources.Remove(source);
    }

    private float GetSlowMultiplier()
    {
        float multiplier = 1f;
        foreach (float value in slowSources.Values)
        {
            if (value < multiplier) multiplier = value;
        }
        return multiplier;
    }

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

        perfectBounce = GetComponent<PlayerPerfectBounce>();
        if (perfectBounce == null) perfectBounce = gameObject.AddComponent<PlayerPerfectBounce>();  //씬/프리팹 수정 없이 퍼펙트 반사 추가
    }

    private void Start()
    {
        if (health != null)
        {
            health.Initialize(playerStats.MaxHp);
            lastKnownHp = health.CurrentHp;  //첫 피격부터 진동이 나도록 기준값 설정
        }
    }
    private void OnEnable()
    {
        if (health != null)
        {
            health.OnDeath += HandlePlayerDeath;
            health.OnHealthChanged += HandleHealthChanged;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDeath -= HandlePlayerDeath;
            health.OnHealthChanged -= HandleHealthChanged;
        }

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
            if (isJumping && GameManager.Instance.currentState != GameState.Paused) EndJump();  //방 클리어/게임오버 등: 공중에 남지 않도록 착지 처리 (버프 없음)

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            appliedTimeCompensation = 1f;
            return;
        }

        float compensation = TimeSlowController.PlayerCompensation;

        ScaleHorizontalVelocity(1f / appliedTimeCompensation);  //지난 스텝의 보정을 걷어내고 원래 속도 기준으로 계산
        appliedTimeCompensation = compensation;
        realFixedStep = Time.fixedDeltaTime * compensation;

        UpdateHorizontalVelocity();
        UpdateJump();

        ScaleHorizontalVelocity(compensation);
    }

    private void ScaleHorizontalVelocity(float scale)
    {
        if (Mathf.Approximately(scale, 1f)) return;

        Vector3 velocity = rb.linearVelocity;
        velocity.x *= scale;
        velocity.z *= scale;
        rb.linearVelocity = velocity;
    }

    private void UpdateHorizontalVelocity()
    {
        if (isHeld)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        if (speedBoostTimer > 0f)  //돼지 돌진등의 스킬 사용중 속도 유지
        {
            speedBoostTimer -= realFixedStep;

            Vector3 direction = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).normalized;  //점프 중 수직 속도는 제외

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
            boostTimer -= realFixedStep;

            if (xzVelocity.magnitude > maxMoveSpeed)  
            {
                rb.linearVelocity = xzVelocity.normalized * maxMoveSpeed;
            }

            return;
        }

        rb.linearVelocity = xzVelocity.normalized * CurrentMoveSpeed;
        //Debug.Log(rb.linearVelocity);
    }

    public bool StartJump(float height, float duration, Action onLanded)  //착지하면 onLanded 호출 (취소되면 호출 안 함)
    {
        if (isJumping || duration <= 0f) return false;
        if (GameManager.Instance == null || GameManager.Instance.currentState != GameState.Playing) return false;

        isJumping = true;
        jumpTimer = 0f;
        jumpDuration = duration;
        jumpHeight = height;
        jumpGroundY = rb.position.y;
        jumpLandedCallback = onLanded;

        rb.constraints &= ~RigidbodyConstraints.FreezePositionY;
        return true;
    }

    private void UpdateJump()
    {
        if (!isJumping) return;

        jumpTimer += realFixedStep;
        float t = jumpTimer / jumpDuration;

        if (t >= 1f)
        {
            Action landedCallback = jumpLandedCallback;
            EndJump();
            landedCallback?.Invoke();
            return;
        }

        float targetY = jumpGroundY + jumpHeight * 4f * t * (1f - t);

        Vector3 velocity = rb.linearVelocity;
        velocity.y = (targetY - rb.position.y) / Time.fixedDeltaTime;
        rb.linearVelocity = velocity;
    }

    private void EndJump()  //원래 높이로 내려놓고 Y 고정 복구
    {
        if (!isJumping) return;

        isJumping = false;
        jumpLandedCallback = null;

        Vector3 position = rb.position;
        position.y = jumpGroundY;
        rb.position = position;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;

        rb.constraints |= RigidbodyConstraints.FreezePositionY;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (GameManager.Instance.currentState != GameState.Playing) return;

        if (collision.collider.CompareTag("Wall"))
        {
            playerStats.AddWallReboundStack();  //반동 충전 증강
            launchFeedback?.Impact(0.16f);  //벽에 부딪혀 찌그러졌다 돌아옴
            OnPlayerHitWall?.Invoke();

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
            float currentSpeed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude / appliedTimeCompensation;  //점프 중 수직 속도와 시간 감속 보정은 피해에 넣지 않음

            bool forcedCritical = CharacterAugmentEffects.ConsumeForcedCritical();  //토끼 전용 증강: 퍼펙트 반사 다음 타격
            bool isCritical = forcedCritical || UnityEngine.Random.value < playerStats.CriticalChance;

            DamageTextType textType = isCritical ? DamageTextType.Critical : DamageTextType.Normal;

            float damageMultiplier = playerStats.FinalCollisionDamageMultiplier;  //증강 배율 + 캐릭터 스킬 임시 배율 합친 최종 피해 배율

            damageMultiplier *= playerStats.ConsumeWallReboundMultiplier();  //반동 충전 중첩 소모

            damageMultiplier *= ComboSystem.RegisterHit();  //콤보 보너스

            bool perfectCharged = perfectBounce != null && perfectBounce.IsCharged;
            damageMultiplier *= perfectBounce != null ? perfectBounce.ConsumeCharge() : 1f;  //퍼펙트 반사 충전

            // 특수 적: 방패 정면은 피해 감소 (퍼펙트 충전 타격은 방패를 뚫음), 가시는 부딪힌 플레이어도 피해 (퍼펙트 충전/스킬 돌진 중엔 안전)
            EnemyRoleController enemyRole = collision.gameObject.GetComponent<EnemyRoleController>();
            bool shieldBlocked = enemyRole != null && !perfectCharged && enemyRole.BlocksHitFrom(transform.position);
            if (shieldBlocked) damageMultiplier *= EnemyRoleController.ShieldDamageMultiplier;
            bool spikeHurt = enemyRole != null && enemyRole.IsSpiky && !perfectCharged && !IsInSkillMotion;
            HapticFeedback.Light();

            if (isCritical)
            {
                damageMultiplier *= playerStats.CriticalDamage;
            }

            int dealtDamage = DamageManager.Instance.ApplyDamage(collision.gameObject,playerStats.Attack, currentSpeed, textType, damageMultiplier, true);

            playerStats.ApplyLifeSteal(dealtDamage, health);
            LastHitWasCritical = isCritical;
            OnPlayerHitEnemy?.Invoke(collision.gameObject);

            if (shieldBlocked) OnShieldBlocked?.Invoke(collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position);

            if (spikeHurt)
            {
                DamageManager.Instance.ApplyFixedDamage(gameObject, EnemyRoleController.SpikeDamage, DamageTextType.Normal);
                OnSpikeHurt?.Invoke(transform.position);
            }
            launchFeedback?.Impact(isCritical ? 0.3f : 0.22f);

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
    private float lastKnownHp = -1f;

    private void HandleHealthChanged(float currentHp, float maxHp)  //피격 진동
    {
        if (lastKnownHp >= 0f && currentHp < lastKnownHp && currentHp > 0f)
        {
            HapticFeedback.Medium();
            GameEvents.RaisePlayerDamaged();
        }

        lastKnownHp = currentHp;
    }

    public void HandlePlayerDeath()
    {
        animationController?.PlayDie();
        GameManager.Instance.GameOver();
    }

    public void ResetForNewRoom(Vector3 position)  //라운드 시작때 플레이어 초기화
    {
        EndJump();
        isHeld = false;
        appliedTimeCompensation = 1f;

        rb.position = position;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        boostTimer = 0f;
        isDragging = false;
        lastMoveDirection = Vector3.zero;

        speedBoostTimer = 0f;
        speedBoostMultiplier = 1f;
        slowSources.Clear();

        playerStats.ResetTemporaryCollisionMultiplier();
        playerStats.ResetWallReboundStacks();

        handler.ClearAllEffects();
        launchFeedback?.ResetImmediate();
        facingController?.ResetFacing();
        animationController?.ResetAnimation();
    }
    public void SetHold(bool hold)  //true면 제자리에 멈춤 (이동 방향은 기억)
    {
        isHeld = hold;
    }

    public void StartDash(Vector3 direction, float multiplier, float duration)  //지정한 방향으로 일정 시간 가속 돌진
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = lastMoveDirection;
        if (direction.sqrMagnitude < 0.0001f) return;

        direction.Normalize();
        isHeld = false;
        lastMoveDirection = direction;

        Vector3 velocity = direction * Mathf.Min(CurrentMoveSpeed * multiplier, maxMoveSpeed) * appliedTimeCompensation;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;

        speedBoostMultiplier = multiplier;
        speedBoostTimer = duration;
    }

    public void ApplyPerfectBounceBoost(float multiplier, float duration)  //퍼펙트 반사: 현재 방향 그대로 잠깐 빨라짐 (스킬 돌진/멈춤 중에는 무시)
    {
        if (isHeld || speedBoostTimer > 0f) return;

        Vector3 velocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (velocity.sqrMagnitude < 0.01f) return;

        float boostedSpeed = Mathf.Min(velocity.magnitude * multiplier, maxMoveSpeed * appliedTimeCompensation);  //현재 속도에는 시간 감속 보정이 들어 있음

        Vector3 boosted = velocity.normalized * boostedSpeed;
        boosted.y = rb.linearVelocity.y;
        rb.linearVelocity = boosted;

        boostTimer = Mathf.Max(boostTimer, duration);  //부스트 중에는 속도를 기본값으로 되돌리지 않음
    }

    public void StartSpeedBoost(float multiplier, float duration)  //일정 시간 동안 플레이어의 이동속도 증가
    {
        speedBoostMultiplier = multiplier;
        speedBoostTimer = duration;

        Vector3 direction = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).normalized;

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

        // 공 높이의 수평면에서 누른 지점이 공 중심 반경 안이면 잡힌 것으로 처리
        Plane ballPlane = new Plane(Vector3.up, transform.position);
        if (ballPlane.Raycast(ray, out float enter))
        {
            Vector3 offset = ray.GetPoint(enter) - transform.position;
            offset.y = 0f;

            if (offset.sqrMagnitude <= touchRadius * touchRadius) return true;
        }

        // 장애물 벽이나 외곽 투명 벽 등 앞을 가리는 콜라이더가 있어도 공을 잡을 수 있도록 플레이어 레이어만 검사
        int touchMask = 1 << gameObject.layer;

        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, touchMask, QueryTriggerInteraction.Ignore))
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
