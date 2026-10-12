using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 증강 선택 화면 카드 위에 이번 판에 가진 증강을 아이콘 한 줄로 표시 (@@@@ | #).
// 왼쪽: 일반/전설 증강 (같은 증강을 여러 번 고르면 "N강"), 오른쪽: 증강 스킬 (Lv.N). 아이콘을 누르면 이름 토스트.
// AugmentPanelUI가 소유하며 실행 중에 만든다. 데이터는 RunState.PickedAugments.
public class OwnedAugmentBar
{
    private const float MaxIconSize = 92f;
    private const float MinIconSize = 54f;
    private const float Spacing = 12f;
    private const float GroupGap = 44f;  //증강 | 스킬 사이
    private const float MaxWidth = 980f;

    private static readonly Color CommonColor = new Color(0.62f, 0.66f, 0.72f);
    private static readonly Color RareColor = new Color(0.3f, 0.6f, 1f);
    private static readonly Color EpicColor = new Color(0.72f, 0.4f, 1f);
    private static readonly Color LegendaryColor = new Color(1f, 0.6f, 0.15f);
    private static readonly Color SkillColor = new Color(0.2f, 0.85f, 0.75f);

    private class Entry
    {
        public AugmentData augment;
        public int count;
    }

    private readonly RectTransform root;
    private readonly CombatUiStyle style;

    public OwnedAugmentBar(Transform parent, float cardTopY)
    {
        style = CombatUiStyle.Get();

        GameObject go = new GameObject("OwnedAugmentBar", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;  //카드 가로 배치에서 제외

        root = (RectTransform)go.transform;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0f);
        root.anchoredPosition = new Vector2(0f, cardTopY + 24f);  //카드 바로 위
        root.sizeDelta = new Vector2(MaxWidth, MaxIconSize);
    }

    public void Refresh()
    {
        for (int i = root.childCount - 1; i >= 0; i--) Object.Destroy(root.GetChild(i).gameObject);

        List<Entry> augments = new List<Entry>();
        List<Entry> skills = new List<Entry>();

        foreach (AugmentData augment in RunState.PickedAugments)
        {
            if (augment == null) continue;

            List<Entry> group = augment.augmentType == AugmentType.AugmentSkill ? skills : augments;
            Entry entry = group.Find(e => e.augment == augment || (augment.augmentType != AugmentType.AugmentSkill && e.augment.augmentType == augment.augmentType));

            if (entry == null) group.Add(new Entry { augment = augment, count = 1 });
            else entry.count++;
        }

        int iconCount = augments.Count + skills.Count;
        root.gameObject.SetActive(iconCount > 0);
        if (iconCount == 0) return;

        bool hasBoth = augments.Count > 0 && skills.Count > 0;
        float gaps = (iconCount - 1) * Spacing + (hasBoth ? GroupGap - Spacing : 0f);
        float size = Mathf.Clamp((MaxWidth - gaps) / iconCount, MinIconSize, MaxIconSize);
        float totalWidth = iconCount * size + gaps;

        float x = -totalWidth * 0.5f + size * 0.5f;
        int index = 0;

        foreach (Entry entry in augments)
        {
            CreateIcon(entry, size, x, false, index++);
            x += size + Spacing;
        }

        if (hasBoth)  //구분선
        {
            x += GroupGap - Spacing;
            CreateDivider(x - size * 0.5f - GroupGap * 0.5f, size);
        }

        foreach (Entry entry in skills)
        {
            CreateIcon(entry, size, x, true, index++);
            x += size + Spacing;
        }
    }

    private void CreateIcon(Entry entry, float size, float x, bool isSkill, int index)
    {
        AugmentData augment = entry.augment;
        bool legendary = LegendaryEffects.IsLegendary(augment.augmentType);

        Image frame = NewImage("Owned_" + augment.name, root, style != null ? style.nodeFrame : null, isSkill ? SkillColor : GetGradeColor(augment.grade, legendary));
        frame.type = Image.Type.Sliced;
        frame.rectTransform.anchoredPosition = new Vector2(x, size * 0.5f);
        frame.rectTransform.sizeDelta = new Vector2(size, size);

        Button button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = frame;
        button.onClick.AddListener(() => ToastMessageManager.Instance?.Show(GetName(augment)));

        if (augment.icon != null)
        {
            Image icon = NewImage("Icon", frame.transform, augment.icon, Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Stretch(icon.rectTransform, size * 0.14f);
        }

        // 강화 단계: 일반 증강은 2번 이상 골랐을 때 "N강", 스킬은 항상 Lv.N
        int level = isSkill ? GetSkillLevel(entry.count) : entry.count;
        if (isSkill || level >= 2)
        {
            TMP_Text badge = NewText(frame.transform, size * 0.3f);
            badge.text = isSkill ? $"Lv.{level}" : string.Format(RunPopupUI.Loc("owned_augment_level_format", "x{0}"), level);
            badge.alignment = TextAlignmentOptions.BottomRight;
            Stretch(badge.rectTransform, 0f);
            badge.rectTransform.offsetMin = new Vector2(0f, -size * 0.12f);
            badge.rectTransform.offsetMax = new Vector2(size * 0.08f, 0f);
        }

        frame.transform.localScale = Vector3.zero;
        frame.transform.DOScale(1f, 0.2f).SetDelay(0.03f * index).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private void CreateDivider(float x, float size)
    {
        Image line = NewImage("Divider", root, null, new Color(1f, 1f, 1f, 0.45f));
        line.raycastTarget = false;
        line.rectTransform.anchoredPosition = new Vector2(x, size * 0.5f);
        line.rectTransform.sizeDelta = new Vector2(5f, size * 0.8f);
    }

    private static int GetSkillLevel(int pickCount)
    {
        int level = AugmentManager.Instance != null ? AugmentManager.Instance.AugmentSkillLevel : 0;
        return level > 0 ? level : pickCount;
    }

    private static string GetName(AugmentData augment)
    {
        if (augment.localizedAugmentName != null && !augment.localizedAugmentName.IsEmpty)
        {
            string localized = augment.localizedAugmentName.GetLocalizedString();
            if (!string.IsNullOrEmpty(localized)) return localized;
        }

        return augment.augmentName;
    }

    private static Color GetGradeColor(AugmentGrade grade, bool legendary)
    {
        if (legendary) return LegendaryColor;

        return grade switch
        {
            AugmentGrade.Rare => RareColor,
            AugmentGrade.Epic => EpicColor,
            AugmentGrade.Legendary => LegendaryColor,
            _ => CommonColor
        };
    }

    private TMP_Text NewText(Transform parent, float fontSize)
    {
        GameObject go = new GameObject("Badge", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (style != null && style.boldFont != null) text.font = style.boldFont;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(1f, 0.92f, 0.4f);
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.outlineWidth = 0.3f;
        text.outlineColor = new Color32(10, 10, 25, 255);
        return text;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    private static void Stretch(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }
}
