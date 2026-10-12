using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

// 로비 '도감' 창: 만난 적과 보스를 카드로 모아 보고, 누르면 자세한 정보를 본다.
// - 목록은 스테이지와 같은 Addressables 라벨(EnemyData_Normal/Elite/Boss)로 읽기만 한다
// - 기록은 세이브(encounteredEnemies, defeatedBosses, enemyKillCounts)
// - 카드 그림은 CodexPortraitStudio가 실제 적 모델을 찍은 것 (못 만난 적은 실루엣)
// - 이름/설명은 도감 전용 번역 키(codex_name_*, codex_desc_*). 보스 이름은 EnemyData의 번역 이름을 그대로 쓴다
public class CodexPopup : MonoBehaviour
{
    private enum Category { Boss, Elite, Normal }

    private class Entry
    {
        public EnemyData data;
        public Category category;
        public string id;
        public bool encountered;
        public bool defeated;
        public int kills;
        public RenderTexture portrait;
    }

    private static readonly (string label, Category category)[] Sections =
    {
        ("EnemyData_Boss", Category.Boss),
        ("EnemyData_Elite", Category.Elite),
        ("EnemyData_Normal", Category.Normal),
    };

    private const float CardWidth = 284f;
    private const float CardHeight = 368f;
    private const float CardSpacing = 16f;

    private LobbyUiKit kit;
    private RectTransform root;
    private RectTransform listContent;
    private TMP_Text progressText;
    private Image progressFill;
    private TMP_Text statusText;
    private RectTransform detailRoot;
    private CodexPortraitStudio studio;
    private readonly List<AsyncOperationHandle<IList<EnemyData>>> handles = new List<AsyncOperationHandle<IList<EnemyData>>>();
    private bool closed;

    public static void Show(Transform parent)
    {
        LobbyUiKit kit = LobbyUiKit.Load();
        if (kit == null || parent == null) return;

        GameObject go = new GameObject("CodexPopup", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        LobbyUiKit.Stretch((RectTransform)go.transform);
        go.transform.SetAsLastSibling();

        CodexPopup popup = go.AddComponent<CodexPopup>();
        popup.kit = kit;
        popup.root = (RectTransform)go.transform;
        popup.Build();
    }

    // ── 목록 ──

    private void Build()
    {
        studio = CodexPortraitStudio.Create();

        kit.Popup(transform, "Popup", LobbyUiKit.Loc("codex_title", "몬스터 도감"), new Vector2(980f, 1600f), Close, out RectTransform content);

        // 발견 진행도
        RectTransform header = NewRect("Header", content);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.sizeDelta = new Vector2(0f, 110f);

        progressText = kit.Text(header, "Progress", "", 36f, kit.outlineMaterial, TextAlignmentOptions.Left);
        RectTransform progressRect = progressText.rectTransform;
        progressRect.anchorMin = new Vector2(0f, 0.5f);
        progressRect.anchorMax = new Vector2(1f, 1f);
        progressRect.offsetMin = new Vector2(10f, 0f);
        progressRect.offsetMax = new Vector2(-10f, 0f);

        TMP_Text hint = kit.Text(header, "Hint", LobbyUiKit.Loc("codex_tap_hint", "카드를 누르면 자세히 볼 수 있어요"), 26f, kit.shadowMaterial, TextAlignmentOptions.Right);
        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = new Vector2(0f, 0.5f);
        hintRect.anchorMax = new Vector2(1f, 1f);
        hintRect.offsetMin = new Vector2(10f, 0f);
        hintRect.offsetMax = new Vector2(-10f, 0f);
        hint.color = new Color(0.75f, 0.85f, 1f);

        Image barBack = kit.Image(header, "BarBack", kit.cellFrame, new Color(0f, 0f, 0f, 0.45f), true);
        RectTransform barRect = barBack.rectTransform;
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0.5f);
        barRect.offsetMin = new Vector2(10f, 12f);
        barRect.offsetMax = new Vector2(-10f, -6f);

        progressFill = kit.Image(barRect, "Fill", kit.cellFrame, new Color(1f, 0.78f, 0.25f), true);
        RectTransform fillRect = progressFill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0.02f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        // 스크롤 목록
        RectTransform scrollRect = NewRect("Scroll", content);
        LobbyUiKit.Stretch(scrollRect);
        scrollRect.offsetMax = new Vector2(0f, -124f);

        RectTransform viewport = NewRect("Viewport", scrollRect);
        LobbyUiKit.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        Image viewportHit = viewport.gameObject.AddComponent<Image>();
        viewportHit.color = new Color(0f, 0f, 0f, 0f);

        listContent = NewRect("Content", viewport);
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18f;
        layout.padding = new RectOffset(0, 0, 0, 30);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = listContent;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        statusText = kit.Text(scrollRect, "Status", LobbyUiKit.Loc("ranking_loading", "불러오는 중..."), 36f, kit.shadowMaterial);
        LobbyUiKit.Stretch(statusText.rectTransform);

        Load();
    }

