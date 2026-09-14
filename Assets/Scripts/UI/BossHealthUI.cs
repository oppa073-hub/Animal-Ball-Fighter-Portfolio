using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class BossHealthUI : MonoBehaviour
{
    private Health health;
    [SerializeField] private Image fillImg;
    [SerializeField] private TextMeshProUGUI bossName;
   public void Initialize(Health health, EnemyController enemy)
   {
        this.health = health;
        bossName.text = enemy.EnemyName;
        fillImg.fillAmount = 1f;
   }

    private void Update()
    {
        if (health == null) return;

        fillImg.fillAmount = health.HpRatio;

        if (health.CurrentHp <= 0)
        {
            health = null;
        }
    }
}
