using DG.Tweening;
using UnityEngine;
using System.Collections;

public class BossWarningIndicator : MonoBehaviour
{
    [SerializeField] private Transform visual;
    [SerializeField] private float startScale = 0.15f;
    [SerializeField] private Renderer warningRenderer;

    [Header("Blink")]
    [SerializeField] private float blinkDuration = 0.3f;
    [SerializeField] private float blinkInterval = 0.08f;

    private Coroutine blinkCoroutine;
    private Vector3 originalScale;

    private void Awake()
    {
        if (visual == null) visual = transform;

        originalScale = visual.localScale;
    }
    private void OnDisable()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }

        if (visual != null)
        {
            visual.DOKill();
            visual.localScale = originalScale;
        }

        if (warningRenderer != null)
        {
            warningRenderer.enabled = true;
        }
    }

    public void Play(float duration)
    {
        visual.DOKill();

        if (warningRenderer != null)
        {
            warningRenderer.enabled = true;
        }

        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }

        visual.localScale = originalScale * startScale;

        visual.DOScale(originalScale, duration)
        .SetEase(Ease.Linear); 
        
        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);

        blinkCoroutine = StartCoroutine(BlinkBeforeAttack(duration));
    }

    private IEnumerator BlinkBeforeAttack(float totalDuration)
    {
        if (warningRenderer == null) yield break;

        float waitTime = Mathf.Max(0f, totalDuration - blinkDuration);

        yield return new WaitForSeconds(waitTime);

        while (true)
        {
            warningRenderer.enabled = false;

            yield return new WaitForSeconds(blinkInterval);

            warningRenderer.enabled = true;

            yield return new WaitForSeconds(blinkInterval);
        }
    }
}
