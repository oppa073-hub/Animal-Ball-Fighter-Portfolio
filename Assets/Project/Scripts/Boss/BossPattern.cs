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
        float multiplier = bossController != null ? bossController.CooldownMultiplier : 1f;  //분노(2페이즈)면 쿨타임 감소
        multiplier *= RemoteTuning.BossCooldownMultiplier;  //원격 밸런스 조정 (기본 1)
        if (Time.time >= lastUseTime + cooldown * multiplier) return true;

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
