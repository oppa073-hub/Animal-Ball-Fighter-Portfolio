using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 로그라이크 진행 흐름 (씬 로컬, RoomManager.Start에서 생성).
// - 시작 축복 3택 1
// - 방 보상 뒤 갈림길: 다음 방을 문 3개 중에서 고름 (일반/엘리트/상점/휴식/이벤트). 보스 방은 고정
// - 전투 없는 방(상점/휴식/이벤트) 진행
// - 런 골드 (처치/방 클리어로 획득, 상점/이벤트에서 사용) HUD
// - 판이 끝나면 런 요약
// 저장 데이터는 건드리지 않는다 (RunState는 판이 끝나면 사라짐).
public class RunFlow : MonoBehaviour
{
    public static RunFlow Instance { get; private set; }

    private static readonly Color NormalColor = new Color(0.45f, 0.75f, 1f);
    private static readonly Color EliteColor = new Color(1f, 0.42f, 0.38f);
    private static readonly Color ShopColor = new Color(1f, 0.78f, 0.3f);
    private static readonly Color RestColor = new Color(0.45f, 0.9f, 0.55f);
    private static readonly Color EventColor = new Color(0.78f, 0.55f, 1f);
    private static readonly Color LegendaryColor = new Color(1f, 0.6f, 0.15f);

    private const int ShopHealPrice = 20;
    private const float ShopHealRatio = 0.3f;
    private const int ShopBanishPrice = 15;

    private PlayerBallController player;
    private PlayerStats playerStats;
    private Health playerHealth;

    private bool blessingShown;
    private readonly List<int> usedEvents = new List<int>();

    // 상점 진열 (방에 들어올 때 정함)
    private readonly List<AugmentData> shopItems = new List<AugmentData>();
    private readonly List<bool> shopSold = new List<bool>();
    private bool shopHealSold;


    public static void Create()
    {
        if (Instance != null) return;
        new GameObject("RunFlow").AddComponent<RunFlow>();
    }

    private void Awake()
    {
        Instance = this;
        RunState.Reset();

        player = FindAnyObjectByType<PlayerBallController>();
        if (player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
            playerHealth = player.GetComponent<Health>();
        }

        GameEvents.EnemyKilled += HandleEnemyKilled;
        GameEvents.ComboChanged += HandleComboChanged;
        PlayerPerfectBounce.OnPerfect += HandlePerfect;
        PlayerBallController.OnPlayerLaunched += HandleLaunched;
        PlayerBallController.OnPlayerHitWall += HandleWallHit;
        PlayerBallController.OnPlayerHitEnemy += HandlePlayerHitEnemy;
    }

    private void OnDestroy()
    {
        GameEvents.EnemyKilled -= HandleEnemyKilled;
        GameEvents.ComboChanged -= HandleComboChanged;
        PlayerPerfectBounce.OnPerfect -= HandlePerfect;
        PlayerBallController.OnPlayerLaunched -= HandleLaunched;
        PlayerBallController.OnPlayerHitWall -= HandleWallHit;
        PlayerBallController.OnPlayerHitEnemy -= HandlePlayerHitEnemy;

        if (Instance == this) Instance = null;
    }

    private static string Loc(string key, string fallback) => RunPopupUI.Loc(key, fallback);

    // ───────── 기록 / 골드 ─────────

    private void HandleEnemyKilled(EnemyController enemy, bool isBoss)
    {
        if (enemy == null) return;

        RunState.RecordKill();

        bool isElite = !isBoss && enemy.TryGetComponent(out EliteTraitController elite) && elite.HasTrait;
        RunState.AddGold(isBoss ? 25 : isElite ? 8 : 3);
    }

    private void HandleComboChanged(int combo) => RunState.RecordCombo(combo);
    private void HandlePerfect(Vector3 position, int chain) => RunState.RecordPerfect();

