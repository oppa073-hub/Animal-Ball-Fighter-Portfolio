using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

// 캐릭터 고유 스킬 트리 팝업 (로비 캐릭터 화면). 실행 중에 만든다.
// [기본] → [A2]/[B2] 중 택1 → [A3]/[B3] 궁극. 골드로 강화, 초기화(롤백)는 수수료를 떼고 환불.
public class SkillTreePopupUI : MonoBehaviour
{
    private static SkillTreePopupUI instance;

    private static readonly SkillTreeNode[] Nodes = { SkillTreeNode.Base, SkillTreeNode.A2, SkillTreeNode.B2, SkillTreeNode.A3, SkillTreeNode.B3 };
    private static readonly Color Owned = new Color(1f, 0.82f, 0.3f);
    private static readonly Color Available = new Color(0.45f, 0.8f, 1f);
    private static readonly Color Locked = new Color(0.35f, 0.4f, 0.5f);

    private CombatUiStyle style;
    private RectTransform window;
    private CharacterData character;
    private SkillTreeNode selectedNode;

    private TMP_Text titleText;
    private TMP_Text detailNameText;
    private TMP_Text detailDescText;
    private Button buyButton;
    private TMP_Text buyText;
    private Button resetButton;
    private TMP_Text resetText;

    private readonly Dictionary<SkillTreeNode, Image> nodeFrames = new();
    private readonly Dictionary<SkillTreeNode, TMP_Text> nodeStates = new();
    private readonly Dictionary<SkillTreeNode, Image> nodeSelect = new();
    private readonly List<(Image line, SkillTreeNode to)> lines = new();

    public static void Open(CharacterData data, Canvas canvas)
    {
        if (data == null || canvas == null) return;

        if (instance == null)
        {
            var go = new GameObject("SkillTreePopup", typeof(RectTransform));
            go.transform.SetParent(canvas.rootCanvas.transform, false);
            instance = go.AddComponent<SkillTreePopupUI>();
            instance.Build();
        }

        // 토스트(골드 부족 등)가 팝업 위에 보이도록 토스트 바로 아래 층에 둠
        Transform toast = ToastMessageManager.Instance != null ? ToastMessageManager.Instance.transform : null;
        if (toast != null && toast.parent != null)
        {
            instance.transform.SetParent(toast.parent, false);
            instance.transform.SetSiblingIndex(toast.GetSiblingIndex());
        }
        else instance.transform.SetAsLastSibling();

        instance.Show(data);
    }

