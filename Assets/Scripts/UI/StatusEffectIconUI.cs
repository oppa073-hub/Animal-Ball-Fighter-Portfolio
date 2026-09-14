using UnityEngine;
using UnityEngine.UI;

public class StatusEffectIconUI : MonoBehaviour
{
    private ActiveStatusEffect effect;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image fillImage;

    public void Initialize(ActiveStatusEffect effect)
    {
        this.effect = effect;
        iconImage.sprite = effect.data.icon;
        fillImage.sprite = effect.data.icon;
        fillImage.fillAmount = 1f;
    }

    private void Update()
    {
        if (effect == null) return;

        float duration = Mathf.Max(effect.data.duration, 0.01f);

        fillImage.fillAmount = Mathf.Clamp01(effect.remainingTime / duration);
    }
}