    public void OnRoomCleared(RoomType roomType)  //전투 방 클리어 보너스
    {
        RunState.AddGold(roomType == RoomType.Boss ? 20 : roomType == RoomType.Elite ? 12 : 5);

        if (launchesThisRoom == 1 && player != null)  //한 번 발사로 방 정리
        {
            int gained = RunState.AddGold(OneShotGold);
            OnTrickShot?.Invoke(Loc("trick_one_shot", "ONE SHOT!"), gained, player.transform.position);
        }

        launchesThisRoom = 0;  //wallBounces는 여기서 지우지 않음: 마지막 적 처치 때 방 클리어가 OnPlayerHitEnemy보다 먼저 와서 뱅크샷이 사라짐 (발사 때 초기화)
    }

    // ───────── 트릭샷 ─────────

    public static event System.Action<string, int, Vector3> OnTrickShot;  //문구, 얻은 골드, 위치 (연출: CombatJuice)

    private const int BankShotBounces = 3;
    private const int BankShotGold = 5;
    private const int OneShotGold = 15;

    private int launchesThisRoom;
    private int wallBounces;  //마지막 적 타격 이후 벽에 튕긴 횟수

    private void HandleLaunched()
    {
        launchesThisRoom++;
        wallBounces = 0;
    }

    private void HandleWallHit() => wallBounces++;

    private void HandlePlayerHitEnemy(GameObject enemyObject)  //벽에 여러 번 튕긴 뒤 처치 = 뱅크샷
    {
        int bounces = wallBounces;
        wallBounces = 0;

        if (bounces < BankShotBounces || enemyObject == null) return;
        if (!enemyObject.TryGetComponent(out EnemyController enemy) || !enemy.IsDead) return;  //이번 충돌로 죽었을 때만

        int gained = RunState.AddGold(BankShotGold);
        GameEvents.RaiseBankShot();
        OnTrickShot?.Invoke(string.Format(Loc("trick_bank_shot_format", "BANK SHOT x{0}!"), bounces), gained, enemyObject.transform.position);
    }

    // ───────── 시작 축복 ─────────

    public void ShowBlessingIfNeeded()  //첫 방 시작 전: (승천이 열려 있으면) 난이도 선택 → 축복 선택
    {
        if (blessingShown) return;
        blessingShown = true;

        CombatTopBar.GetOrCreate();  //런 골드는 위쪽 통합 바에 표시
        if (TutorialMode.IsActive) return;  //튜토리얼: 승천/축복 없음

        GameManager.Instance.currentState = GameState.RoomClear;  //고르는 동안 발사 막음

        int unlocked = SaveManager.Instance != null ? SaveManager.Instance.GetAscensionUnlocked() : 0;
        if (unlocked > 0 && !GameSessionData.IsEndless) ShowAscensionSelect(Mathf.Min(unlocked, RunState.MaxAscension));  //무한 모드는 자체 난이도 상승이 있어 승천 없음
        else ShowBlessing();
    }

    // ───────── 승천 (난이도 단계) ─────────

    private static readonly string[] AscensionEffectKeys = { "", "ascension_effect_1", "ascension_effect_2", "ascension_effect_3", "ascension_effect_4", "ascension_effect_5" };
    private static readonly string[] AscensionEffectFallbacks = { "", "Enemy HP +20%", "Enemy Attack +20%", "Shop prices +30%", "+1 special enemy per room", "Boss HP +30%" };

