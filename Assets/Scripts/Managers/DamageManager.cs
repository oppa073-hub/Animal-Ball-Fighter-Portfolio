using System;
using UnityEngine;

public class DamageManager : MonoBehaviour
{
    public static DamageManager Instance { get; private set; }
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private GameObject shieldBreakEffectPrefab;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void PlayHitEffect(Vector3 position)
    {
        if (hitEffectPrefab != null)
        {
            ObjectPoolManager.Instance.GetObject(hitEffectPrefab, position, Quaternion.identity);
        }
    }
    public void PlayShieldBreakEffect(Vector3 position)
    {
        if (shieldBreakEffectPrefab == null) return;
        if (ObjectPoolManager.Instance == null) return;

        GameObject effectObject =ObjectPoolManager.Instance.GetObject(shieldBreakEffectPrefab,position,Quaternion.identity);

        if (effectObject == null)
        {
            Debug.LogWarning("[Shield] 파괴 이펙트 풀 부족");

            return;
        }

        ShieldBreakEffectController effectController = effectObject.GetComponent<ShieldBreakEffectController>();

        if (effectController == null)
        {
            Debug.LogError("[Shield] ShieldBreakEffectController 없음");

            effectObject.GetComponent<PooledObject>()?.ReturnToPool();

            return;
        }

        effectController.Play(position);
    }

    public int ApplyDamage(GameObject target, float attack, float currentSpeed, DamageTextType type, float damageMultiplier = 1f, bool showHitFlash = true, float shakeDuration = 0f, float shakeStrength = 0f)
    {
        var health = target.GetComponent<Health>();

        if (health == null) return 0;

        int damage = CalculateCollisionDamage(attack, currentSpeed);
        damage = Mathf.RoundToInt(damage * damageMultiplier);  //크리틱컬 적용

        PlayerShield shield = target.GetComponent<PlayerShield>();

        if (shield != null && shield.TryAbsorbDamage(damage))
        {
            Debug.Log($"[Shield] 피해 {damage} 흡수");
            return 0;
        }

        health.TakeDamage(damage);
        PlayHitEffect(target.transform.position + Vector3.up * 0.5f);

        if (showHitFlash)
        {
            target.GetComponent<HitFlash>()?.Flash();
        }

        if (shakeDuration > 0f && shakeStrength > 0f)
        {
            CameraShake.Instance?.Shake(shakeDuration, shakeStrength);
        }


        DamageTextManager.Instance.ShowDamage(damage,target.transform.position + Vector3.up * 1f, type);
        return damage;
    }
    public int ApplyFixedDamage(GameObject target, float damage, DamageTextType type, bool showHitFlash = true, float shakeDuration = 0f, float shakeStrength = 0f, bool showHitEffect = true)
    {
        Health health = target.GetComponent<Health>();
        if (health == null) return 0;

        int finalDamage = Mathf.RoundToInt(damage);

        PlayerShield shield = target.GetComponent<PlayerShield>();

        if (shield != null && shield.TryAbsorbDamage(finalDamage))
        {
            Debug.Log($"[Shield] 피해 {finalDamage} 흡수");
            return 0;
        }

        health.TakeDamage(finalDamage);

        if (showHitEffect)
        {
            PlayHitEffect(target.transform.position + Vector3.up * 0.5f);
        }

        if (showHitFlash)
        {
            target.GetComponent<HitFlash>()?.Flash();
        }

        if (shakeDuration > 0f && shakeStrength > 0f)
        {
            CameraShake.Instance?.Shake(shakeDuration, shakeStrength);
        }

        DamageTextManager.Instance.ShowDamage( finalDamage,target.transform.position + Vector3.up,type);
        return finalDamage;
    }
    public int CalculateCollisionDamage(float attack, float currentSpeed)
    {
        return (int)Math.Round((attack * 0.6) + (currentSpeed * 0.8));
    }
}
