using System.Collections.Generic;

// 한 판(스테이지 씬 하나) 동안만 쓰는 로그라이크 진행 데이터. 저장하지 않는다 (PlayerSaveData와 무관).
// RunFlow.Awake(RoomManager.Awake에서 생성)에서 초기화한다.
public static class RunState
{
    public const int MaxBanishes = 2;

    public static int Gold { get; private set; }  //런 골드: 상점/이벤트에만 쓰고 판이 끝나면 사라짐
    public static int TotalGoldEarned { get; private set; }
    public static int Kills { get; private set; }
    public static int MaxCombo { get; private set; }
    public static int PerfectCount { get; private set; }
    public static int BanishesLeft { get; private set; }

    public static float EnemyHpMultiplier { get; private set; } = 1f;  //저주받은 상자 등
    public static float GoldMultiplier { get; private set; } = 1f;
    public static float ShopDiscount { get; private set; }  //0~1

    // 승천(난이도 단계): 판 시작 때 고름. 단계가 오를수록 아래 효과가 누적된다
    public const int MaxAscension = 5;
    public static int Ascension { get; private set; }
    public static float AscensionEnemyHp => Ascension >= 1 ? 1.2f : 1f;       //1: 적 체력 +20%
    public static float AscensionEnemyAttack => Ascension >= 2 ? 1.2f : 1f;   //2: 적 공격력 +20%
    public static float AscensionShopPrice => Ascension >= 3 ? 1.3f : 1f;     //3: 상점 가격 +30%
    public static int AscensionSpecialBonus => Ascension >= 4 ? 1 : 0;        //4: 특수 적 +1
    public static float AscensionBossHp => Ascension >= 5 ? 1.3f : 1f;        //5: 보스 체력 +30%
    public static float AscensionGoldReward => 1f + 0.25f * Ascension;        //보상: 클리어 골드 단계당 +25%

    public static void SetAscension(int level) => Ascension = UnityEngine.Mathf.Clamp(level, 0, MaxAscension);

    public static readonly List<AugmentData> PickedAugments = new List<AugmentData>();
    public static readonly HashSet<AugmentType> BanishedTypes = new HashSet<AugmentType>();

    public static event System.Action<int> GoldChanged;

    public static void Reset()
    {
        Gold = 0;
        TotalGoldEarned = 0;
        Kills = 0;
        MaxCombo = 0;
        PerfectCount = 0;
        BanishesLeft = MaxBanishes;
        EnemyHpMultiplier = 1f;
        GoldMultiplier = 1f;
        ShopDiscount = 0f;
        Ascension = 0;
        PickedAugments.Clear();
        BanishedTypes.Clear();
        GoldChanged?.Invoke(Gold);
    }

    public static int AddGold(int amount, bool applyMultiplier = true)
    {
        if (amount <= 0) return 0;

        int gained = applyMultiplier ? UnityEngine.Mathf.RoundToInt(amount * GoldMultiplier) : amount;
        Gold += gained;
        TotalGoldEarned += gained;
        GoldChanged?.Invoke(Gold);
        return gained;
    }

    public static bool TrySpendGold(int amount)
    {
        if (amount < 0 || Gold < amount) return false;

        Gold -= amount;
        GoldChanged?.Invoke(Gold);
        return true;
    }

    public static int GetPrice(int basePrice) => UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(basePrice * AscensionShopPrice * (1f - ShopDiscount)));

    public static void RecordKill() => Kills++;
    public static void RecordCombo(int combo) { if (combo > MaxCombo) MaxCombo = combo; }
    public static void RecordPerfect() => PerfectCount++;
    public static void RecordAugment(AugmentData augment) { if (augment != null) PickedAugments.Add(augment); }

    public static bool TryUseBanish()
    {
        if (BanishesLeft <= 0) return false;
        BanishesLeft--;
        return true;
    }

    public static void AddBanishes(int amount) => BanishesLeft += amount;
    public static void MultiplyEnemyHp(float multiplier) => EnemyHpMultiplier *= multiplier;
    public static void MultiplyGold(float multiplier) => GoldMultiplier *= multiplier;
    public static void SetShopDiscount(float discount) => ShopDiscount = UnityEngine.Mathf.Clamp01(discount);
}