    private void ShowAscensionSelect(int unlocked)
    {
        var options = new List<RunPopupUI.Option>();

        for (int level = 0; level <= unlocked; level++)
        {
            int chosen = level;
            options.Add(new RunPopupUI.Option
            {
                title = level == 0 ? Loc("ascension_base", "Normal") : string.Format(Loc("ascension_level_format", "Ascension {0}"), level),
                description = GetAscensionDescription(level),
                tag = level > 0 ? string.Format(Loc("ascension_reward_format", "Gold +{0}%"), level * 25) : null,
                color = Color.Lerp(new Color(0.45f, 0.75f, 1f), new Color(1f, 0.3f, 0.3f), level / (float)RunState.MaxAscension),
                onClick = () =>
                {
                    RunState.SetAscension(chosen);
                    if (chosen > 0)
                    {
                        RoomManager.Instance?.ApplyAscensionToCurrentEnemies();  //첫 방 적은 이미 나와 있으므로 보정
                        HudBanner.Show(Loc("ascension_title", "Difficulty"), string.Format(Loc("ascension_level_format", "Ascension {0}"), chosen));
                    }
                    ShowBlessing();
                }
            });
        }

        RunPopupUI.Show(Loc("ascension_title", "Difficulty"), Loc("ascension_subtitle", "Clear a stage to unlock the next level."), options);
    }

    private static string GetAscensionDescription(int level)
    {
        if (level == 0) return Loc("ascension_base_desc", "No extra effects.");

        var effects = new List<string>();
        for (int i = 1; i <= level; i++) effects.Add(Loc(AscensionEffectKeys[i], AscensionEffectFallbacks[i]));
        return string.Join(" · ", effects);
    }

    private void ShowBlessing()
    {
        var all = new List<RunPopupUI.Option>
        {
            Blessing("blessing_attack", "Warrior's Blessing", "blessing_attack_desc", "Attack +10%", () => playerStats.AddAttackPercent(0.1f)),
            Blessing("blessing_hp", "Giant's Blessing", "blessing_hp_desc", "Max HP +20%", () => playerStats.AddMaxHpPercent(0.2f)),
            Blessing("blessing_gold", "Merchant's Blessing", "blessing_gold_desc", "Start with 60 run Gold", () => RunState.AddGold(60, false)),
            Blessing("blessing_speed", "Wind's Blessing", "blessing_speed_desc", "Move Speed +10%", () => playerStats.AddMoveSpeedPercent(0.1f)),
            Blessing("blessing_crit", "Lucky Blessing", "blessing_crit_desc", "Critical Chance +8%", () => playerStats.AddCriticalChance(0.08f)),
            Blessing("blessing_collector", "Collector's Blessing", "blessing_collector_desc", "Banish +2, Shop prices -10%", () => { RunState.AddBanishes(2); RunState.SetShopDiscount(0.1f); }),
        };

        RunPopupUI.Show(Loc("blessing_title", "Choose a Blessing"), Loc("blessing_subtitle", "It lasts for this run."), PickRandom(all, 3));
        TutorialEvents.Raise(TutorialStep.Blessing);  //첫 축복 선택 안내
    }

    private RunPopupUI.Option Blessing(string nameKey, string nameFallback, string descKey, string descFallback, System.Action apply)
    {
        return new RunPopupUI.Option
        {
            title = Loc(nameKey, nameFallback),
            description = Loc(descKey, descFallback),
            color = new Color(0.55f, 0.85f, 1f),
            onClick = () =>
            {
                apply?.Invoke();
                RunPopupUI.Hide();
                SoundManager.Instance?.PlaySFX(SoundId.AugmentSelect);
                if (GameManager.Instance.currentState == GameState.RoomClear) GameManager.Instance.currentState = GameState.Ready;
            }
        };
    }

    // ───────── 갈림길 ─────────

