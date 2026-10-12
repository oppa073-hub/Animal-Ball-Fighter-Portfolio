using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// 적 외형 연출: 맞으면 순간 하얗게 번쩍이며 말랑하게 찌그러지고, 죽으면 맞은 방향으로 빙글빙글 날아간다.
// 루트(물리)가 아니라 모델만 움직이고, 풀로 돌아갈 때 원래 위치/회전/크기로 되돌린다. CombatJuice가 필요할 때 붙인다.
public class EnemyJuiceState : MonoBehaviour
{
    private const float FlyDistance = 1.8f;
    private const float FlyHeight = 1f;
    private const float FlyDuration = 0.55f;

    private Transform modelRoot;
    private Vector3 modelRootScale;
    private bool initialized;

    private Transform flyingModel;
    private Vector3 flyLocalPosition;
    private Quaternion flyLocalRotation;
    private Vector3 flyLocalScale;
    private Sequence flySequence;

    private readonly List<Renderer> flashRenderers = new List<Renderer>();
    private readonly List<Material[]> flashOriginals = new List<Material[]>();
    private Coroutine flashRoutine;

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;

        modelRoot = transform.Find("ModelRoot");
        if (modelRoot == null)
        {
            Animator animator = GetComponentInChildren<Animator>();
            modelRoot = animator != null ? animator.transform : null;
        }

        if (modelRoot != null) modelRootScale = modelRoot.localScale;
    }

    public void Squash(float amount)
    {
        Initialize();
        if (modelRoot == null || flyingModel != null) return;

        modelRoot.DOKill();
        modelRoot.localScale = modelRootScale;
        modelRoot.DOPunchScale(Vector3.Scale(modelRootScale, new Vector3(amount, -amount, amount)), 0.22f, 6, 0.6f);
    }

    // 몸 전체를 잠깐 흰색 머티리얼로 바꿔 "맞았다"는 느낌을 준다 (보스처럼 HitFlash가 있으면 그쪽이 처리)
    public void Flash(Material flashMaterial, float realDuration)
    {
        Initialize();
        if (flashMaterial == null || modelRoot == null || flyingModel != null) return;
        if (GetComponent<HitFlash>() != null) return;

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        else
        {
            flashRenderers.Clear();
            flashOriginals.Clear();

            foreach (Renderer renderer in GetActiveModel().GetComponentsInChildren<Renderer>(false))
            {
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;

                Material[] originals = renderer.sharedMaterials;
                Material[] flash = new Material[originals.Length];
                for (int i = 0; i < flash.Length; i++) flash[i] = flashMaterial;

                flashRenderers.Add(renderer);
                flashOriginals.Add(originals);
                renderer.sharedMaterials = flash;
            }
        }

        flashRoutine = StartCoroutine(RestoreFlashAfter(realDuration));
    }

    private IEnumerator RestoreFlashAfter(float realDuration)
    {
        yield return new WaitForSecondsRealtime(realDuration);
        RestoreFlash();
    }

    private void RestoreFlash()
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = null;

        for (int i = 0; i < flashRenderers.Count; i++)
        {
            if (flashRenderers[i] != null) flashRenderers[i].sharedMaterials = flashOriginals[i];
        }

        flashRenderers.Clear();
        flashOriginals.Clear();
    }

    public void FlyAway(Vector3 direction)
    {
        Initialize();
        if (modelRoot == null || flyingModel != null) return;

        RestoreFlash();
        modelRoot.DOKill();
        modelRoot.localScale = modelRootScale;

        flyingModel = GetActiveModel();
        flyLocalPosition = flyingModel.localPosition;
        flyLocalRotation = flyingModel.localRotation;
        flyLocalScale = flyingModel.localScale;

        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.01f ? direction.normalized : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;

        float distance = GetDistanceToWall(direction);
        Vector3 target = flyingModel.position + direction * distance;
        Vector3 spin = new Vector3(Random.Range(-1f, 1f), Random.Range(0.5f, 1f), Random.Range(-1f, 1f)).normalized * Random.Range(360f, 540f);

        flySequence = DOTween.Sequence();
        flySequence.Append(flyingModel.DOJump(target, FlyHeight, 1, FlyDuration).SetEase(Ease.OutQuad));
        flySequence.Join(flyingModel.DOLocalRotate(spin, FlyDuration, RotateMode.FastBeyond360).SetRelative());
        flySequence.Insert(FlyDuration * 0.7f, flyingModel.DOScale(Vector3.zero, FlyDuration * 0.35f).SetEase(Ease.InBack));
    }

    private Transform GetActiveModel()  //모델이 여러 개 중 하나만 켜져 있는 구조(Enemy/EliteEnemy)면 켜진 것
    {
        foreach (Transform child in modelRoot)
        {
            if (child.gameObject.activeSelf && child.GetComponent<Animator>() != null) return child;
        }

        return modelRoot;
    }

    private float GetDistanceToWall(Vector3 direction)  //벽 너머로 날아가 보이지 않게
    {
        Vector3 origin = transform.position + Vector3.up * 0.3f;
        float distance = FlyDistance;

        foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, FlyDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.CompareTag("Wall")) distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.6f));
        }

        return distance;
    }

    private void OnDisable()  //풀 반환
    {
        RestoreFlash();
        flySequence?.Kill();
        flySequence = null;

        if (flyingModel != null)
        {
            flyingModel.DOKill();
            flyingModel.localPosition = flyLocalPosition;
            flyingModel.localRotation = flyLocalRotation;
            flyingModel.localScale = flyLocalScale;
            flyingModel = null;
        }

        if (modelRoot != null)
        {
            modelRoot.DOKill();
            modelRoot.localScale = modelRootScale;
        }
    }
}
