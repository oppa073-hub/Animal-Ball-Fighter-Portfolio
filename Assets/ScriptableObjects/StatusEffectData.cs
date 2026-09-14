using UnityEngine;

public enum StatusEffectType
{
    Burn, Poison
}

[CreateAssetMenu(fileName = "StatusEffectData", menuName = "Data/Status Effect")]
public class StatusEffectData : ScriptableObject
{
    public StatusEffectType type;
    public float duration;  //지속 시간
    public float tickInterval;  //몇 초마다 데미지
    public float damagePerTick;
    public Sprite icon;

    [Header("VFX")]
    public GameObject vfxPrefab;
    public Vector3 vfxOffset;
    public Vector3 vfxRotationOffset;
    public Vector3 vfxScale = Vector3.one;
}
