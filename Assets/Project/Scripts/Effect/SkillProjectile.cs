using System.Collections.Generic;
using UnityEngine;

// 캐릭터 스킬 강화용 투사체 (고양이 발톱, 병아리 깃털 등). 벽/적을 통과하며 직진하고, 닿은 적에게 한 번씩 피해.
// 프리팹 없이 코드로 만든다: 카메라를 보는 사각형(빛나는 머티리얼) + 빛 꼬리.
public class SkillProjectile : MonoBehaviour
{
    private static Mesh quadMesh;

    private Vector3 velocity;
    private float lifeTime;
    private float damage;
    private float hitRadius;
    private int remainingHits;
    private LayerMask enemyLayer;
    private PlayerStats ownerStats;
    private Health ownerHealth;

    private readonly HashSet<int> hitEnemies = new();
    private readonly Collider[] buffer = new Collider[8];
    private Transform visual;
    private float spinSpeed;

    public static void Spawn(Vector3 position, Vector3 direction, float speed, float damage, float lifeTime, int pierceCount,
        LayerMask enemyLayer, Material bodyMaterial, Material trailMaterial, Color color, float size, PlayerStats ownerStats, Health ownerHealth)
    {
        if (quadMesh == null) quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        var go = new GameObject("SkillProjectile");
        go.transform.position = position;

        SkillProjectile projectile = go.AddComponent<SkillProjectile>();
        projectile.velocity = direction.normalized * speed;
        projectile.damage = damage;
        projectile.lifeTime = lifeTime;
        projectile.hitRadius = size * 0.6f;
        projectile.remainingHits = Mathf.Max(1, pierceCount);
        projectile.enemyLayer = enemyLayer;
        projectile.ownerStats = ownerStats;
        projectile.ownerHealth = ownerHealth;
        projectile.spinSpeed = 720f;

        var visual = new GameObject("Visual").transform;
        visual.SetParent(go.transform, false);
        visual.localScale = Vector3.one * size;
        visual.gameObject.AddComponent<MeshFilter>().sharedMesh = quadMesh;
        var renderer = visual.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = bodyMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(block);
        projectile.visual = visual;

        if (trailMaterial != null)
        {
            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMaterial;
            trail.time = 0.18f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, size * 0.7f), new Keyframe(1f, 0f));
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                             new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        transform.position += velocity * dt;

        lifeTime -= dt;
        if (lifeTime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        Camera cam = Camera.main;
        if (cam != null) visual.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, Time.time * spinSpeed);

        int count = Physics.OverlapSphereNonAlloc(transform.position, hitRadius, buffer, enemyLayer, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            GameObject enemy = buffer[i].attachedRigidbody != null ? buffer[i].attachedRigidbody.gameObject : buffer[i].gameObject;
            if (!hitEnemies.Add(enemy.GetInstanceID())) continue;

            int dealt = DamageManager.Instance != null ? DamageManager.Instance.ApplyFixedDamage(enemy, damage, DamageTextType.Normal) : 0;
            if (ownerStats != null) ownerStats.ApplyLifeSteal(dealt, ownerHealth);

            remainingHits--;
            if (remainingHits <= 0)
            {
                Destroy(gameObject);
                return;
            }
        }
    }
}