    public void OnRewardFinished()  //방 보상(증강/상점/이벤트)이 끝남 → 다음 방 고르기
    {
        RoomManager room = RoomManager.Instance;
        if (room == null) return;

        int nextRoom = room.CurrentRoom + 1;

        if (TutorialMode.IsActive)  //튜토리얼: 갈림길 없이 정해진 순서 (일반 → 일반 → 엘리트 → 보스)
        {
            RunPopupUI.Hide();
            room.SetNextRoomType(room.IsBossRoomNumber(nextRoom) ? RoomType.Boss : nextRoom == 3 ? RoomType.Elite : RoomType.Normal);
            room.StartNextRoom();
            return;
        }

        if (room.IsBossRoomNumber(nextRoom))  //보스 방은 고르지 않음
        {
            RunPopupUI.Hide();
            room.SetNextRoomType(RoomType.Boss);
            room.StartNextRoom();
            return;
        }

        GameManager.Instance.currentState = GameState.RoomClear;

        var options = new List<RunPopupUI.Option>();
        foreach (RoomType type in RollDoors(nextRoom)) options.Add(Door(type));

        RunPopupUI.Show(Loc("door_title", "Choose your path"), string.Format(Loc("door_subtitle_format", "Next: Room {0}"), nextRoom), options);
        TutorialEvents.Raise(TutorialStep.Door);  //첫 갈림길 안내
    }

    private const int FirstEliteRoom = 3;  //예전처럼 3번 방부터 엘리트 (엘리트 첫 클리어 = 증강 스킬 획득 시점)

    private List<RoomType> RollDoors(int nextRoom)
    {
        var doors = new List<RoomType> { RoomType.Normal };

        if (nextRoom >= FirstEliteRoom)
        {
            bool needSkill = AugmentManager.Instance != null && !AugmentManager.Instance.HasAugmentSkill;
            if (needSkill || Random.value < 0.6f) doors.Add(RoomType.Elite);  //스킬 증강이 없으면 엘리트 문은 항상
        }

        var specials = new List<RoomType> { RoomType.Shop, RoomType.Rest, RoomType.Event };
        while (doors.Count < 3 && specials.Count > 0)
        {
            int index = Random.Range(0, specials.Count);
            doors.Add(specials[index]);
            specials.RemoveAt(index);
        }

        return doors;
    }

    private RunPopupUI.Option Door(RoomType type)
    {
        string title;
        string desc;
        Color color;

        switch (type)
        {
            case RoomType.Elite:
                title = Loc("door_elite", "Elite");
                bool needSkill = AugmentManager.Instance != null && !AugmentManager.Instance.HasAugmentSkill;
                desc = needSkill ? Loc("door_elite_desc_skill", "Strong foes. Reward: Skill Augment, more Gold")
                                 : Loc("door_elite_desc", "Strong foes. Reward: Legendary Augment chance, more Gold");
                color = EliteColor;
                break;
            case RoomType.Shop:
                title = Loc("door_shop", "Shop");
                desc = Loc("door_shop_desc", "Spend run Gold on Augments and healing. No fight.");
                color = ShopColor;
                break;
            case RoomType.Rest:
                title = Loc("door_rest", "Campfire");
                desc = Loc("door_rest_desc", "Heal or train. No fight.");
                color = RestColor;
                break;
            case RoomType.Event:
                title = Loc("door_event", "???");
                desc = Loc("door_event_desc", "Something unexpected awaits. Risk and reward.");
                color = EventColor;
                break;
            default:
                title = Loc("door_normal", "Battle");
                desc = Loc("door_normal_desc", "A normal fight. Reward: Augment");
                color = NormalColor;
                break;
        }

        return new RunPopupUI.Option
        {
            title = title,
            description = desc,
            color = color,
            onClick = () =>
            {
                RunPopupUI.Hide();
                RoomManager.Instance.SetNextRoomType(type);
                RoomManager.Instance.StartNextRoom();
            }
        };
    }

    // ───────── 전투 없는 방 ─────────

    public void EnterSpecialRoom(RoomType type)
    {
        GameManager.Instance.currentState = GameState.RoomClear;

        switch (type)
        {
            case RoomType.Shop: OpenShop(true); break;
            case RoomType.Rest: OpenRest(); break;
            default: OpenEvent(); break;
        }
    }

    private void FinishSpecialRoom()
    {
        RunPopupUI.Hide();
        OnRewardFinished();
    }

