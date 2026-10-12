using UnityEngine;

public class StatusEffectReceiver : MonoBehaviour
{
    private StatusEffectHandler handler;

    private void Awake()
    {
        handler = GetComponent<StatusEffectHandler>();
    }

    public void ApplyStatus(StatusEffectData data)
    {
        handler.ApplyStatus(data);
    }

}
