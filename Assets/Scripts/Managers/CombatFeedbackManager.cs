using UnityEngine;

[DefaultExecutionOrder(10000)]
public class CombatFeedbackManager : MonoBehaviour
{
    public static CombatFeedbackManager Instance { get; private set; }

    [Header("Feedback Budget")]
    [SerializeField, Min(0f)] private float sfxInterval = 0.08f;
    [SerializeField, Min(0f)] private float shakeInterval = 0.12f;

    private bool hasSfxRequest;
    private SoundId pendingSoundId;
    private int pendingSoundPriority = int.MinValue;

    private bool hasShakeRequest;
    private float pendingShakeDuration;
    private float pendingShakeStrength;

    private float nextSfxTime;
    private float nextShakeTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // 크리티컬 충돌에서만 사용: 효과음 + 카메라 흔들림
    public void RequestImpact(SoundId soundId, float shakeDuration, float shakeStrength, int priority = 0)
    {
        CollectSfxRequest(soundId, priority);
        CollectShakeRequest(shakeDuration, shakeStrength);
    }

    // 일반 충돌과 벽 충돌에서 사용: 효과음만
    public void RequestSfx(SoundId soundId, int priority = 0)
    {
        CollectSfxRequest(soundId, priority);
    }

    private void CollectSfxRequest(SoundId soundId, int priority)
    {
        if (!hasSfxRequest || priority > pendingSoundPriority)
        {
            hasSfxRequest = true;
            pendingSoundId = soundId;
            pendingSoundPriority = priority;
        }
    }

    private void CollectShakeRequest(float duration, float strength)
    {
        if (duration <= 0f || strength <= 0f) return;

        hasShakeRequest = true;

        // 같은 프레임에는 가장 강한 흔들림만 남긴다.
        pendingShakeDuration = Mathf.Max(pendingShakeDuration, duration);

        pendingShakeStrength = Mathf.Max(pendingShakeStrength, strength);
    }

    private void LateUpdate()
    {
        float currentTime = Time.unscaledTime;

        if (hasSfxRequest && currentTime >= nextSfxTime)
        {
            SoundManager.Instance?.PlaySFX(pendingSoundId);
            nextSfxTime = currentTime + sfxInterval;
        }

        if (hasShakeRequest && currentTime >= nextShakeTime)
        {
            CameraShake.Instance?.Shake(pendingShakeDuration, pendingShakeStrength);

            nextShakeTime = currentTime + shakeInterval;
        }

        ClearRequests();
    }

    private void ClearRequests()
    {
        hasSfxRequest = false;
        pendingSoundPriority = int.MinValue;

        hasShakeRequest = false;
        pendingShakeDuration = 0f;
        pendingShakeStrength = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}