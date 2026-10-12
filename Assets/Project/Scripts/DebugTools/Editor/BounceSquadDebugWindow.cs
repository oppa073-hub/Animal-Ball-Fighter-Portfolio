using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 랜덤 때문에 테스트하기 어려운 기능용 에디터 창. 설정은 DebugTestSettings(EditorPrefs)에 저장
public class BounceSquadDebugWindow : EditorWindow
{
    private readonly List<AugmentData> augments = new List<AugmentData>();
    private string[] augmentLabels = new string[0];

    [MenuItem("Tools/BounceSquad Debug")]
    private static void Open()
    {
        GetWindow<BounceSquadDebugWindow>("BounceSquad Debug");
    }

    private void OnEnable()
    {
        RefreshAugments();
    }

    private void OnFocus()
    {
        RefreshAugments();  //새 증강 에셋 추가 시 자동 반영
    }

    private void RefreshAugments()
    {
        augments.Clear();

        foreach (string guid in AssetDatabase.FindAssets("t:AugmentData"))
        {
            AugmentData augment = AssetDatabase.LoadAssetAtPath<AugmentData>(AssetDatabase.GUIDToAssetPath(guid));
            if (augment != null) augments.Add(augment);
        }

        augments.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        augmentLabels = new string[augments.Count + 1];
        augmentLabels[0] = "(끔)";

        for (int i = 0; i < augments.Count; i++)
        {
            string skillMark = augments[i].augmentType == AugmentType.AugmentSkill ? " [스킬]" : "";
            augmentLabels[i + 1] = augments[i].name + skillMark;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("에디터 전용 테스트 설정입니다. 릴리즈 빌드와 git에는 영향이 없습니다.", MessageType.Info);

        DrawAugmentSection();
        EditorGUILayout.Space();
        DrawPlayerSection();
        EditorGUILayout.Space();
        DrawObstacleSection();
        EditorGUILayout.Space();
        DrawRoomSection();
        EditorGUILayout.Space();
        DrawGoldSection();
    }

    private static readonly int[] GoldAmounts = { 1000, 10000, 100000 };

    private void DrawGoldSection()
    {
        EditorGUILayout.LabelField("골드", EditorStyles.boldLabel);

        bool canGive = Application.isPlaying && SaveManager.Instance != null;
        EditorGUILayout.LabelField("보유 골드", canGive ? SaveManager.Instance.Data.gold.ToString("N0") : "(Play 중에만 표시)");

        using (new EditorGUI.DisabledScope(!canGive))
        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (int amount in GoldAmounts)
            {
                if (GUILayout.Button($"+{amount:N0}"))
                {
                    SaveManager.Instance.AddGold(amount);  //에디터 세이브에 저장됨 (F9로 초기화)
                    Debug.LogWarning($"[DebugTest] 골드 지급: +{amount}");
                }
            }
        }
    }

    private void DrawRoomSection()
    {
        EditorGUILayout.LabelField("방 / 보스", EditorStyles.boldLabel);

        int startRoom = EditorGUILayout.IntSlider("시작 방 번호 (1=끔)", Mathf.Max(1, DebugTestSettings.StartRoomOverride), 1, 30);
        if (startRoom != Mathf.Max(1, DebugTestSettings.StartRoomOverride)) DebugTestSettings.StartRoomOverride = startRoom;
        if (startRoom > 1) EditorGUILayout.HelpBox("켜져 있으면 에디터 세이브에 도달 방/클리어/해금이 기록됩니다. 테스트 후 1로 돌리고 필요하면 F9로 세이브 초기화.", MessageType.Warning);

        string[] bossNames = System.Enum.GetNames(typeof(BossId));
        string[] bossLabels = new string[bossNames.Length + 1];
        bossLabels[0] = "(끔)";
        for (int i = 0; i < bossNames.Length; i++) bossLabels[i + 1] = bossNames[i];

        int bossIndex = Mathf.Clamp(DebugTestSettings.BossOverride + 1, 0, bossLabels.Length - 1);
        int newBossIndex = EditorGUILayout.Popup("보스 강제 지정", bossIndex, bossLabels);
        if (newBossIndex != bossIndex) DebugTestSettings.BossOverride = newBossIndex - 1;

        EditorGUILayout.LabelField(" ", "5의 배수 방 = 보스 방. 다음 스테이지 시작부터 적용", EditorStyles.miniLabel);
    }

