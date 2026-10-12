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
    [SerializeField, Range(0, 3)] private int maxGuideBounces = 2;
    [SerializeField] private float guideCastRadius = 0.36f;  //공 콜라이더 반지름(0.4)보다 살짝 작게

    [Header("Tail + Chevron Guide (머티리얼이 없으면 Aim Dots 사용)")]
    [SerializeField] private Material guideMaterial;
    [SerializeField] private float tailMinLength = 0.5f;
    [SerializeField] private float tailMaxLength = 2.2f;
    [SerializeField] private float tailWidth = 0.7f;
    [SerializeField] private float chevronSpacing = 0.75f;
    [SerializeField] private float chevronSize = 0.3f;
    [SerializeField] private float chevronLineWidth = 0.11f;
    [SerializeField] private float chevronFlowSpeed = 1.6f;
    [SerializeField] private float chevronStartGap = 0.6f;  //공 밑에 가리지 않도록 띄우는 거리

    private readonly List<Transform> aimDots = new();
    private readonly List<Vector3> guidePoints = new();
    private readonly RaycastHit[] guideHits = new RaycastHit[16];
    private int guideCastMask;

    private const int TailPointCount = 8;
    private static readonly Quaternion FlatRotation = Quaternion.Euler(90f, 0f, 0f);  //LineAlignment.TransformZ로 바닥에 눕힘
    private LineRenderer tailLine;
    private readonly List<LineRenderer> chevrons = new();
    private readonly Vector3[] tailPoints = new Vector3[TailPointCount];
    private readonly Vector3[] chevronPoints = new Vector3[3];

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

        if (guideMaterial != null)
        {
            CreateGuideLines();
        }
        else if (aimDotPrefab != null)
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

        if (guideMaterial != null)
        {
            UpdateTail(origin, shootDirection, easedRatio);
            UpdateChevrons(origin, shootDirection, pullRatio);
        }
        else
        {
            UpdateAimDots(origin, shootDirection, pullRatio);
        }
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

    // 부딪힌 순간 납작하게 찌그러뜨림. 이후 Update의 SmoothDamp가 원래 크기로 되돌린다.
    public void Impact(float amount)
    {
        if (visualRoot == null) return;

        visualRoot.localScale = new Vector3(
            defaultLocalScale.x * (1f + amount),
            defaultLocalScale.y * (1f - amount),
            defaultLocalScale.z * (1f + amount)
        );

        scaleVelocity = Vector3.zero;
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

        BuildGuidePath(origin, direction, guideDistance);

        float spacing = guideDistance / (visibleCount + 1);
        Vector3 heightOffset = Vector3.up * guideHeightOffset;

        for (int i = 0; i < aimDots.Count; i++)
        {
            GameObject dotObject = aimDots[i].gameObject;

            if (i >= visibleCount)
            {
                dotObject.SetActive(false);
                continue;
            }

            float distance = spacing * (i + 1);
            Vector3 position = EvaluateGuidePath(distance, out int segmentIndex) + heightOffset;

            float dotRatio = visibleCount <= 1
                ? 0f
                : (float)i / (visibleCount - 1);

            float scale = Mathf.Lerp(firstDotScale, lastDotScale, dotRatio);

            scale *= Mathf.Lerp(0.75f, 1.15f, pullRatio);

            if (segmentIndex >= 3) scale *= 0.75f;  //두 번째 반사 이후는 실제 랜덤 반사각 때문에 대략적이라 작게

            aimDots[i].position = position;
            aimDots[i].localScale = Vector3.one * scale;

            dotObject.SetActive(true);
        }
    }

    // 공 반지름 굵기로 벽(Wall 레이어 또는 Wall 태그: 장애물 벽 + 외곽 벽)에 반사되는 경로를 만든다. 바닥 등 다른 콜라이더는 무시.
    private void BuildGuidePath(Vector3 origin, Vector3 direction, float guideDistance)
    {
        guidePoints.Clear();
        guidePoints.Add(origin);

        Vector3 position = origin;
        Vector3 currentDirection = direction;
        float remaining = guideDistance;
        if (guideCastMask == 0) guideCastMask = wallLayer | LayerMask.GetMask("Default", "Wall");
        int castMask = guideCastMask;

        for (int bounce = 0; bounce <= maxGuideBounces && remaining > 0.01f; bounce++)
        {
            if (bounce == maxGuideBounces || !TryFindWall(position, currentDirection, remaining, castMask, out RaycastHit hit))
            {
                position += currentDirection * remaining;
                guidePoints.Add(position);
                break;
            }

            Vector3 contactCenter = position + currentDirection * hit.distance;  //벽에 닿는 순간의 공 중심
            guidePoints.Add(contactCenter);

            remaining -= hit.distance;

            Vector3 normal = hit.normal;
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.0001f) break;

            currentDirection = Vector3.Reflect(currentDirection, normal.normalized);
            currentDirection.y = 0f;
            currentDirection.Normalize();

            position = contactCenter + currentDirection * 0.02f;  //같은 벽에 다시 걸리지 않게 살짝 띄움
        }
    }

    private bool TryFindWall(Vector3 position, Vector3 direction, float distance, int castMask, out RaycastHit nearest)
    {
        nearest = default;
        float nearestDistance = float.MaxValue;

        int hitCount = Physics.SphereCastNonAlloc(position, guideCastRadius, direction, guideHits, distance, castMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = guideHits[i];
            if (hit.distance <= 0f) continue;  //시작부터 겹친 콜라이더(바닥 등)는 제외

            bool isWall = ((1 << hit.collider.gameObject.layer) & wallLayer) != 0 || hit.collider.CompareTag("Wall");
            if (!isWall || hit.distance >= nearestDistance) continue;

            nearestDistance = hit.distance;
            nearest = hit;
        }

        return nearestDistance < float.MaxValue;
    }

    private Vector3 EvaluateGuidePath(float distance, out int segmentIndex)
    {
        for (int i = 1; i < guidePoints.Count; i++)
        {
            float segmentLength = Vector3.Distance(guidePoints[i - 1], guidePoints[i]);

            if (distance <= segmentLength || i == guidePoints.Count - 1)
            {
                segmentIndex = i;
                return Vector3.MoveTowards(guidePoints[i - 1], guidePoints[i], distance);
            }

            distance -= segmentLength;
        }

        segmentIndex = 1;
        return guidePoints[guidePoints.Count - 1];
    }

    private void HideAimDots()
    {
        for (int i = 0; i < aimDots.Count; i++)
        {
            aimDots[i].gameObject.SetActive(false);
        }

        if (tailLine != null) tailLine.gameObject.SetActive(false);

        for (int i = 0; i < chevrons.Count; i++)
        {
            chevrons[i].gameObject.SetActive(false);
        }
    }

    // 꼬리(당김) + 쉐브론(진행 방향): 바닥에 눕힌 흰 라인. 외형 흔들림 영향이 없도록 플레이어 루트 밑에 두고 월드 좌표로 그린다.
    private void CreateGuideLines()
    {
        tailLine = CreateGuideLine("AimTail", TailPointCount);
        tailLine.numCapVertices = 6;
        tailLine.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.25f, 0.8f), new Keyframe(1f, 0.08f));
        tailLine.colorGradient = CreateGradient(0.85f, 0f);

        int chevronCount = Mathf.CeilToInt(maximumGuideDistance / Mathf.Max(chevronSpacing, 0.1f)) + 1;
        for (int i = 0; i < chevronCount; i++)
        {
            LineRenderer chevron = CreateGuideLine("AimChevron", 3);
            chevron.numCapVertices = 3;
            chevron.numCornerVertices = 3;
            chevrons.Add(chevron);
        }
    }

    private LineRenderer CreateGuideLine(string objectName, int pointCount)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        lineObject.transform.rotation = FlatRotation;

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = guideMaterial;
        line.useWorldSpace = true;
        line.alignment = LineAlignment.TransformZ;
        line.positionCount = pointCount;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        lineObject.SetActive(false);
        return line;
    }

    private static Gradient CreateGradient(float startAlpha, float endAlpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(endAlpha, 1f) });
        return gradient;
    }

    private void UpdateTail(Vector3 origin, Vector3 shootDirection, float easedRatio)
    {
        if (tailLine == null) return;

        // 발사 반대쪽(손가락 쪽)으로 당긴 만큼 늘어나는 꼬리
        float length = Mathf.Lerp(tailMinLength, tailMaxLength, easedRatio);
        Vector3 start = origin + Vector3.up * guideHeightOffset;

        for (int i = 0; i < TailPointCount; i++)
        {
            tailPoints[i] = start - shootDirection * (length * i / (TailPointCount - 1));
        }

        tailLine.SetPositions(tailPoints);
        tailLine.widthMultiplier = tailWidth * Mathf.Lerp(0.8f, 1f, easedRatio);
        tailLine.transform.rotation = FlatRotation;  //공이 굴러 회전해도 바닥에 눕힌 상태 유지
        tailLine.gameObject.SetActive(true);
    }

    private void UpdateChevrons(Vector3 origin, Vector3 direction, float pullRatio)
    {
        if (chevrons.Count == 0) return;

        float guideDistance = Mathf.Lerp(minimumGuideDistance, maximumGuideDistance, pullRatio);
        BuildGuidePath(origin, direction, guideDistance);

        float spacing = Mathf.Max(chevronSpacing, 0.1f);
        float flowOffset = Mathf.Repeat(Time.unscaledTime * chevronFlowSpeed, spacing);  //앞으로 흐르는 움직임
        float sizeScale = Mathf.Lerp(0.85f, 1.15f, pullRatio);
        Vector3 heightOffset = Vector3.up * guideHeightOffset;

        for (int i = 0; i < chevrons.Count; i++)
        {
            LineRenderer chevron = chevrons[i];
            float distance = chevronStartGap + flowOffset + spacing * i;

            if (distance > guideDistance)
            {
                chevron.gameObject.SetActive(false);
                continue;
            }

            Vector3 center = EvaluateGuidePath(distance, out int segmentIndex) + heightOffset;
            Vector3 forward = guidePoints[segmentIndex] - guidePoints[segmentIndex - 1];
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = direction;
            forward.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, forward);

            float size = chevronSize * sizeScale;
            if (segmentIndex >= 3) size *= 0.75f;  //두 번째 반사 이후는 실제 랜덤 반사각 때문에 대략적이라 작게

            chevronPoints[0] = center - forward * size * 0.5f - side * size;
            chevronPoints[1] = center + forward * size * 0.5f;
            chevronPoints[2] = center - forward * size * 0.5f + side * size;
            chevron.SetPositions(chevronPoints);
            chevron.widthMultiplier = chevronLineWidth * sizeScale;

            // 공 쪽에서 서서히 나타나고 경로 끝으로 갈수록 흐려짐
            float fadeIn = Mathf.Clamp01((distance - chevronStartGap) / (spacing * 0.6f));
            float fadeOut = Mathf.Clamp01((guideDistance - distance) / (guideDistance * 0.35f));
            float alpha = 0.95f * fadeIn * fadeOut * (segmentIndex >= 3 ? 0.6f : 1f);
            chevron.startColor = chevron.endColor = new Color(1f, 1f, 1f, alpha);
            chevron.transform.rotation = FlatRotation;

            chevron.gameObject.SetActive(true);
        }
    }

    private IEnumerator ReturnFromReleaseKick()
    {
        yield return new WaitForSecondsRealtime(releaseKickDuration);

        targetLocalScale = defaultLocalScale;
        releaseRoutine = null;
    }
}