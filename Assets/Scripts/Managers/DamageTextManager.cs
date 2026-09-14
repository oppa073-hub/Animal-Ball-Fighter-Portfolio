using UnityEngine;
public enum DamageTextType
{
    Normal,
    Critical,
    Burn,
    Poison,
    Heal
}
public class DamageTextManager : MonoBehaviour
{
    [SerializeField] private DamageTextUI damageTextPrefab;
    public static DamageTextManager Instance { get; private set; }

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
    public void ShowDamage(float damage, Vector3 position, DamageTextType type)
    {
        if (ObjectPoolManager.Instance == null) return;
        if (damageTextPrefab == null) return;

        GameObject damageTextObject =ObjectPoolManager.Instance.GetObject(damageTextPrefab.gameObject, position, Quaternion.identity);

        if (damageTextObject == null)
        {
            Debug.LogWarning("[DamageText] 데미지 텍스트 풀 부족");

            return;
        }

        DamageTextUI damageTextUI = damageTextObject.GetComponent<DamageTextUI>();

        if (damageTextUI == null)
        {
            Debug.LogError("[DamageText] DamageTextUI 컴포넌트 없음");

            damageTextObject.GetComponent<PooledObject>() ?.ReturnToPool();

            return;
        }

        damageTextUI.Initialize(damage, type);
    }
}
