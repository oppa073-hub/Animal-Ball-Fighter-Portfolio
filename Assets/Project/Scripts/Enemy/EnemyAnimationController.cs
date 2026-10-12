using UnityEngine;

public class EnemyAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Header("Movement Animation")]
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private float movingThreshold = 0.15f;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    private static readonly int MeleeAttackHash = Animator.StringToHash("MeleeAttack");

    private static readonly int RangedAttackHash = Animator.StringToHash("RangedAttack");

    private static readonly int DeadHash = Animator.StringToHash("Dead");

    private static readonly int PrepareHash = Animator.StringToHash("Prepare");

    private void Awake()
    {
        // 풀 반환/모델 교체로 비활성화될 때 기본 자세를 복원한다
        // (안 하면 사망 자세가 남은 채 Rebind되어 그 자세가 기본값으로 저장되고, 재사용 시 누운 채로 스폰됨)
        foreach (Animator modelAnimator in GetComponentsInChildren<Animator>(true))
        {
            modelAnimator.writeDefaultValuesOnDisable = true;
        }

        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody>();

            if (targetRigidbody == null)
            {
                targetRigidbody = GetComponentInParent<Rigidbody>();
            }
        }
    }

    private void OnEnable()
    {
        ResetAnimation();
    }

    private void Update()
    {
        if (animator == null || targetRigidbody == null) return;
        if (animator.GetBool(DeadHash)) return;

        Vector3 velocity = targetRigidbody.linearVelocity;
        velocity.y = 0f;

        bool isMoving = velocity.sqrMagnitude > movingThreshold * movingThreshold;

        animator.SetBool(IsMovingHash, isMoving);
    }

    public void SetAnimator(Animator newAnimator)
    {
        animator = newAnimator;
        ResetAnimation();
    }

    private void ResetAnimation()
    {
        if (animator == null) return;

        animator.Rebind();
        animator.Update(0f);

        animator.ResetTrigger(MeleeAttackHash);
        animator.ResetTrigger(RangedAttackHash);
        animator.ResetTrigger(PrepareHash);

        animator.SetBool(DeadHash, false);
        animator.SetBool(IsMovingHash, false);
    }

    public void PlayPrepare()
    {
        if (!CanPlayAction()) return;

        animator.SetTrigger(PrepareHash);
    }

    public void PlayMeleeAttack()
    {
        if (!CanPlayAction()) return;

        animator.SetTrigger(MeleeAttackHash);
    }

    public void PlayRangedAttack()
    {
        if (!CanPlayAction()) return;

        animator.SetTrigger(RangedAttackHash);
    }

    public void PlayDie()
    {
        if (animator == null) return;

        animator.ResetTrigger(MeleeAttackHash);
        animator.ResetTrigger(RangedAttackHash);
        animator.ResetTrigger(PrepareHash);

        animator.SetBool(IsMovingHash, false);
        animator.SetBool(DeadHash, true);
    }

    private bool CanPlayAction()
    {
        return animator != null && !animator.GetBool(DeadHash);
    }
}