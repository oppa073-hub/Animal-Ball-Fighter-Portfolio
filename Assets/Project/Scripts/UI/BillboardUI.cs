using UnityEngine;

public class BillboardUI : MonoBehaviour
{
    private Transform mainCamera;

    private void Awake()
    {
        if (Camera.main != null)
        {
            mainCamera = Camera.main.transform;
        }
    }

    private void LateUpdate()
    {
        if (mainCamera == null) return;

        transform.rotation = mainCamera.rotation;
    }
}