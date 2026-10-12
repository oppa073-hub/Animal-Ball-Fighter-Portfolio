using UnityEngine;
using TMPro;
using DG.Tweening;
public class DamageTextUI : MonoBehaviour
{
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private float lifeTime;
    [SerializeField] private float randomOffsetX = 0.3f;
    [SerializeField] private float moveDistance = 1f;
    private PooledObject pooled;
    private Tween returnTween;

    private void OnDisable()
    {
        transform.DOKill();
        damageText.DOKill();

        returnTween?.Kill();
        returnTween = null;
    }
    private void OnDestroy()
    {
        transform.DOKill();
        damageText.DOKill();

        returnTween?.Kill();
    }

    public void Initialize(float damage, DamageTextType type)
    {
        transform.DOKill();
        damageText.DOKill();

        damageText.text = damage.ToString();

        float randomX = Random.Range(-randomOffsetX, randomOffsetX);
        transform.position += new Vector3(randomX, 0f, 0f);

        transform.localScale = Vector3.one;

        Color color = Color.white;

        switch (type)
        {
            case DamageTextType.Normal:
                color = Color.white;
                break;
            case DamageTextType.Critical:
                color = new Color(1f, 0.85f, 0f);
                transform.localScale = Vector3.one * 1.25f;
                break;
            case DamageTextType.Burn:
                color = new Color(1f, 0.5f, 0f);
                transform.localScale = Vector3.one * 0.85f;
                break;
            case DamageTextType.Poison:
                color = Color.green;
                transform.localScale = Vector3.one * 0.85f;
                break;
            case DamageTextType.Heal:
                color = Color.cyan;
                break;
        }

        color.a = 1f;
        damageText.color = color;

        // 이동
        transform.DOMoveY(transform.position.y + moveDistance, lifeTime);

        // 페이드
        damageText.DOFade(0f, lifeTime);

        // 종료 후 풀 반환
        returnTween?.Kill();

        returnTween = DOVirtual.DelayedCall(lifeTime, () =>
        {
            if (pooled != null) pooled.ReturnToPool();
        });
    }

    private void Awake()
    {
        pooled = GetComponent<PooledObject>();
    }
}
