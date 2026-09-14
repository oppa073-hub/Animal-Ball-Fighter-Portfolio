using System.Collections.Generic;
using UnityEngine;

public class StatusEffectUIContainer : MonoBehaviour
{
    [SerializeField] private StatusEffectIconUI iconPrefab;
    [SerializeField] private Transform iconParent;
    private Dictionary<ActiveStatusEffect, StatusEffectIconUI> activeIcons = new Dictionary<ActiveStatusEffect, StatusEffectIconUI>();

    public void AddIcon(ActiveStatusEffect effect)
    {
        if (!activeIcons.ContainsKey(effect))
        {
            StatusEffectIconUI newIcon = Instantiate(iconPrefab, iconParent);
            newIcon.Initialize(effect);
            activeIcons.Add(effect, newIcon);
        }
    }

    public void RemoveIcon(ActiveStatusEffect effect)
    {
        if (activeIcons.TryGetValue(effect, out StatusEffectIconUI icon))
        {
            Destroy(icon.gameObject);
            activeIcons.Remove(effect);
        }
    }
}
