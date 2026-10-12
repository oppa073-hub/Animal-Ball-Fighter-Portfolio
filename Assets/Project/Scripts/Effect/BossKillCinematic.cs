using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

// 보스 처치 연출: 잠깐 슬로모션 + 카메라가 처치 지점 쪽으로 다가가며 줌인했다가 원래대로.
// 실제 시간 기준으로 진행하고, 끝나거나 중간에 파괴돼도 timeScale/fixedDeltaTime/카메라를 반드시 복구한다.
public class BossKillCinematic : MonoBehaviour
{
    private struct Settings
    {
        public float slowScale, zoomInTime, holdTime, zoomOutTime, moveRatio, fovRatio;  //moveRatio: 카메라가 처치 지점 쪽으로 다가가는 비율
    }

    private static readonly Settings BossSettings = new Settings { slowScale = 0.2f, zoomInTime = 0.25f, holdTime = 0.55f, zoomOutTime = 0.3f, moveRatio = 0.3f, fovRatio = 0.8f };
    private static readonly Settings LastKillSettings = new Settings { slowScale = 0.3f, zoomInTime = 0.12f, holdTime = 0.28f, zoomOutTime = 0.15f, moveRatio = 0.12f, fovRatio = 0.92f };  //일반/엘리트 방 마지막 적: 짧고 약하게

    private Settings settings;

    private Camera targetCamera;
    private Vector3 originalPosition;
    private float originalFov;
    private float originalFixedDeltaTime;
    private bool isRestored = true;

    public static void Play(Vector3 focusPoint, Action onFinished, bool lastKillOnly = false)
    {
        BossKillCinematic cinematic = new GameObject("BossKillCinematic").AddComponent<BossKillCinematic>();
        cinematic.settings = lastKillOnly ? LastKillSettings : BossSettings;
        cinematic.StartCoroutine(cinematic.Run(focusPoint, onFinished));
    }

    private IEnumerator Run(Vector3 focusPoint, Action onFinished)
    {
        TimeSlowController.End();  //산데비스탄 감속 중이었다면 먼저 정리

        targetCamera = Camera.main;
        originalFixedDeltaTime = Time.fixedDeltaTime;
        isRestored = false;

        Time.timeScale = settings.slowScale;
        Time.fixedDeltaTime = originalFixedDeltaTime * settings.slowScale;

        Vector3 zoomPosition = Vector3.zero;

        if (targetCamera != null)
        {
            targetCamera.transform.DOComplete();  //진행 중인 화면 흔들림을 끝내 원위치로 (흔들린 위치를 기준으로 잡지 않도록)

            originalPosition = targetCamera.transform.position;
            originalFov = targetCamera.fieldOfView;
            zoomPosition = Vector3.Lerp(originalPosition, focusPoint, settings.moveRatio);
            zoomPosition.y = Mathf.Lerp(originalPosition.y, focusPoint.y, settings.moveRatio * 0.5f);
        }

        yield return Animate(0f, 1f, settings.zoomInTime, zoomPosition);
        yield return Animate(1f, 1f, settings.holdTime, zoomPosition);  //유지 구간도 매 프레임 적용 (처치 타격의 화면 흔들림이 줌 위치를 끌어당기지 않게)
        yield return Animate(1f, 0f, settings.zoomOutTime, zoomPosition);

        Restore();
        Destroy(gameObject);

        onFinished?.Invoke();
    }

    private IEnumerator Animate(float from, float to, float duration, Vector3 zoomPosition)
    {
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(from, to, Mathf.Clamp01(timer / duration));
            Apply(t, zoomPosition);
            yield return null;
        }

        Apply(to, zoomPosition);
    }

    private void Apply(float t, Vector3 zoomPosition)
    {
        if (targetCamera == null) return;

        targetCamera.transform.position = Vector3.Lerp(originalPosition, zoomPosition, t);
        targetCamera.fieldOfView = Mathf.Lerp(originalFov, originalFov * settings.fovRatio, t);
    }

    private void Restore()
    {
        if (isRestored) return;
        isRestored = true;

        if (Mathf.Approximately(Time.timeScale, settings.slowScale)) Time.timeScale = 1f;
        Time.fixedDeltaTime = originalFixedDeltaTime;

        if (targetCamera != null)
        {
            targetCamera.transform.DOKill();  //연출 중 시작된 흔들림이 줌 위치로 되돌아가지 않도록
            targetCamera.transform.position = originalPosition;
            targetCamera.fieldOfView = originalFov;
        }
    }

    private void OnDestroy()
    {
        Restore();
    }
}
