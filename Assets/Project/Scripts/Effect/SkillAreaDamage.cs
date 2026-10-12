using UnityEngine;

// 캐릭터 스킬 강화용 범위 피해 (돼지 충격파, 토끼 착지 충격파 등): 폭발 이펙트 + 반경 안 적에게 피해, 선택적으로 넉백.
public static class SkillAreaDamage
{
    private static readonly Collider[] buffer = new Collider[32];

    public static int Explode(Vector3 center, float radius, float damage, LayerMask enemyLayer, GameObject vfxPrefab,
        PlayerStats ownerStats, Health ownerHealth, float knockbackForce = 0f)
    {
        // 풀링된 이펙트라 크기는 건드리지 않음 (다른 스킬이 같은 이펙트를 씀)
        if (vfxPrefab != null && VFXManager.Instance != null) VFXManager.Instance.Play(vfxPrefab, center, Quaternion.identity, 1.2f);

        int count = Physics.OverlapSphereNonAlloc(center, radius, buffer, enemyLayer, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            GameObject enemy = buffer[i].attachedRigidbody != null ? buffer[i].attachedRigidbody.gameObject : buffer[i].gameObject;

            int dealt = DamageManager.Instance != null ? DamageManager.Instance.ApplyFixedDamage(enemy, damage, DamageTextType.Normal) : 0;
            if (ownerStats != null) ownerStats.ApplyLifeSteal(dealt, ownerHealth);

            if (knockbackForce > 0f && buffer[i].attachedRigidbody != null)
            {
                Vector3 away = buffer[i].transform.position - center;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
                away.y = 0f;
                buffer[i].attachedRigidbody.AddForce(away.normalized * knockbackForce, ForceMode.Impulse);
            }
        }

        return count;
    }
}
