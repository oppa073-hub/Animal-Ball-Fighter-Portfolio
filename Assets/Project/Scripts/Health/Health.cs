using System;
using UnityEngine;
public class Health : MonoBehaviour
{
    private int maxHp;
    private int currentHp;
    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;
    public float HpRatio => maxHp > 0 ? (float)currentHp / maxHp : 0f;

    public bool IsInvulnerable { get; set; }  //연출 등으로 잠깐 피해를 받지 않음 (기본 false)

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
        if (IsInvulnerable) return;
#if UNITY_EDITOR
        if (DebugTestSettings.PlayerInvincible && CompareTag("Player")) return;  //Tools > BounceSquad Debug
#endif

        if (TutorialMode.IsActive && damage >= currentHp && CompareTag("Player")) damage = currentHp - 1;  //튜토리얼에서는 쓰러지지 않음

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
        long healAmount = (long)Mathf.Round(Mathf.Max(0f, amount));  //int 오버플로 방지

        currentHp = (int)System.Math.Min((long)currentHp + healAmount, maxHp);

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

        maxHp = (int)System.Math.Min((long)maxHp + amount, int.MaxValue);  //int 오버플로 방지

        if (healAddedAmount)
        {
            currentHp = (int)System.Math.Min((long)currentHp + amount, maxHp);
        }

        OnHealthChanged?.Invoke(currentHp, maxHp);
    }
}
