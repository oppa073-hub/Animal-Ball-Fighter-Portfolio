using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody targetRigidbody;

    [Header("Movement")]
    [SerializeField] private float movingThreshold = 0.15f;

    private static readonly int IsMovingHash =
        Animator.StringToHash("IsMoving");

    private static readonly int DeadHash =
        Animator.StringToHash("Dead");

    private void Awake()
    {
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

    public void PlayDie()
    {
        if (animator == null) return;

        animator.SetBool(IsMovingHash, false);
        animator.SetBool(DeadHash, true);
    }

    public void ResetAnimation()
    {
        if (animator == null) return;

        animator.Rebind();
        animator.Update(0f);

        animator.SetBool(IsMovingHash, false);
        animator.SetBool(DeadHash, false);
    }
}