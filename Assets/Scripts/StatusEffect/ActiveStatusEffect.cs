using UnityEngine;

public class ActiveStatusEffect
{
    public StatusEffectData data;
    public float remainingTime;
    public float tickTimer;
    public GameObject spawnedVfx;

    public ActiveStatusEffect(StatusEffectData data)
    {
        this.data = data;
        remainingTime = data.duration;
        tickTimer = 0f;
    }

    public bool CanTick()
    {
        return tickTimer >= data.tickInterval;
    }

    public void ResetTickTimer()
    {
        tickTimer = 0f;
    }
    public void DestroyVfx()
    {
        if (spawnedVfx != null)
        {
            GameObject.Destroy(spawnedVfx);
        }
    }
}
