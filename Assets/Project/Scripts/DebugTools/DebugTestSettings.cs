#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 에디터 전용 테스트 설정 (Tools > BounceSquad Debug 창에서 변경)
// 값은 EditorPrefs(PC별 로컬)에 저장되므로 git과 릴리즈 빌드에 영향이 없다
public static class DebugTestSettings
{
    private const string Prefix = "BounceSquad.DebugTest.";

    public static string ForcedAugmentGuid  //강제 노출할 증강 에셋 guid (빈 값이면 끔)
    {
        get => EditorPrefs.GetString(Prefix + "ForcedAugmentGuid", "");
        set => EditorPrefs.SetString(Prefix + "ForcedAugmentGuid", value);
    }

    public static bool HpOverrideEnabled
    {
        get => EditorPrefs.GetBool(Prefix + "HpOverrideEnabled", false);
        set => EditorPrefs.SetBool(Prefix + "HpOverrideEnabled", value);
    }

    public static int HpOverrideValue
    {
        get => EditorPrefs.GetInt(Prefix + "HpOverrideValue", 120000);
        set => EditorPrefs.SetInt(Prefix + "HpOverrideValue", value);
    }

    public static bool AttackOverrideEnabled
    {
        get => EditorPrefs.GetBool(Prefix + "AttackOverrideEnabled", false);
        set => EditorPrefs.SetBool(Prefix + "AttackOverrideEnabled", value);
    }

    public static float AttackOverrideValue
    {
        get => EditorPrefs.GetFloat(Prefix + "AttackOverrideValue", 1000f);
        set => EditorPrefs.SetFloat(Prefix + "AttackOverrideValue", value);
    }

    public static int WallCountOverride  //방마다 배치할 벽 개수 (-1이면 끔)
    {
        get => EditorPrefs.GetInt(Prefix + "WallCountOverride", -1);
        set => EditorPrefs.SetInt(Prefix + "WallCountOverride", value);
    }

    public static int WallHitsOverride  //벽이 부서지는 타격 횟수 (0이면 끔)
    {
        get => EditorPrefs.GetInt(Prefix + "WallHitsOverride", 0);
        set => EditorPrefs.SetInt(Prefix + "WallHitsOverride", value);
    }

    public static int StartRoomOverride  //스테이지 시작 방 번호 (1 이하면 끔). 5의 배수 = 보스 방
    {
        get => EditorPrefs.GetInt(Prefix + "StartRoomOverride", 0);
        set => EditorPrefs.SetInt(Prefix + "StartRoomOverride", value);
    }

    public static int BossOverride  //보스 방에 나올 BossId (-1이면 끔)
    {
        get => EditorPrefs.GetInt(Prefix + "BossOverride", -1);
        set => EditorPrefs.SetInt(Prefix + "BossOverride", value);
    }

    public static bool PlayerInvincible
    {
        get => EditorPrefs.GetBool(Prefix + "PlayerInvincible", false);
        set => EditorPrefs.SetBool(Prefix + "PlayerInvincible", value);
    }

    public static void ApplyForcedAugment(List<AugmentData> choices, List<AugmentData> pool)  //후보에 있으면 선택지 첫 칸에 넣음
    {
        string guid = ForcedAugmentGuid;
        if (string.IsNullOrEmpty(guid) || choices.Count == 0) return;

        AugmentData forced = pool.Find(augment => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(augment)) == guid);
        if (forced == null || choices.Contains(forced)) return;

        choices[0] = forced;
        Debug.LogWarning($"[DebugTest] 증강 강제 노출: {forced.name}");
    }

    public static int ApplyHpOverride(int maxHp)
    {
        if (!HpOverrideEnabled) return maxHp;

        Debug.LogWarning($"[DebugTest] 체력 오버라이드: {maxHp} -> {HpOverrideValue}");
        return HpOverrideValue;
    }

    public static float ApplyAttackOverride(float attack)
    {
        if (!AttackOverrideEnabled) return attack;

        Debug.LogWarning($"[DebugTest] 공격력 오버라이드: {attack} -> {AttackOverrideValue}");
        return AttackOverrideValue;
    }
}
#endif
