using UnityEngine;

// 범퍼: 부딪히면 잠깐 가속 + 통통 튀는 연출
public class RoomBumper : MonoBehaviour
{
    private Vector3 baseScale;

    private void Awake() => baseScale = transform.localScale;

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag("Player")) return;
        if (GameManager.Instance == null || GameManager.Instance.currentState != GameState.Playing) return;

        collision.collider.GetComponentInParent<PlayerBallController>()?.ApplyPerfectBounceBoost(1.35f, 0.6f);

        transform.localScale = baseScale;
        StopAllCoroutines();
        StartCoroutine(Punch());
        SoundManager.Instance?.PlaySFX(SoundId.PlayerLaunch);
        RoomGimmickEvents.RaiseBumper(transform.position);
    }

    private System.Collections.IEnumerator Punch()
    {
        float time = 0f;
        while (time < 0.25f)
        {
            time += Time.unscaledDeltaTime;
            float wave = Mathf.Sin(time / 0.25f * Mathf.PI) * 0.25f;
            transform.localScale = new Vector3(baseScale.x * (1f + wave), baseScale.y * (1f - wave * 0.5f), baseScale.z * (1f + wave));
            yield return null;
        }
        transform.localScale = baseScale;
    }
}