    // 상점
    private void OpenShop(bool restock)
    {
        if (restock)
        {
            shopItems.Clear();
            shopSold.Clear();
            shopHealSold = false;

            if (AugmentManager.Instance != null) shopItems.AddRange(AugmentManager.Instance.GetShopOffers(3));
            foreach (AugmentData _ in shopItems) shopSold.Add(false);
        }

        var options = new List<RunPopupUI.Option>();

        for (int i = 0; i < shopItems.Count; i++)
        {
            int index = i;
            AugmentData augment = shopItems[i];
            int price = RunState.GetPrice(GetAugmentPrice(augment));
            bool sold = shopSold[i] || AugmentManager.Instance.IsUniqueOwned(augment.augmentType);  //전설/전용 증강을 다른 경로로 이미 얻었으면 판매 완료로
            bool legendary = LegendaryEffects.IsLegendary(augment.augmentType);

            options.Add(new RunPopupUI.Option
            {
                title = AugmentManager.Instance.GetAugmentDisplayName(augment),
                description = StripRichText(AugmentManager.Instance.GetAugmentDisplayDescription(augment)),
                tag = sold ? Loc("shop_sold", "SOLD") : $"{price} G",
                icon = augment.icon,
                color = legendary ? LegendaryColor : NormalColor,
                enabled = !sold && RunState.Gold >= price,
                onClick = () =>
                {
                    if (!RunState.TrySpendGold(price)) { OpenShop(false); return; }

                    shopSold[index] = true;
                    SoundManager.Instance?.PlaySFX(SoundId.CoinCollect);
                    AugmentManager.Instance.GrantAugment(augment);
                    OpenShop(false);
                }
            });
        }

        int healPrice = RunState.GetPrice(ShopHealPrice);
        options.Add(new RunPopupUI.Option
        {
            title = Loc("shop_heal", "Potion"),
            description = Loc("shop_heal_desc", "Heal 30% of Max HP."),
            tag = shopHealSold ? Loc("shop_sold", "SOLD") : $"{healPrice} G",
            color = RestColor,
            enabled = !shopHealSold && RunState.Gold >= healPrice,
            onClick = () =>
            {
                if (!RunState.TrySpendGold(healPrice)) { OpenShop(false); return; }

                shopHealSold = true;
                playerHealth?.Heal(playerHealth.MaxHp * ShopHealRatio);
                SoundManager.Instance?.PlaySFX(SoundId.HealSkill);
                OpenShop(false);
            }
        });

        int banishPrice = RunState.GetPrice(ShopBanishPrice);
        options.Add(new RunPopupUI.Option
        {
            title = Loc("shop_banish", "Eraser"),
            description = Loc("shop_banish_desc", "+1 Banish (remove an Augment card for this run)."),
            tag = $"{banishPrice} G",
            color = EventColor,
            enabled = RunState.Gold >= banishPrice,
            onClick = () =>
            {
                if (RunState.TrySpendGold(banishPrice)) RunState.AddBanishes(1);
                OpenShop(false);
            }
        });

        RunPopupUI.Show(Loc("shop_title", "Shop"), Loc("shop_subtitle", "Run Gold disappears when the run ends."), options, Loc("common_leave", "Leave"), FinishSpecialRoom);
        TutorialEvents.Raise(TutorialStep.SpecialRoom);
    }

    private static int GetAugmentPrice(AugmentData augment)
    {
        if (LegendaryEffects.IsLegendary(augment.augmentType)) return 80;

        return augment.grade switch
        {
            AugmentGrade.Rare => 35,
            AugmentGrade.Epic => 50,
            AugmentGrade.Legendary => 60,
            _ => 25
        };
    }

    private static string StripRichText(string text)  //카드용 색 태그가 팝업에서도 보이도록 그대로 두되 줄바꿈만 정리
    {
        return string.IsNullOrEmpty(text) ? "" : text.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");  //번역표 줄바꿈은 \r\n이라 \r도 지워야 함 (남으면 줄이 갈라져 글자가 겹침)
    }

