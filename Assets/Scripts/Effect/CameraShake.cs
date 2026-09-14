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
        Debug.Log($"Shake : {strength}");

        transform.DOKill();
        transform.DOShakePosition(duration, strength);
    }
}