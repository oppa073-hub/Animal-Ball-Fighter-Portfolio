using UnityEngine;
using DG.Tweening;

public class HitFlash : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer meshRenderer;
    [SerializeField] private float holdDuration = 0.06f;
    [SerializeField] private float restoreDuration = 0.12f;

    private Material material;
    private Color originalColor;
    private Sequence flashSequence;
    private void OnDisable()
    {
        flashSequence?.Kill();
        flashSequence = null;

        if (material != null)
        {
            material.color = originalColor;
        }
    }

    private void OnDestroy()
    {
        flashSequence?.Kill();

        if (material != null)
        {
            Destroy(material);
            material = null;
        }
    }

    private void Awake()
    {
        if (meshRenderer == null)
        {
            meshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }
        if (meshRenderer != null)
        {
            material = meshRenderer.material;
            originalColor = material.color;
        }
    }

    public void Flash()
    {
        if (material == null) return;

        flashSequence?.Kill();

        material.color = originalColor;

        flashSequence = DOTween.Sequence();
        flashSequence.AppendCallback(() => material.color = Color.white);
        flashSequence.AppendInterval(holdDuration);
        flashSequence.Append(material.DOColor(originalColor, restoreDuration));
    }
}