    // 휴식
    private void OpenRest()
    {
        var options = new List<RunPopupUI.Option>
        {
            new RunPopupUI.Option
            {
                title = Loc("rest_heal", "Rest"),
                description = Loc("rest_heal_desc", "Heal 40% of Max HP."),
                color = RestColor,
                onClick = () =>
                {
                    playerHealth?.Heal(playerHealth.MaxHp * 0.4f);
                    SoundManager.Instance?.PlaySFX(SoundId.HealSkill);
                    FinishSpecialRoom();
                }
            },
            new RunPopupUI.Option
            {
                title = Loc("rest_train", "Train"),
                description = Loc("rest_train_desc", "Attack +8% for this run."),
                color = EliteColor,
                onClick = () =>
                {
                    playerStats?.AddAttackPercent(0.08f);
                    SoundManager.Instance?.PlaySFX(SoundId.AugmentSelect);
                    FinishSpecialRoom();
                }
            }
        };

        RunPopupUI.Show(Loc("rest_title", "Campfire"), Loc("rest_subtitle", "Choose one."), options);
        TutorialEvents.Raise(TutorialStep.SpecialRoom);
    }

    // 이벤트
    private void OpenEvent()
    {
        TutorialEvents.Raise(TutorialStep.SpecialRoom);  //첫 특별한 방 안내

        const int eventCount = 5;

        var candidates = new List<int>();
        for (int i = 0; i < eventCount; i++) if (!usedEvents.Contains(i)) candidates.Add(i);
        if (candidates.Count == 0) { usedEvents.Clear(); for (int i = 0; i < eventCount; i++) candidates.Add(i); }

        int eventId = candidates[Random.Range(0, candidates.Count)];
        usedEvents.Add(eventId);

        switch (eventId)
        {
            case 0: EventBloodAltar(); break;
            case 1: EventCursedChest(); break;
            case 2: EventGambler(); break;
            case 3: EventSpring(); break;
            default: EventMerchant(); break;
        }
    }

    private RunPopupUI.Option LeaveOption()
    {
        return new RunPopupUI.Option
        {
            title = Loc("event_leave", "Walk away"),
            description = Loc("event_leave_desc", "Nothing happens."),
            color = new Color(0.55f, 0.6f, 0.7f),
            onClick = FinishSpecialRoom
        };
    }

    private void EventBloodAltar()  //체력을 바쳐 전설 증강
    {
        int cost = playerHealth != null ? Mathf.Min(Mathf.RoundToInt(playerHealth.MaxHp * 0.3f), playerHealth.CurrentHp - 1) : 0;

        var options = new List<RunPopupUI.Option>
        {
            new RunPopupUI.Option
            {
                title = Loc("event_altar_offer", "Offer blood"),
                description = string.Format(Loc("event_altar_offer_desc", "Lose {0} HP. Choose a Legendary Augment."), cost),
                color = EliteColor,
                enabled = cost > 0 && AugmentManager.Instance != null && AugmentManager.Instance.IsReady,  //보상을 못 주면 체력도 안 깎음
                onClick = () =>
                {
                    RunPopupUI.Hide();
                    playerHealth?.TakeDamage(cost);
                    AugmentManager.Instance.ShowLegendaryChoices();  //고르면 갈림길로 이어짐
                }
            },
            LeaveOption()
        };

        RunPopupUI.Show(Loc("event_altar_title", "Blood Altar"), Loc("event_altar_story", "An altar hums, thirsty for blood."), options);
    }

    private void EventCursedChest()  //저주: 적 강화 대신 골드 2배
    {
        var options = new List<RunPopupUI.Option>
        {
            new RunPopupUI.Option
            {
                title = Loc("event_chest_open", "Open it"),
                description = Loc("event_chest_open_desc", "Get 40 Gold. For this run: enemy HP +25%, Gold gain x2."),
                color = EventColor,
                onClick = () =>
                {
                    RunState.AddGold(40);
                    RunState.MultiplyEnemyHp(1.25f);
                    RunState.MultiplyGold(2f);
                    SoundManager.Instance?.PlaySFX(SoundId.CoinCollect);
                    FinishSpecialRoom();
                }
            },
            LeaveOption()
        };

        RunPopupUI.Show(Loc("event_chest_title", "Cursed Chest"), Loc("event_chest_story", "Gold glitters inside... along with a dark aura."), options);
    }

