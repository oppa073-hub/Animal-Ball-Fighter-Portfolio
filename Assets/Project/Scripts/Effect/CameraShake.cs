using UnityEngine;
using DG.Tweening;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Shake(float duration, float strength)
    {
        if (!ComfortSettings.ScreenShakeEnabled) return;  //설정에서 화면 흔들림 끔

        transform.DOKill();
        transform.DOShakePosition(duration, strength);
    }
}