    private async void Load()
    {
        List<Entry>[] sections = new List<Entry>[Sections.Length];
        PlayerSaveData save = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
        HashSet<string> added = new HashSet<string>();

        for (int i = 0; i < Sections.Length; i++)
        {
            AsyncOperationHandle<IList<EnemyData>> handle = Addressables.LoadAssetsAsync<EnemyData>(Sections[i].label, null);
            handles.Add(handle);
            await handle.Task;
            if (closed) return;

            sections[i] = new List<Entry>();
            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null) continue;

            foreach (EnemyData data in handle.Result)
            {
                if (data == null || !added.Add(data.name)) continue;  //라벨이 겹치는 에셋은 한 번만

                bool defeated = save != null && save.defeatedBosses.Contains(data.name);
                EnemyKillCountData killEntry = save?.enemyKillCounts.Find(item => item.enemyId == data.name);
                sections[i].Add(new Entry
                {
                    data = data,
                    category = Sections[i].category,
                    id = ToId(data.name),
                    encountered = defeated || (save != null && save.encounteredEnemies.Contains(data.name)),
                    defeated = defeated,
                    kills = killEntry != null ? killEntry.count : 0,
                });
            }

            sections[i].Sort((a, b) => a.data.maxHp.CompareTo(b.data.maxHp));  //약한 적부터
        }

        statusText.text = "";

        int total = 0;
        int seen = 0;
        for (int i = 0; i < sections.Length; i++)
        {
            if (sections[i] == null || sections[i].Count == 0) continue;

            CreateSectionHeader(Sections[i].category, sections[i]);

            RectTransform grid = NewRect("Grid", listContent);
            GridLayoutGroup gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(CardWidth, CardHeight);
            gridLayout.spacing = new Vector2(CardSpacing, CardSpacing);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 3;
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            int rows = Mathf.CeilToInt(sections[i].Count / 3f);
            grid.gameObject.AddComponent<LayoutElement>().preferredHeight = rows * CardHeight + (rows - 1) * CardSpacing;

            int index = 0;
            foreach (Entry entry in sections[i])
            {
                entry.portrait = studio.Request(entry.data.enemyPrefab, entry.data.tint, !entry.encountered);
                CreateCard(grid, entry, index++);

                total++;
                if (entry.encountered) seen++;
            }
        }

