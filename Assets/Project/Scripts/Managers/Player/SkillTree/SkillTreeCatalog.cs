// 캐릭터 고유 스킬 트리: 기본 강화 → A/B 갈래 중 하나 선택 → 2단계 → 궁극.
// 노드 이름/설명은 Localization UI 테이블 키 "skilltree_{캐릭터}_{노드}_name/desc".
public enum SkillTreeNode
{
    Base,
    A2,
    A3,
    B2,
    B3
}

public static class SkillTreeCatalog
{
    public const float ResetFeeRate = 0.2f;  //롤백 수수료: 쓴 골드의 20% (최소 ResetMinFee)
    public const int ResetMinFee = 500;

    public static int GetCost(SkillTreeNode node)
    {
        switch (node)
        {
            case SkillTreeNode.Base: return 1000;
            case SkillTreeNode.A2:
            case SkillTreeNode.B2: return 2500;
            default: return 5000;
        }
    }

    public static int GetTier(SkillTreeNode node)
    {
        switch (node)
        {
            case SkillTreeNode.Base: return 1;
            case SkillTreeNode.A2:
            case SkillTreeNode.B2: return 2;
            default: return 3;
        }
    }

    public static int GetBranch(SkillTreeNode node)  //0 공통, 1 A, 2 B
    {
        if (node == SkillTreeNode.A2 || node == SkillTreeNode.A3) return 1;
        if (node == SkillTreeNode.B2 || node == SkillTreeNode.B3) return 2;
        return 0;
    }

    public static bool IsOwned(SkillTreeProgressData progress, SkillTreeNode node)
    {
        if (progress == null) return false;

        int branch = GetBranch(node);
        if (branch != 0 && progress.branch != branch) return false;

        return progress.tier >= GetTier(node);
    }

    public static bool CanBuy(SkillTreeProgressData progress, SkillTreeNode node)  //바로 다음 단계이고, 다른 갈래를 이미 고르지 않았으면
    {
        int tier = progress != null ? progress.tier : 0;
        int chosenBranch = progress != null ? progress.branch : 0;
        int branch = GetBranch(node);

        if (GetTier(node) != tier + 1) return false;
        if (branch != 0 && chosenBranch != 0 && chosenBranch != branch) return false;

        return true;
    }

    public static int GetResetFee(SkillTreeProgressData progress)
    {
        if (progress == null || progress.tier == 0) return 0;

        return UnityEngine.Mathf.Max(ResetMinFee, UnityEngine.Mathf.RoundToInt(progress.spentGold * ResetFeeRate));
    }

    public static string GetNameKey(CharacterId characterId, SkillTreeNode node) => $"skilltree_{characterId.ToString().ToLower()}_{node.ToString().ToLower()}_name";
    public static string GetDescKey(CharacterId characterId, SkillTreeNode node) => $"skilltree_{characterId.ToString().ToLower()}_{node.ToString().ToLower()}_desc";

    // 전투 중 스킬이 지금 선택된 캐릭터의 노드를 가졌는지 확인
    public static bool HasNode(SkillTreeNode node)
    {
        if (SaveManager.Instance == null) return false;

        return IsOwned(SaveManager.Instance.GetSkillTree(SaveManager.Instance.Data.selectedCharacterId), node);
    }
}
