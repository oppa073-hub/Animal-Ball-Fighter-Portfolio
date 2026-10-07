using UnityEngine;
using System.Collections.Generic;

public class StatusEffectHandler : MonoBehaviour
{
    [SerializeField] StatusEffectUIContainer container;
    private List<ActiveStatusEffect> activeEffects = new List<ActiveStatusEffect>();

    private void Update()
    {
        if (GameManager.Instance.currentState != GameState.Playing) return;

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveStatusEffect effect = activeEffects[i];

            effect.remainingTime -= Time.deltaTime;
            effect.tickTimer += Time.deltaTime;

            if (effect.CanTick())
            {
                DamageTextType textType = effect.data.type switch
                {
                    StatusEffectType.Burn => DamageTextType.Burn,
                    StatusEffectType.Poison => DamageTextType.Poison,
                    _ => DamageTextType.Normal
                };

                DamageManager.Instance.ApplyFixedDamage(gameObject, effect.data.damagePerTick, textType, false, false);

                if (!activeEffects.Contains(effect))
                {
                    continue;
                }

                effect.ResetTickTimer();
            }
            if (effect.remainingTime <= 0)
            {
                container.RemoveIcon(effect);
                effect.DestroyVfx();
                activeEffects.RemoveAt(i);
            }
        }
    }

    public void ApplyStatus(StatusEffectData data)
    {
        if (data == null)
        {
            return;
        }

        for (int i = 0; i < activeEffects.Count; i++)
        {
            if (activeEffects[i].data.type == data.type)
            {
                activeEffects[i].remainingTime = data.duration;
                return;
            }
        }
        ActiveStatusEffect effect = new ActiveStatusEffect(data);
        if (data.vfxPrefab != null)
        {
            GameObject vfx = Instantiate(data.vfxPrefab, transform);

            vfx.transform.localPosition = data.vfxOffset;
            vfx.transform.localRotation = Quaternion.Euler(data.vfxRotationOffset);
            vfx.transform.localScale = data.vfxScale;

            effect.spawnedVfx = vfx;
        }
        activeEffects.Add(effect);
        container.AddIcon(effect);
    }

    public void ClearAllEffects()
    {
        foreach (var effect in activeEffects)
        {
            container.RemoveIcon(effect);
            effect.DestroyVfx();
        }
        activeEffects.Clear();
    }
}
