using UnityEngine;

public abstract class BossPattern : MonoBehaviour
{
    public float cooldown = 5f;
    public float lastUseTime = 0f;
    protected BossController bossController;
    public int requiredPhase;
    protected virtual void Awake()
    {
        bossController = GetComponent<BossController>();
    }

    public abstract void UsePattern();
    public virtual bool CanUse()
    {
        if (Time.time >= lastUseTime + cooldown) return true;

        return false;
    }
    public virtual void RecordUseTime()
    {
        lastUseTime = Time.time;
    }
    public virtual void CancelPattern()
    {

    }
}
