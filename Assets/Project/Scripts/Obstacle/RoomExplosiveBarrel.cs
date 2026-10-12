using UnityEngine;

// 폭발 통: 공으로 치면 터져서 주변 적에게 큰 피해 (방마다 한 번)
public class RoomExplosiveBarrel : MonoBehaviour
{
    private const float Radius = 3f;
    private const float Damage = 40f;
    private bool exploded;

    private void OnCollisionEnter(Collision collision)
    {
        if (exploded || !collision.collider.CompareTag("Player")) return;
        if (GameManager.Instance == null || GameManager.Instance.currentState != GameState.Playing) return;

        exploded = true;
        SoundManager.Instance?.PlaySFX(SoundId.RobotMissileImpact);
        AreaDamage.Explode(transform.position, Radius, Damage, new Color(1f, 0.4f, 0.1f));
        gameObject.SetActive(false);
    }
}