    private void OnEnable()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnSkillTreeChanged += HandleChanged;
            SaveManager.Instance.OnGoldChanged += HandleGoldChanged;
        }
    }

    private void OnDisable()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnSkillTreeChanged -= HandleChanged;
            SaveManager.Instance.OnGoldChanged -= HandleGoldChanged;
        }
    }

    private void HandleChanged(CharacterId id) => Refresh();
    private void HandleGoldChanged(int gold) => Refresh();

    private void Show(CharacterData data)
    {
        character = data;
        selectedNode = FirstUsefulNode();

        gameObject.SetActive(true);
        Refresh();

        window.DOKill();
        window.localScale = Vector3.one * 0.85f;
        window.DOScale(1f, 0.22f).SetEase(Ease.OutBack);
    }

    private void Close()
    {
        window.DOKill();
        gameObject.SetActive(false);
    }

    private SkillTreeNode FirstUsefulNode()  //처음 열면 다음에 살 수 있는 노드를 선택
    {
        SkillTreeProgressData progress = SaveManager.Instance?.GetSkillTree(character.characterId);
        foreach (SkillTreeNode node in Nodes) if (SkillTreeCatalog.CanBuy(progress, node)) return node;
        return SkillTreeNode.Base;
    }

    private bool IsUnlockedCharacter => SaveManager.Instance != null && SaveManager.Instance.Data.unlockedCharacters.Contains(character.characterId);

    private void Refresh()
    {
        if (character == null || SaveManager.Instance == null) return;

        SkillTreeProgressData progress = SaveManager.Instance.GetSkillTree(character.characterId);
        CharacterId id = character.characterId;

        string skillName = character.localizedSkillName != null && !character.localizedSkillName.IsEmpty ? character.localizedSkillName.GetLocalizedString() : character.skillName;
        titleText.text = string.Format(Loc("skilltree_title_format", "{0} Upgrade"), skillName);

        foreach (SkillTreeNode node in Nodes)
        {
            bool owned = SkillTreeCatalog.IsOwned(progress, node);
            bool available = SkillTreeCatalog.CanBuy(progress, node);

            nodeFrames[node].color = owned ? Owned : available ? Available : Locked;
            nodeStates[node].text = owned ? Loc("skilltree_owned", "Owned") : $"{SkillTreeCatalog.GetCost(node):N0} G";
            nodeSelect[node].enabled = node == selectedNode;
        }

        foreach ((Image line, SkillTreeNode to) in lines)
            line.color = SkillTreeCatalog.IsOwned(progress, to) ? Owned : new Color(1f, 1f, 1f, 0.25f);

        // 선택 노드 상세
        detailNameText.text = Loc(SkillTreeCatalog.GetNameKey(id, selectedNode), selectedNode.ToString());
        detailDescText.text = Loc(SkillTreeCatalog.GetDescKey(id, selectedNode), "");

        bool selectedOwned = SkillTreeCatalog.IsOwned(progress, selectedNode);
        bool canBuy = IsUnlockedCharacter && SkillTreeCatalog.CanBuy(progress, selectedNode);
        int cost = SkillTreeCatalog.GetCost(selectedNode);

        buyButton.interactable = canBuy && SaveManager.Instance.GetGold() >= cost;
        buyText.text = selectedOwned ? Loc("skilltree_owned", "Owned")
            : !IsUnlockedCharacter ? Loc("skilltree_character_locked", "Locked")
            : canBuy ? $"{Loc("skilltree_buy", "Upgrade")}  {cost:N0} G"
            : Loc("skilltree_locked", "Locked");

        int fee = SkillTreeCatalog.GetResetFee(progress);
        resetButton.interactable = progress != null && progress.tier > 0;
        resetText.text = progress != null && progress.tier > 0
            ? string.Format(Loc("skilltree_reset_format", "Reset (-{0:N0} G)"), fee)
            : Loc("skilltree_reset", "Reset");
    }

    private void Buy()
    {
        if (SaveManager.Instance.TryBuySkillNode(character.characterId, selectedNode))
        {
            SoundManager.Instance?.PlaySFX(SoundId.AugmentSelect);
            nodeFrames[selectedNode].transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 6, 0.5f);
            selectedNode = FirstUsefulNode();
            Refresh();
        }
        else
        {
            ToastMessageManager.Instance?.Show(Loc("common_not_enough_gold", "Not enough Gold."));
        }
    }

    private void ResetTree()
    {
        if (SaveManager.Instance.TryResetSkillTree(character.characterId))
        {
            ToastMessageManager.Instance?.Show(Loc("skilltree_reset_done", "Skill tree reset."));
            selectedNode = SkillTreeNode.Base;
            Refresh();
        }
        else
        {
            ToastMessageManager.Instance?.Show(Loc("common_not_enough_gold", "Not enough Gold."));
        }
    }

    // ───────────── 화면 구성 ─────────────

    private void Build()
    {
        style = CombatUiStyle.Get();
        var root = (RectTransform)transform;
        Stretch(root);

        // 어두운 배경 (누르면 닫힘)
        Image dim = NewImage("Dim", root, null, new Color(0.01f, 0.04f, 0.12f, 0.8f));
        Stretch(dim.rectTransform);
        dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);

        window = NewRect("Window", root);
        window.sizeDelta = new Vector2(920f, 1360f);
        Image frame = window.gameObject.AddComponent<Image>();
        frame.sprite = style != null ? style.popupFrame : null;
        frame.type = Image.Type.Sliced;
        if (frame.sprite == null) frame.color = new Color(0.05f, 0.15f, 0.35f);

        titleText = NewText("Title", window, 54f, Color.white, new Vector2(0f, 600f), new Vector2(760f, 90f));

        // 닫기
        Button close = NewButton("Close", window, style != null ? style.redButton : null, new Vector2(390f, 600f), new Vector2(90f, 90f), out _);
        close.onClick.AddListener(Close);
        if (style != null && style.closeIcon != null)
        {
            Image icon = NewImage("Icon", close.transform, style.closeIcon, Color.white);
            Stretch(icon.rectTransform);
            icon.rectTransform.offsetMin = new Vector2(16f, 20f);
            icon.rectTransform.offsetMax = new Vector2(-16f, -12f);
        }

        // 갈래 안내
        TMP_Text hint = NewText("Hint", window, 28f, new Color(0.75f, 0.85f, 1f), new Vector2(0f, 525f), new Vector2(800f, 50f));
        hint.text = Loc("skilltree_branch_hint", "Choose one branch. Reset to switch.");
        hint.fontStyle = FontStyles.Normal;

        // 노드 배치
        var positions = new Dictionary<SkillTreeNode, Vector2>
        {
            { SkillTreeNode.Base, new Vector2(0f, 360f) },
            { SkillTreeNode.A2, new Vector2(-215f, 100f) },
            { SkillTreeNode.B2, new Vector2(215f, 100f) },
            { SkillTreeNode.A3, new Vector2(-215f, -160f) },
            { SkillTreeNode.B3, new Vector2(215f, -160f) },
        };

        AddLine(window, positions[SkillTreeNode.Base], positions[SkillTreeNode.A2], SkillTreeNode.A2);
        AddLine(window, positions[SkillTreeNode.Base], positions[SkillTreeNode.B2], SkillTreeNode.B2);
        AddLine(window, positions[SkillTreeNode.A2], positions[SkillTreeNode.A3], SkillTreeNode.A3);
        AddLine(window, positions[SkillTreeNode.B2], positions[SkillTreeNode.B3], SkillTreeNode.B3);

        foreach (SkillTreeNode node in Nodes) AddNode(window, node, positions[node]);

        // 상세 설명
        Image detailBg = NewImage("Detail", window, style != null ? style.badge : null, new Color(0.02f, 0.07f, 0.18f, 0.85f));
        detailBg.type = Image.Type.Sliced;
        detailBg.rectTransform.anchoredPosition = new Vector2(0f, -390f);
        detailBg.rectTransform.sizeDelta = new Vector2(820f, 230f);

        detailNameText = NewText("Name", detailBg.transform, 38f, Owned, new Vector2(0f, 70f), new Vector2(780f, 60f));
        detailDescText = NewText("Desc", detailBg.transform, 30f, Color.white, new Vector2(0f, -25f), new Vector2(780f, 120f));
        detailDescText.fontStyle = FontStyles.Normal;
        detailDescText.textWrappingMode = TextWrappingModes.Normal;
        detailDescText.enableAutoSizing = true;
        detailDescText.fontSizeMin = 22f;
        detailDescText.fontSizeMax = 30f;

        // 버튼
        resetButton = NewButton("Reset", window, style != null ? style.redButton : null, new Vector2(-215f, -580f), new Vector2(380f, 110f), out resetText);
        resetButton.onClick.AddListener(ResetTree);
        buyButton = NewButton("Buy", window, style != null ? style.greenButton : null, new Vector2(215f, -580f), new Vector2(380f, 110f), out buyText);
        buyButton.onClick.AddListener(Buy);
    }

    private void AddNode(RectTransform parent, SkillTreeNode node, Vector2 position)
    {
        bool ultimate = SkillTreeCatalog.GetTier(node) == 3;

        Image select = NewImage("Select_" + node, parent, style != null ? style.badge : null, new Color(1f, 1f, 1f, 0.9f));
        select.type = Image.Type.Sliced;
        select.rectTransform.anchoredPosition = position;
        select.rectTransform.sizeDelta = new Vector2(330f, 210f);
        select.raycastTarget = false;
        nodeSelect[node] = select;

        Image frame = NewImage("Node_" + node, parent, style != null ? style.nodeFrame : null, Locked);
        frame.type = Image.Type.Sliced;
        frame.rectTransform.anchoredPosition = position;
        frame.rectTransform.sizeDelta = new Vector2(310f, 190f);
        Button button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = frame;
        button.onClick.AddListener(() => { selectedNode = node; Refresh(); });
        nodeFrames[node] = frame;

        TMP_Text name = NewText("Name", frame.transform, 32f, Color.white, new Vector2(0f, 25f), new Vector2(290f, 90f));
        name.text = (ultimate ? "★ " : "") + Loc(SkillTreeCatalog.GetNameKey(character != null ? character.characterId : CharacterId.Cat, node), node.ToString());
        name.textWrappingMode = TextWrappingModes.Normal;
        name.enableAutoSizing = true;
        name.fontSizeMin = 22f;
        name.fontSizeMax = 32f;
        nodeNames[node] = name;

        TMP_Text state = NewText("State", frame.transform, 26f, new Color(1f, 1f, 1f, 0.9f), new Vector2(0f, -55f), new Vector2(290f, 40f));
        nodeStates[node] = state;
    }

    private readonly Dictionary<SkillTreeNode, TMP_Text> nodeNames = new();

    private void AddLine(RectTransform parent, Vector2 from, Vector2 to, SkillTreeNode target)
    {
        Image line = NewImage("Line", parent, null, Color.white);
        line.raycastTarget = false;
        Vector2 delta = to - from;
        line.rectTransform.anchoredPosition = (from + to) * 0.5f;
        line.rectTransform.sizeDelta = new Vector2(10f, delta.magnitude);
        line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, delta));
        lines.Add((line, target));
    }

    private void LateUpdate()  //캐릭터가 바뀌면 노드 이름도 갱신
    {
        if (character == null) return;

        foreach (KeyValuePair<SkillTreeNode, TMP_Text> pair in nodeNames)
        {
            string text = (SkillTreeCatalog.GetTier(pair.Key) == 3 ? "★ " : "") + Loc(SkillTreeCatalog.GetNameKey(character.characterId, pair.Key), pair.Key.ToString());
            if (pair.Value.text != text) pair.Value.text = text;
        }
    }

    private Button NewButton(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size, out TMP_Text label)
    {
        Image image = NewImage(name, parent, sprite, Color.white);
        image.type = Image.Type.Sliced;
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = size;
        if (sprite == null) image.color = new Color(0.3f, 0.6f, 0.9f);

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        label = NewText("Label", image.transform, 34f, Color.white, new Vector2(0f, 4f), size - new Vector2(30f, 20f));
        label.enableAutoSizing = true;
        label.fontSizeMin = 20f;
        label.fontSizeMax = 34f;
        return button;
    }

    private TMP_Text NewText(string name, Transform parent, float size, Color color, Vector2 position, Vector2 rectSize)
    {
        RectTransform rect = NewRect(name, parent);
        rect.anchoredPosition = position;
        rect.sizeDelta = rectSize;

        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (style != null && style.boldFont != null) text.font = style.boldFont;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.outlineWidth = 0.18f;
        text.outlineColor = new Color32(10, 25, 60, 255);
        return text;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rect = NewRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static string Loc(string key, string fallback)
    {
        string value = LocalizationSettings.StringDatabase.GetLocalizedString("UI", key);
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    private void OnDestroy()
    {
        window?.DOKill();
        if (instance == this) instance = null;
    }
}