    private void EventGambler()  //골드를 걸고 50% 확률로 증강
    {
        const int smallBet = 30;
        const int bigBet = 60;

        var options = new List<RunPopupUI.Option>
        {
            new RunPopupUI.Option
            {
                title = string.Format(Loc("event_gamble_small", "Bet {0} G"), smallBet),
                description = Loc("event_gamble_small_desc", "50%: a Rare+ Augment."),
                color = ShopColor,
                enabled = RunState.Gold >= smallBet,
                onClick = () =>
                {
                    if (!RunState.TrySpendGold(smallBet)) { FinishSpecialRoom(); return; }

                    if (Random.value < 0.5f) GiveRandomAugment(AugmentGrade.Rare);
                    else ShowGambleLost();
                }
            },
            new RunPopupUI.Option
            {
                title = string.Format(Loc("event_gamble_big", "Bet {0} G"), bigBet),
                description = Loc("event_gamble_big_desc", "50%: choose a Legendary Augment."),
                color = LegendaryColor,
                enabled = RunState.Gold >= bigBet,
                onClick = () =>
                {
                    if (!RunState.TrySpendGold(bigBet)) { FinishSpecialRoom(); return; }

                    if (Random.value < 0.5f)
                    {
                        RunPopupUI.Hide();
                        AugmentManager.Instance.ShowLegendaryChoices();
                    }
                    else ShowGambleLost();
                }
            },
            LeaveOption()
        };

        RunPopupUI.Show(Loc("event_gamble_title", "Gambler"), Loc("event_gamble_story", "\"Feeling lucky?\""), options);
    }

    private void ShowGambleLost()
    {
        var options = new List<RunPopupUI.Option> { new RunPopupUI.Option { title = Loc("event_gamble_lost", "You lost!"), description = Loc("event_gamble_lost_desc", "The gambler laughs."), color = new Color(0.55f, 0.6f, 0.7f) } };
        RunPopupUI.Show(Loc("event_gamble_title", "Gambler"), null, options, Loc("common_continue", "Continue"), FinishSpecialRoom);
    }

    private void GiveRandomAugment(AugmentGrade minGrade)
    {
        AugmentData augment = AugmentManager.Instance != null ? AugmentManager.Instance.GetRandomAugment(minGrade) : null;
        if (augment == null) { FinishSpecialRoom(); return; }

        AugmentManager.Instance.GrantAugment(augment);
        SoundManager.Instance?.PlaySFX(SoundId.AugmentSelect);

        var options = new List<RunPopupUI.Option>
        {
            new RunPopupUI.Option
            {
                title = AugmentManager.Instance.GetAugmentDisplayName(augment),
                description = StripRichText(AugmentManager.Instance.GetAugmentDisplayDescription(augment)),
                icon = augment.icon,
                color = NormalColor
            }
        };

        RunPopupUI.Show(Loc("event_reward_title", "You got an Augment!"), null, options, Loc("common_continue", "Continue"), FinishSpecialRoom);
    }