        progressText.text = string.Format(LobbyUiKit.Loc("codex_progress_format", "발견 {0} / {1}"), seen, total);
        progressFill.rectTransform.DOAnchorMax(new Vector2(Mathf.Max(0.02f, total > 0 ? (float)seen / total : 0f), 1f), 0.6f).SetEase(Ease.OutCubic).SetLink(progressFill.gameObject);
        if (total == 0) statusText.text = LobbyUiKit.Loc("codex_empty", "도감을 불러오지 못했어요.");
    }

    private void CreateSectionHeader(Category category, List<Entry> entries)
    {
        int seen = entries.FindAll(entry => entry.encountered).Count;

        RectTransform header = NewRect("SectionHeader", listContent);
        header.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;

        Image line = kit.Image(header, "Line", kit.cellFrame, CategoryColor(category, 0.9f), true);
        RectTransform lineRect = line.rectTransform;
        lineRect.anchorMin = new Vector2(0f, 0f);
        lineRect.anchorMax = new Vector2(1f, 0f);
        lineRect.pivot = new Vector2(0.5f, 0f);
        lineRect.sizeDelta = new Vector2(0f, 8f);

        TMP_Text title = kit.Text(header, "Title", $"{CategoryName(category)}  <size=70%><color=#C8D6F0>{seen} / {entries.Count}</color></size>", 42f, kit.outlineMaterial, TextAlignmentOptions.Left);
        LobbyUiKit.Stretch(title.rectTransform);
        title.rectTransform.offsetMin = new Vector2(10f, 10f);
    }

    private void CreateCard(RectTransform parent, Entry entry, int index)
    {
        Color frameColor = entry.encountered ? CategoryColor(entry.category, 1f) : new Color(0.07f, 0.1f, 0.18f, 1f);
        Image card = kit.Image(parent, entry.data.name, kit.cellFrame, frameColor, true);
        card.raycastTarget = true;
        RectTransform rect = card.rectTransform;

        Button button = card.gameObject.AddComponent<Button>();
        button.onClick.AddListener(() =>
        {
            SoundManager.Instance?.PlaySFX(SoundId.UI_Click);
            OpenDetail(entry);
        });

        // 그림 뒤 둥근 빛
        Image glow = kit.Image(rect, "Glow", kit.circleButton, entry.encountered ? new Color(1f, 1f, 1f, 0.16f) : new Color(1f, 1f, 1f, 0.05f));
        RectTransform glowRect = glow.rectTransform;
        glowRect.anchorMin = glowRect.anchorMax = new Vector2(0.5f, 1f);
        glowRect.pivot = new Vector2(0.5f, 1f);
        glowRect.anchoredPosition = new Vector2(0f, -22f);
        glowRect.sizeDelta = new Vector2(220f, 220f);

        RawImage portrait = NewRect("Portrait", rect).gameObject.AddComponent<RawImage>();
        portrait.texture = entry.portrait;
        portrait.raycastTarget = false;
        RectTransform portraitRect = portrait.rectTransform;
        portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0.5f, 1f);
        portraitRect.pivot = new Vector2(0.5f, 1f);
        portraitRect.anchoredPosition = new Vector2(0f, -6f);
        portraitRect.sizeDelta = new Vector2(256f, 256f);

        if (!entry.encountered)
        {
            Image lockIcon = kit.Image(rect, "Lock", kit.lockIcon, new Color(1f, 1f, 1f, 0.85f));
            RectTransform lockRect = lockIcon.rectTransform;
            lockRect.anchorMin = lockRect.anchorMax = new Vector2(0.5f, 1f);
            lockRect.anchoredPosition = new Vector2(0f, -150f);
            lockRect.sizeDelta = new Vector2(64f, 64f);
        }
        else if (entry.defeated)
        {
            Image crown = kit.Image(rect, "Crown", kit.crownIcon, new Color(1f, 0.85f, 0.3f));
            RectTransform crownRect = crown.rectTransform;
            crownRect.anchorMin = crownRect.anchorMax = new Vector2(1f, 1f);
            crownRect.anchoredPosition = new Vector2(-36f, -36f);
            crownRect.sizeDelta = new Vector2(58f, 58f);
            crownRect.localRotation = Quaternion.Euler(0f, 0f, -14f);
        }

        // 이름판
        Image plate = kit.Image(rect, "NamePlate", kit.cellFrame, new Color(0f, 0f, 0f, 0.38f), true);
        RectTransform plateRect = plate.rectTransform;
        plateRect.anchorMin = new Vector2(0f, 0f);
        plateRect.anchorMax = new Vector2(1f, 0f);
        plateRect.pivot = new Vector2(0.5f, 0f);
        plateRect.offsetMin = new Vector2(12f, 12f);
        plateRect.offsetMax = new Vector2(-12f, 0f);
        plateRect.sizeDelta = new Vector2(plateRect.sizeDelta.x, 96f);

        TMP_Text nameText = kit.Text(plateRect, "Name", entry.encountered ? GetDisplayName(entry) : "???", 32f, kit.outlineMaterial);
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 0.42f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.offsetMin = new Vector2(8f, 0f);
        nameRect.offsetMax = new Vector2(-8f, -2f);
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 20f;
        nameText.fontSizeMax = 32f;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;

        string sub = entry.encountered ? string.Format(LobbyUiKit.Loc("codex_kills_format", "처치 {0}"), entry.kills) : LobbyUiKit.Loc("codex_not_met", "아직 못 만났어요");
        TMP_Text subText = kit.Text(plateRect, "Sub", sub, 24f, kit.shadowMaterial);
        RectTransform subRect = subText.rectTransform;
        subRect.anchorMin = new Vector2(0f, 0f);
        subRect.anchorMax = new Vector2(1f, 0.45f);
        subRect.offsetMin = new Vector2(8f, 4f);
        subRect.offsetMax = new Vector2(-8f, 0f);
        subText.color = entry.encountered ? new Color(1f, 0.86f, 0.45f) : new Color(0.65f, 0.7f, 0.8f);

        // 순서대로 톡톡 나타남
        rect.localScale = Vector3.one * 0.85f;
        CanvasGroup group = card.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        float delay = 0.04f * index;
        rect.DOScale(1f, 0.28f).SetDelay(delay).SetEase(Ease.OutBack).SetLink(card.gameObject);
        group.DOFade(1f, 0.2f).SetDelay(delay).SetLink(card.gameObject);
    }

    // ── 자세히 보기 ──

    private void OpenDetail(Entry entry)
    {
        if (detailRoot != null) Destroy(detailRoot.gameObject);

        detailRoot = NewRect("Detail", root);
        LobbyUiKit.Stretch(detailRoot);

        Image dim = kit.Image(detailRoot, "Dim", null, new Color(0.01f, 0.03f, 0.1f, 0.82f));
        dim.raycastTarget = true;
        LobbyUiKit.Stretch(dim.rectTransform);
        dim.rectTransform.offsetMin = new Vector2(-800f, -500f);
        dim.rectTransform.offsetMax = new Vector2(800f, 500f);
        dim.gameObject.AddComponent<Button>().onClick.AddListener(CloseDetail);

        Image window = kit.Image(detailRoot, "Window", kit.popupFrame, Color.white, true);
        window.raycastTarget = true;
        RectTransform windowRect = window.rectTransform;
        windowRect.sizeDelta = new Vector2(920f, 1500f);

        Button close = kit.Button(windowRect, "CloseButton", kit.redButton, new Vector2(96f, 96f), CloseDetail);
        RectTransform closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-62f, -62f);
        Image closeIcon = kit.Image(closeRect, "Icon", kit.closeIcon, Color.white);
        LobbyUiKit.Stretch(closeIcon.rectTransform);
        closeIcon.rectTransform.offsetMin = new Vector2(18f, 18f);
        closeIcon.rectTransform.offsetMax = new Vector2(-18f, -18f);

        // 큰 그림
        Image stage = kit.Image(windowRect, "Stage", kit.cellFrame, entry.encountered ? CategoryColor(entry.category, 0.95f) : new Color(0.07f, 0.1f, 0.18f, 1f), true);
        RectTransform stageRect = stage.rectTransform;
        stageRect.anchorMin = new Vector2(0f, 1f);
        stageRect.anchorMax = new Vector2(1f, 1f);
        stageRect.pivot = new Vector2(0.5f, 1f);
        stageRect.offsetMin = new Vector2(48f, 0f);
        stageRect.offsetMax = new Vector2(-48f, 0f);
        stageRect.anchoredPosition = new Vector2(0f, -150f);
        stageRect.sizeDelta = new Vector2(-96f, 470f);

        Image glow = kit.Image(stageRect, "Glow", kit.circleButton, new Color(1f, 1f, 1f, entry.encountered ? 0.18f : 0.05f));
        glow.rectTransform.sizeDelta = new Vector2(400f, 400f);
        glow.rectTransform.DOScale(1.06f, 1.2f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(glow.gameObject);

        RawImage portrait = NewRect("Portrait", stageRect).gameObject.AddComponent<RawImage>();
        portrait.texture = entry.portrait;
        portrait.raycastTarget = false;
        portrait.rectTransform.sizeDelta = new Vector2(460f, 460f);

        TMP_Text chip = kit.Text(stageRect, "Category", CategoryName(entry.category), 30f, kit.outlineMaterial, TextAlignmentOptions.Left);
        RectTransform chipRect = chip.rectTransform;
        chipRect.anchorMin = chipRect.anchorMax = new Vector2(0f, 1f);
        chipRect.pivot = new Vector2(0f, 1f);
        chipRect.anchoredPosition = new Vector2(24f, -16f);
        chipRect.sizeDelta = new Vector2(300f, 50f);

        // 이름
        TMP_Text nameText = kit.Text(windowRect, "Name", entry.encountered ? GetDisplayName(entry) : "???", 58f, kit.outlineThickMaterial);
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.5f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0f, -40f);
        nameRect.sizeDelta = new Vector2(680f, 90f);
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 34f;
        nameText.fontSizeMax = 58f;

        // 설명
        string description = entry.encountered
            ? LobbyUiKit.Loc("codex_desc_" + entry.id, "")
            : LobbyUiKit.Loc("codex_unknown_desc", "아직 만나지 못한 적이에요. 모험을 계속하면 정체를 알 수 있어요!");
        TMP_Text descText = kit.Text(windowRect, "Description", description, 32f, kit.shadowMaterial, TextAlignmentOptions.TopLeft);
        RectTransform descRect = descText.rectTransform;
        descRect.anchorMin = new Vector2(0f, 1f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.pivot = new Vector2(0.5f, 1f);
        descRect.anchoredPosition = new Vector2(0f, -640f);
        descRect.sizeDelta = new Vector2(-120f, 200f);
        descText.enableAutoSizing = true;
        descText.fontSizeMin = 22f;
        descText.fontSizeMax = 32f;
        descText.lineSpacing = 8f;

        // 능력치
        RectTransform statPanel = NewRect("Stats", windowRect);
        statPanel.anchorMin = new Vector2(0f, 0f);
        statPanel.anchorMax = new Vector2(1f, 1f);
        statPanel.offsetMin = new Vector2(60f, 60f);
        statPanel.offsetMax = new Vector2(-60f, -860f);
        VerticalLayoutGroup statLayout = statPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        statLayout.spacing = 10f;
        statLayout.childControlWidth = true;
        statLayout.childControlHeight = false;
        statLayout.childForceExpandHeight = false;
        statLayout.childAlignment = TextAnchor.UpperCenter;

        if (entry.encountered)
        {
            foreach ((string label, string value) in BuildStats(entry)) CreateStatRow(statPanel, label, value);
        }

        windowRect.localScale = Vector3.one * 0.9f;
        windowRect.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetLink(windowRect.gameObject);
    }

    private List<(string label, string value)> BuildStats(Entry entry)
    {
        EnemyData data = entry.data;
        List<(string, string)> stats = new List<(string, string)>
        {
            (LobbyUiKit.Loc("codex_stat_hp", "기본 체력"), data.maxHp.ToString()),
            (LobbyUiKit.Loc("codex_stat_attack", "공격력"), (data.attackType == EnemyAttackType.Ranged ? data.projectileDamage : data.attack).ToString("0.#")),
            (LobbyUiKit.Loc("codex_stat_style", "공격 방식"), StyleName(data)),
            (LobbyUiKit.Loc("codex_stat_special", "특수 능력"), SpecialName(entry)),
        };

        string status = StatusName(data.projectileStatusEffect != null ? data.projectileStatusEffect : data.statusEffectOnHit);
        if (status != null) stats.Add((LobbyUiKit.Loc("codex_stat_status", "상태이상"), status));

        stats.Add((LobbyUiKit.Loc("codex_stat_kills", "처치 수"), entry.kills.ToString("N0")));

        if (entry.category == Category.Boss)
        {
            string reward = entry.defeated
                ? LobbyUiKit.Loc("codex_reward_claimed", "받음")
                : string.Format(LobbyUiKit.Loc("codex_boss_reward_format", "첫 처치 +{0}"), StageProgressTracker.BossFirstKillReward);
            stats.Add((LobbyUiKit.Loc("codex_stat_reward", "첫 처치 보상"), reward));
        }

        return stats;
    }

    private void CreateStatRow(RectTransform parent, string label, string value)
    {
        Image row = kit.Image(parent, "Row", kit.cellFrame, new Color(0.06f, 0.14f, 0.3f, 0.95f), true);
        row.rectTransform.sizeDelta = new Vector2(0f, 72f);

        TMP_Text labelText = kit.Text(row.rectTransform, "Label", label, 30f, kit.outlineMaterial, TextAlignmentOptions.Left);
        LobbyUiKit.Stretch(labelText.rectTransform);
        labelText.rectTransform.offsetMin = new Vector2(28f, 0f);
        labelText.color = new Color(0.72f, 0.82f, 1f);

        TMP_Text valueText = kit.Text(row.rectTransform, "Value", value, 30f, kit.outlineMaterial, TextAlignmentOptions.Right);
        LobbyUiKit.Stretch(valueText.rectTransform);
        valueText.rectTransform.offsetMin = new Vector2(260f, 0f);
        valueText.rectTransform.offsetMax = new Vector2(-28f, 0f);
        valueText.enableAutoSizing = true;
        valueText.fontSizeMin = 20f;
        valueText.fontSizeMax = 30f;
    }

    private void CloseDetail()
    {
        if (detailRoot == null) return;

        SoundManager.Instance?.PlaySFX(SoundId.UI_Click);
        Destroy(detailRoot.gameObject);
        detailRoot = null;
    }

    // ── 문구 ──

    private static string ToId(string assetName)  //"Normal Melee Enemy" → "normal_melee_enemy"
    {
        StringBuilder builder = new StringBuilder();
        foreach (char c in assetName.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) builder.Append(c);
            else if (builder.Length > 0 && builder[builder.Length - 1] != '_') builder.Append('_');
        }

        return builder.ToString().Trim('_');
    }

    private static string GetDisplayName(Entry entry)
    {
        // 보스는 전투 화면과 같은 이름 (EnemyData 번역), 나머지는 도감 이름
        if (entry.category == Category.Boss) return GetDataName(entry.data);
        return LobbyUiKit.Loc("codex_name_" + entry.id, GetDataName(entry.data));
    }

    private static string GetDataName(EnemyData enemy)
    {
        if (enemy.localizedEnemyName != null && !enemy.localizedEnemyName.IsEmpty)
        {
            try
            {
                string localized = enemy.localizedEnemyName.GetLocalizedString();
                if (!string.IsNullOrEmpty(localized)) return localized;
            }
            catch (System.Exception)
            {
                //번역 표를 아직 못 읽었으면 기본 이름
            }
        }

        return enemy.enemyName;
    }

    private static string CategoryName(Category category)
    {
        switch (category)
        {
            case Category.Boss: return LobbyUiKit.Loc("codex_section_boss", "보스");
            case Category.Elite: return LobbyUiKit.Loc("codex_section_elite", "엘리트");
            default: return LobbyUiKit.Loc("codex_section_normal", "몬스터");
        }
    }

    private static Color CategoryColor(Category category, float alpha)
    {
        switch (category)
        {
            case Category.Boss: return new Color(0.55f, 0.13f, 0.2f, alpha);
            case Category.Elite: return new Color(0.4f, 0.2f, 0.62f, alpha);
            default: return new Color(0.12f, 0.27f, 0.55f, alpha);
        }
    }

    private static string StyleName(EnemyData data)
    {
        switch (data.attackType)
        {
            case EnemyAttackType.Ranged: return LobbyUiKit.Loc("codex_style_ranged", "원거리 투척");
            case EnemyAttackType.Boss: return LobbyUiKit.Loc("codex_style_boss", "보스 패턴");
            default: return LobbyUiKit.Loc("codex_style_melee", "근접 돌진");
        }
    }

    private static string SpecialName(Entry entry)
    {
        if (entry.category == Category.Elite) return LobbyUiKit.Loc("codex_special_elite", "무작위 엘리트 특성");
        if (entry.category == Category.Boss) return LobbyUiKit.Loc("codex_special_boss", "분노 페이즈");

        switch (entry.data.role)
        {
            case EnemyRole.Shield: return LobbyUiKit.Loc("codex_special_shield", "정면 피해 감소");
            case EnemyRole.Bomber: return LobbyUiKit.Loc("codex_special_bomber", "쓰러지면 폭발");
            case EnemyRole.Healer: return LobbyUiKit.Loc("codex_special_healer", "주변 적 회복");
            case EnemyRole.Spiky: return LobbyUiKit.Loc("codex_special_spiky", "부딪히면 가시 피해");
            default: return LobbyUiKit.Loc("codex_special_none", "없음");
        }
    }

    private static string StatusName(StatusEffectData status)
    {
        if (status == null) return null;

        switch (status.type)
        {
            case StatusEffectType.Burn: return LobbyUiKit.Loc("codex_status_burn", "화상");
            case StatusEffectType.Poison: return LobbyUiKit.Loc("codex_status_poison", "독");
            default: return null;
        }
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private void OnDestroy()
    {
        closed = true;
        if (studio != null) Destroy(studio.gameObject);

        foreach (AsyncOperationHandle<IList<EnemyData>> handle in handles)
        {
            if (handle.IsValid()) Addressables.Release(handle);
        }

        handles.Clear();
    }

    private void Close()
    {
        Destroy(gameObject);
    }
}
