using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerLaunchFeedback : MonoBehaviour
{
    [Header("Pull Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float maxPullDistance = 0.6f;
    [SerializeField, Range(0f, 0.3f)]
    private float squashAmount = 0.12f;
    [SerializeField] private float visualSmoothTime = 0.05f;
    [SerializeField] private float releaseKickDuration = 0.08f;

    [Header("Aim Dots")]
    [SerializeField] private GameObject aimDotPrefab;
    [SerializeField] private Transform aimDotRoot;
    [SerializeField, Min(2)] private int dotCount = 14;
    [SerializeField, Min(1)] private int minimumVisibleDots = 5;
    [SerializeField] private float minimumGuideDistance = 3f;
    [SerializeField] private float maximumGuideDistance = 9f;
    [SerializeField] private float guideHeightOffset = -0.35f;
    [SerializeField] private float firstDotScale = 0.18f;
    [SerializeField] private float lastDotScale = 0.08f;
    [SerializeField] private LayerMask wallLayer;

    private readonly List<Transform> aimDots = new();

    private Vector3 defaultLocalPosition;
    private Vector3 defaultLocalScale;
    private Vector3 targetLocalPosition;
    private Vector3 targetLocalScale;

    private Vector3 positionVelocity;
    private Vector3 scaleVelocity;
    private Coroutine releaseRoutine;

    private void Awake()
    {
        if (visualRoot != null)
        {
            defaultLocalPosition = visualRoot.localPosition;
            defaultLocalScale = visualRoot.localScale;

            targetLocalPosition = defaultLocalPosition;
            targetLocalScale = defaultLocalScale;
        }

        if (aimDotRoot == null)
        {
            aimDotRoot = transform;
        }

        if (aimDotPrefab != null)
        {
            for (int i = 0; i < dotCount; i++)
            {
                GameObject dot = Instantiate(aimDotPrefab,aimDotRoot);

                aimDots.Add(dot.transform);
                dot.SetActive(false);
            }
        }
    }

    private void Update()
    {
        if (visualRoot == null) return;

        float smoothTime = Mathf.Max(visualSmoothTime, 0.001f);

        visualRoot.localPosition = Vector3.SmoothDamp(
            visualRoot.localPosition,
            targetLocalPosition,
            ref positionVelocity,
            smoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime
        );

        visualRoot.localScale = Vector3.SmoothDamp(
            visualRoot.localScale,
            targetLocalScale,
            ref scaleVelocity,
            smoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime
        );
    }

    public void ShowPull(Vector3 origin, Vector3 shootDirection, float pullRatio)
    {
        if (shootDirection.sqrMagnitude < 0.001f) return;

        if (releaseRoutine != null)
        {
            StopCoroutine(releaseRoutine);
            releaseRoutine = null;
        }

        pullRatio = Mathf.Clamp01(pullRatio);

        shootDirection.y = 0f;
        shootDirection.Normalize();

        float easedRatio = Mathf.SmoothStep(0f, 1f, pullRatio);

        if (visualRoot != null)
        {
            Vector3 localDirection = transform.InverseTransformDirection(shootDirection);

            localDirection.y = 0f;
            localDirection.Normalize();

            // 발사 방향 반대쪽으로 외형만 당김
            targetLocalPosition = defaultLocalPosition - localDirection * maxPullDistance * easedRatio;

            float squash = squashAmount * easedRatio;

            targetLocalScale = new Vector3(
                defaultLocalScale.x * (1f + squash),
                defaultLocalScale.y * (1f - squash),
                defaultLocalScale.z * (1f + squash)
            );
        }

        UpdateAimDots(origin, shootDirection, pullRatio);
    }

    public void Release()
    {
        HideAimDots();

        targetLocalPosition = defaultLocalPosition;

        if (visualRoot == null) return;

        targetLocalScale = new Vector3(
            defaultLocalScale.x * 0.88f,
            defaultLocalScale.y * 1.16f,
            defaultLocalScale.z * 0.88f
        );

        if (releaseRoutine != null)
        {
            StopCoroutine(releaseRoutine);
        }

        releaseRoutine = StartCoroutine(ReturnFromReleaseKick());
    }

    public void Cancel()
    {
        if (releaseRoutine != null)
        {
            StopCoroutine(releaseRoutine);
            releaseRoutine = null;
        }

        HideAimDots();

        targetLocalPosition = defaultLocalPosition;
        targetLocalScale = defaultLocalScale;
    }

    public void ResetImmediate()
    {
        Cancel();

        if (visualRoot == null) return;

        visualRoot.localPosition = defaultLocalPosition;
        visualRoot.localScale = defaultLocalScale;

        positionVelocity = Vector3.zero;
        scaleVelocity = Vector3.zero;
    }

    private void UpdateAimDots(Vector3 origin, Vector3 direction, float pullRatio)
    {
        if (aimDots.Count == 0) return;

        int visibleCount = Mathf.Clamp(
            Mathf.RoundToInt(
                Mathf.Lerp(minimumVisibleDots, dotCount, pullRatio)
            ),
            1,
            aimDots.Count
        );

        float guideDistance = Mathf.Lerp(
            minimumGuideDistance,
            maximumGuideDistance,
            pullRatio
        );

        Vector3 startPosition = origin + Vector3.up * guideHeightOffset;

        bool hasWallHit = Physics.Raycast(
            startPosition,
            direction,
            out RaycastHit hit,
            guideDistance,
            wallLayer,
            QueryTriggerInteraction.Ignore
        );

        Vector3 reflectedDirection = direction;

        if (hasWallHit)
        {
            reflectedDirection = Vector3.Reflect(direction, hit.normal);

            reflectedDirection.y = 0f;
            reflectedDirection.Normalize();
        }

        float spacing = guideDistance / (visibleCount + 1);

        for (int i = 0; i < aimDots.Count; i++)
        {
            GameObject dotObject = aimDots[i].gameObject;

            if (i >= visibleCount)
            {
                dotObject.SetActive(false);
                continue;
            }

            float distance = spacing * (i + 1);
            Vector3 position;

            if (hasWallHit && distance > hit.distance)
            {
                float reflectedDistance = distance - hit.distance;

                position = hit.point + reflectedDirection * (reflectedDistance + 0.03f);
            }
            else
            {
                position = startPosition + direction * distance;
            }

            float dotRatio = visibleCount <= 1
                ? 0f
                : (float)i / (visibleCount - 1);

            float scale = Mathf.Lerp(firstDotScale, lastDotScale, dotRatio);

            scale *= Mathf.Lerp(0.75f, 1.15f, pullRatio);

            aimDots[i].position = position;
            aimDots[i].localScale = Vector3.one * scale;

            dotObject.SetActive(true);
        }
    }

    private void HideAimDots()
    {
        for (int i = 0; i < aimDots.Count; i++)
        {
            aimDots[i].gameObject.SetActive(false);
        }
    }

    private IEnumerator ReturnFromReleaseKick()
    {
        yield return new WaitForSecondsRealtime(releaseKickDuration);

        targetLocalScale = defaultLocalScale;
        releaseRoutine = null;
    }
}