    private void EventSpring()  //샘물: 회복 또는 최대 체력
    {
        var options = new List<RunPopupUI.Option>
        {
            new RunPopupUI.Option
            {
                title = Loc("event_spring_drink", "Drink"),
                description = Loc("event_spring_drink_desc", "Heal 50% of Max HP."),
                color = RestColor,
                onClick = () =>
                {
                    playerHealth?.Heal(playerHealth.MaxHp * 0.5f);
                    SoundManager.Instance?.PlaySFX(SoundId.HealSkill);
                    FinishSpecialRoom();
                }
            },
            new RunPopupUI.Option
            {
                title = Loc("event_spring_bathe", "Bathe"),
                description = Loc("event_spring_bathe_desc", "Max HP +15%."),
                color = NormalColor,
                onClick = () =>
                {
                    playerStats?.AddMaxHpPercent(0.15f);
                    SoundManager.Instance?.PlaySFX(SoundId.AugmentSelect);
                    FinishSpecialRoom();
                }
            }
        };

        RunPopupUI.Show(Loc("event_spring_title", "Mysterious Spring"), Loc("event_spring_story", "Clear water sparkles softly."), options);
    }

    private void EventMerchant()  //수상한 상인: 골드로 버리기/할인
    {
        const int banishPrice = 20;
        const int discountPrice = 40;

        var options = new List<RunPopupUI.Option>
        {
            new RunPopupUI.Option
            {
                title = Loc("event_merchant_banish", "Buy erasers"),
                description = Loc("event_merchant_banish_desc", "+2 Banish."),
                tag = $"{banishPrice} G",
                color = EventColor,
                enabled = RunState.Gold >= banishPrice,
                onClick = () =>
                {
                    if (RunState.TrySpendGold(banishPrice)) RunState.AddBanishes(2);
                    FinishSpecialRoom();
                }
            },
            new RunPopupUI.Option
            {
                title = Loc("event_merchant_discount", "Membership card"),
                description = Loc("event_merchant_discount_desc", "Shop prices -25% for this run."),
                tag = $"{discountPrice} G",
                color = ShopColor,
                enabled = RunState.Gold >= discountPrice,
                onClick = () =>
                {
                    if (RunState.TrySpendGold(discountPrice)) RunState.SetShopDiscount(Mathf.Max(RunState.ShopDiscount, 0.25f));
                    FinishSpecialRoom();
                }
            },
            LeaveOption()
        };

        RunPopupUI.Show(Loc("event_merchant_title", "Shady Merchant"), Loc("event_merchant_story", "\"Psst... special goods, just for you.\""), options);
    }

    // ───────── 런 요약 ─────────

    public void ShowSummary(bool cleared, int reachedRoom)
    {
        var names = new List<string>();
        foreach (AugmentData augment in RunState.PickedAugments)
        {
            if (augment != null && AugmentManager.Instance != null) names.Add(AugmentManager.Instance.GetAugmentDisplayName(augment));
        }

        var options = new List<RunPopupUI.Option>
        {
            new RunPopupUI.Option
            {
                title = cleared ? Loc("summary_cleared", "Stage Clear!") : string.Format(Loc("summary_reached_format", "Reached Room {0}"), reachedRoom),
                description = string.Format(Loc("summary_stats_format", "Kills {0}  ·  Best Combo {1}  ·  Perfect {2}"), RunState.Kills, RunState.MaxCombo, RunState.PerfectCount),
                color = cleared ? RestColor : EliteColor
            },
            new RunPopupUI.Option
            {
                title = string.Format(Loc("summary_gold_format", "Run Gold earned: {0}"), RunState.TotalGoldEarned),
                description = Loc("summary_gold_desc", "Run Gold is only used inside a run."),
                color = ShopColor
            },
            new RunPopupUI.Option
            {
                title = string.Format(Loc("summary_augments_format", "Augments ({0})"), names.Count),
                description = names.Count > 0 ? string.Join(", ", names) : "-",
                color = NormalColor
            }
        };

        RunPopupUI.Show(Loc("summary_title", "Run Summary"), null, options, Loc("common_ok", "OK"), RunPopupUI.Hide);
    }

    // ───────── 공용 ─────────

    private static List<T> PickRandom<T>(List<T> source, int count)
    {
        var pool = new List<T>(source);
        var result = new List<T>();

        while (result.Count < count && pool.Count > 0)
        {
            int index = Random.Range(0, pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
    }
}
