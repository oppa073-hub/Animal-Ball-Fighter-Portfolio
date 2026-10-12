using UnityEngine;

// 포털: 들어가면 짝 포털로 이동 (속도 유지). 연속 이동 방지용 짧은 대기
public class RoomPortal : MonoBehaviour
{
    private const float Cooldown = 0.6f;
    private static float nextUsableTime;

    private RoomPortal linked;
    private Transform spin;

    public void Link(RoomPortal other) => linked = other;

    private void Awake() => spin = transform.childCount > 0 ? transform.GetChild(0) : null;

    private void Update()
    {
        if (spin != null) spin.Rotate(0f, 180f * Time.deltaTime, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (linked == null || !other.CompareTag("Player")) return;
        if (Time.time < nextUsableTime) return;
        if (GameManager.Instance == null || GameManager.Instance.currentState != GameState.Playing) return;

        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        nextUsableTime = Time.time + Cooldown;

        Vector3 exit = linked.transform.position;
        exit.y = rb.position.y;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        if (velocity.sqrMagnitude > 0.01f) exit += velocity.normalized * 0.9f;  //나오자마자 다시 들어가지 않게 진행 방향으로 살짝

        RoomGimmickEvents.RaisePortal(rb.position, exit);
        rb.position = exit;
        other.transform.position = exit;
        SoundManager.Instance?.PlaySFX(SoundId.PlayerDash);
    }
}
