using System;
using UnityEngine;

public class PlayerShield : MonoBehaviour
{
    private int maxShield;
    private int currentShield;

    public int MaxShield => maxShield;
    public int CurrentShield => currentShield;
    public bool HasShield => currentShield > 0;
    public event Action<float, float> OnShieldChanged; 
    public event Action OnShieldBroken;

    public void AddMaxShield(int amount)
    {
        maxShield += amount;
        currentShield += amount;

        OnShieldChanged?.Invoke(currentShield, maxShield);
        Debug.Log($"[Shield] 획득 +{amount}  ({currentShield}/{maxShield})");
    }
    public bool TryAbsorbDamage(int damage)
    {
        if (!HasShield) return false;

        int previousShield = currentShield;

        currentShield = Mathf.Max(0, currentShield - damage);

        OnShieldChanged?.Invoke(currentShield, maxShield);

        if (previousShield > 0 && currentShield <= 0)
        {
            OnShieldBroken?.Invoke();
        }

        return true;
    }
}
