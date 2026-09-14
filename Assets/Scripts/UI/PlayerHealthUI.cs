using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthUI : MonoBehaviour
{
    private Health health;
    [SerializeField] private Image fillImg;
    public void Initialize(Health health)
    {
        this.health = health;
        fillImg.fillAmount = health.HpRatio;
    }

    private void Update()
    {
        if (health == null) return;

        fillImg.fillAmount = health.HpRatio;
    }

}
