using System;
using UnityEngine;
public class Health : MonoBehaviour
{
    private int maxHp;
    private int currentHp;
    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;
    public float HpRatio => maxHp > 0 ? (float)currentHp / maxHp : 0f;

    public event Action OnDeath;
    public event Action<float, float> OnHealthChanged;

    public void Initialize(int maxHp)
    {
        this.maxHp = maxHp;
        currentHp = maxHp;
    }

    public void TakeDamage(int damage)
    {
        if (currentHp <= 0) return;

        currentHp -= damage;

        if (currentHp <= 0)
        {
            currentHp = 0;
            OnDeath?.Invoke();
        }

        OnHealthChanged?.Invoke(currentHp, maxHp);
    }
    public void Heal(float amount)
    {
        int healAmount = Mathf.RoundToInt(amount);

        currentHp = Mathf.Min(currentHp + healAmount, maxHp);

        OnHealthChanged?.Invoke(currentHp, maxHp);
    }
    public bool Revive(float hpRatio)
    {
        if (currentHp > 0 || maxHp <= 0) return false;

        currentHp = Mathf.Clamp(
            Mathf.CeilToInt(maxHp * hpRatio),
            1,
            maxHp
        );

        OnHealthChanged?.Invoke(currentHp, maxHp);

        Debug.Log($"[Player] 부활 HP: {currentHp}/{maxHp}");

        return true;
    }
    public void IncreaseMaxHp(int amount, bool healAddedAmount = true)
    {
        if (amount <= 0) return;

        maxHp += amount;

        if (healAddedAmount)
        {
            currentHp = Mathf.Min(currentHp + amount, maxHp);
        }

        OnHealthChanged?.Invoke(currentHp, maxHp);
    }
}