    private static readonly string[] WallCountLabels = { "(끔)", "0", "1", "2", "3", "4", "5", "6" };

    private void DrawObstacleSection()
    {
        EditorGUILayout.LabelField("장애물 벽", EditorStyles.boldLabel);

        int countIndex = DebugTestSettings.WallCountOverride + 1;
        int newCountIndex = EditorGUILayout.Popup("벽 개수 강제", countIndex, WallCountLabels);
        if (newCountIndex != countIndex) DebugTestSettings.WallCountOverride = newCountIndex - 1;

        int hits = EditorGUILayout.IntSlider("파괴 타격 횟수 (0=끔)", DebugTestSettings.WallHitsOverride, 0, 10);
        if (hits != DebugTestSettings.WallHitsOverride) DebugTestSettings.WallHitsOverride = hits;

        EditorGUILayout.LabelField(" ", "벽 개수는 다음 방부터 적용", EditorStyles.miniLabel);
    }

    private void DrawAugmentSection()
    {
        EditorGUILayout.LabelField("증강", EditorStyles.boldLabel);

        int selectedIndex = GetSelectedAugmentIndex();
        int newIndex = EditorGUILayout.Popup("강제 노출 증강", selectedIndex, augmentLabels);

        if (newIndex != selectedIndex)
        {
            DebugTestSettings.ForcedAugmentGuid = newIndex == 0 ? "" : GetGuid(augments[newIndex - 1]);
        }

        if (newIndex > 0 && augments[newIndex - 1].augmentType == AugmentType.AugmentSkill)
        {
            EditorGUILayout.HelpBox("스킬 증강은 엘리트 방(3의 배수)에서 스킬이 없을 때, 또는 같은 스킬을 가진 상태의 일반 증강 선택에서 나옵니다.", MessageType.None);
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying || newIndex == 0 || AugmentManager.Instance == null))
        {
            if (GUILayout.Button("지금 바로 적용 (Play 중)"))
            {
                AugmentData augment = augments[newIndex - 1];
                AugmentManager.Instance.ApplyAugment(augment);
                Debug.LogWarning($"[DebugTest] 증강 즉시 적용: {augment.name}");
            }
        }
    }

    private void DrawPlayerSection()
    {
        EditorGUILayout.LabelField("플레이어", EditorStyles.boldLabel);

        bool hpOverride = EditorGUILayout.Toggle("체력 오버라이드", DebugTestSettings.HpOverrideEnabled);
        if (hpOverride != DebugTestSettings.HpOverrideEnabled) DebugTestSettings.HpOverrideEnabled = hpOverride;

        using (new EditorGUI.DisabledScope(!hpOverride))
        {
            int hpValue = EditorGUILayout.IntField("최대 체력", DebugTestSettings.HpOverrideValue);
            hpValue = Mathf.Max(1, hpValue);
            if (hpValue != DebugTestSettings.HpOverrideValue) DebugTestSettings.HpOverrideValue = hpValue;
        }

        bool attackOverride = EditorGUILayout.Toggle("공격력 오버라이드", DebugTestSettings.AttackOverrideEnabled);
        if (attackOverride != DebugTestSettings.AttackOverrideEnabled) DebugTestSettings.AttackOverrideEnabled = attackOverride;

        using (new EditorGUI.DisabledScope(!attackOverride))
        {
            float attackValue = EditorGUILayout.FloatField("공격력", DebugTestSettings.AttackOverrideValue);
            attackValue = Mathf.Max(0f, attackValue);
            if (!Mathf.Approximately(attackValue, DebugTestSettings.AttackOverrideValue)) DebugTestSettings.AttackOverrideValue = attackValue;
        }

        EditorGUILayout.LabelField(" ", "체력/공격력은 다음 스테이지 시작부터 적용", EditorStyles.miniLabel);

        bool invincible = EditorGUILayout.Toggle("무적 (피해 무시)", DebugTestSettings.PlayerInvincible);
        if (invincible != DebugTestSettings.PlayerInvincible) DebugTestSettings.PlayerInvincible = invincible;
    }

    private int GetSelectedAugmentIndex()
    {
        string guid = DebugTestSettings.ForcedAugmentGuid;
        if (string.IsNullOrEmpty(guid)) return 0;

        for (int i = 0; i < augments.Count; i++)
        {
            if (GetGuid(augments[i]) == guid) return i + 1;
        }

        return 0;
    }

    private static string GetGuid(AugmentData augment)
    {
        return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(augment));
    }
}
