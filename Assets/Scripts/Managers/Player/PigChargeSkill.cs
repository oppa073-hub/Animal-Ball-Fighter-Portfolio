using System.Collections;
using UnityEngine;

public class PigChargeSkill : MonoBehaviour, ISkill
{
    [Header("Charge")]
    [SerializeField] private float duration = 1.5f;
    [SerializeField] private float speedMultiplier = 1.8f;
    [SerializeField] private float collisionDamageMultiplier = 1.5f;

    [Header("VFX")]
    [SerializeField] private GameObject chargeVfxPrefab;
    [SerializeField] private Transform vfxPoint;
    [SerializeField] private Vector3 vfxRotationOffset;
    private GameObject activeChargeVfx;
    private Rigidbody rb;

    private PlayerStats playerStats;
    private PlayerBallController playerController;

    private Coroutine chargeCoroutine;

    private void Awake()
    {
        playerStats = GetComponentInParent<PlayerStats>();
        playerController = GetComponentInParent<PlayerBallController>();
        rb = GetComponentInParent<Rigidbody>();
    }
    private void LateUpdate()
    {
        if (activeChargeVfx == null || !activeChargeVfx.activeSelf) return;

        Vector3 direction = rb.linearVelocity;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f) return;

        Quaternion directionRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        activeChargeVfx.transform.rotation = directionRotation * Quaternion.Euler(vfxRotationOffset);
    }
    public void UseSkill()
    {
        if (chargeCoroutine != null) StopCoroutine(chargeCoroutine);

        chargeCoroutine = StartCoroutine(Charge());
    }

    private IEnumerator Charge()
    {
        SoundManager.Instance?.PlaySFX(SoundId.PigSkill);

        Transform point = vfxPoint != null ? vfxPoint : playerController.transform;

        activeChargeVfx = VFXManager.Instance.PlayAttached(chargeVfxPrefab, point, Vector3.zero, Quaternion.Euler(vfxRotationOffset), duration);

        // 이동속도 일시 증가
        playerController.StartSpeedBoost(speedMultiplier, duration);

        // 충돌 데미지 일시 증가
        playerStats.SetTemporaryCollisionMultiplier(collisionDamageMultiplier);

        Debug.Log("[PigSkill] 돌진 시작");

        yield return new WaitForSeconds(duration);

        // 충돌 데미지 원상복구
        playerStats.ResetTemporaryCollisionMultiplier();

        activeChargeVfx = null;
        chargeCoroutine = null;

        Debug.Log("[PigSkill] 돌진 종료");
    }

    private void OnDisable()
    {
        // 스킬 도중 오브젝트가 꺼질 경우 버프가 남지 않도록 초기화
        if (playerStats != null) playerStats.ResetTemporaryCollisionMultiplier();
    }
}