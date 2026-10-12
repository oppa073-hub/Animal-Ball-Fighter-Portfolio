using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class MovementFacingController : MonoBehaviour
{
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private Transform visualRoot;

    [Header("Facing")]
    [SerializeField] private float turnSpeed = 14f;
    [SerializeField] private float minimumSpeed = 0.15f;
    [SerializeField] private float modelYawOffset;

    private bool hasFacingOverride;
    private Vector3 facingOverrideDirection;
    private Quaternion initialVisualLocalRotation;

    private void Reset()
    {
        targetRigidbody = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (targetRigidbody == null) targetRigidbody = GetComponent<Rigidbody>();
        if (visualRoot != null) initialVisualLocalRotation = visualRoot.localRotation;
    }
    private void OnDisable()
    {
        ClearFacingOverride();
    }

    private void LateUpdate()
    {
        if (visualRoot == null) return;

        // 보스 패턴이 지정한 방향을 우선 사용
        if (hasFacingOverride)
        {
            FaceDirection(facingOverrideDirection);
            return;
        }

        if (targetRigidbody == null) return;

        Vector3 direction = targetRigidbody.linearVelocity;
        direction.y = 0f;

        if (direction.sqrMagnitude < minimumSpeed * minimumSpeed)
        {
            return;
        }

        FaceDirection(direction);
    }

    public void FaceDirection(
        Vector3 direction,
        bool immediately = false)
    {
        if (visualRoot == null) return;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);

        if (immediately || turnSpeed <= 0f)
        {
            visualRoot.rotation = targetRotation;
            return;
        }

        float rotationRatio =
            1f - Mathf.Exp(-turnSpeed * Time.deltaTime);

        visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, targetRotation, rotationRatio);
    }
    public void SetFacingOverride(Vector3 direction, bool immediately = false)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f) return;

        facingOverrideDirection = direction.normalized;
        hasFacingOverride = true;

        if (immediately)
        {
            FaceDirection(facingOverrideDirection, true);
        }
    }

    public void ClearFacingOverride()
    {
        hasFacingOverride = false;
        facingOverrideDirection = Vector3.zero;
    }
    public void ResetFacing()
    {
        ClearFacingOverride();

        if (visualRoot == null) return;

        visualRoot.localRotation = initialVisualLocalRotation;
    }
}