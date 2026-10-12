using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 스킬 버튼 (캐릭터 스킬 / 증강 스킬). 실행 중에 모양과 위치를 다시 잡는다.
// - 배치: 오른쪽 아래 세로. 캐릭터 스킬(아래, 크게) / 증강 스킬(위, 작게). 아래쪽 가로 체력바와 겹치지 않음
// - 모양: 아이콘(네모)에 맞춘 둥근 사각형 테두리 (캐릭터 금색 / 증강 하늘색)
// - 쿨타임: 어두운 아이콘 위로 밝은 아이콘이 시계 방향으로 차오름(fillImg) + 가운데 남은 초
// - 준비 완료 순간: 통통 + 테두리가 퍼지며 번쩍, 준비된 동안 테두리 맥동
// - 증강 스킬: 아래 가장자리에 걸친 알약 라벨 "Lv.N" (자동 발동이면 "자동 · Lv.N")
// - 누를 때 반응, 지금 쓸 수 없으면 흐리게
// 씬 연결(fillImg/backImg/chargeCountText, 버튼의 OnSkillButtonClicked)은 그대로 쓴다. backImg는 버튼 터치 영역으로만 남기고 투명하게.
public class SkillSlotUI : MonoBehaviour
{
    private static readonly Color CharacterColor = new Color(1f, 0.78f, 0.28f);
    private static readonly Color AugmentColor = new Color(0.35f, 0.8f, 1f);
    private static readonly Color DimIconColor = new Color(0.32f, 0.35f, 0.45f, 1f);

    private const float CharacterSize = 150f;
    private const float AugmentSize = 150f;     //캐릭터 스킬과 같은 크기
    private const float RightMargin = 105f;     //화면 오른쪽에서 버튼 중심까지
    private const float CharacterY = 215f;      //화면 아래에서 버튼 중심까지
    private const float AugmentY = 400f;
    private const float IconPadding = 9f;       //테두리 안쪽 여백

    private ISkillUIData skill;
    [SerializeField] private Image fillImg;
    [SerializeField] private Image backImg;
    [SerializeField] private TMP_Text chargeCountText;  //비어 있으면 충전형 스킬일 때 실행 중에 생성

    private ISkillChargeUIData chargeData;
    private ISkillDetailUIData detailData;
    private int shownChargeCount = -1;

    private bool isAugment;
    private bool built;
    private Color accent = CharacterColor;
    private Image frame;
    private Image dimIcon;
    private Image burst;
    private TMP_Text cooldownText;
    private RectTransform levelPill;
    private TMP_Text levelText;
    private CanvasGroup group;
    private bool wasReady = true;
    private int shownSeconds = -1;
    private string shownLabel;
    private int labelLevel = -1;  //레벨/자동 여부가 바뀔 때만 라벨 문자열을 새로 만든다 (번역 조회 포함)
    private bool labelIsAuto;
    private string cachedLabel = "";

    public bool IsEmpty => skill == null;

    public void Initialize(ISkillUIData skill)
    {
        this.skill = skill;

        fillImg.sprite = skill.SkillIcon;
        backImg.sprite = skill.SkillIcon;
        fillImg.fillAmount = 1f;

        chargeData = skill as ISkillChargeUIData;
        if (chargeData != null && chargeData.MaxCharges <= 1) chargeData = null;

        detailData = skill as ISkillDetailUIData;
        isAugment = skill is AugmentSkillRuntime;
        accent = isAugment ? AugmentColor : CharacterColor;

        if (chargeCountText != null) chargeCountText.gameObject.SetActive(chargeData != null);
        shownChargeCount = -1;
        shownSeconds = -1;
        shownLabel = null;
        labelLevel = -1;
        wasReady = true;

        ApplyLayout();
        if (gameObject.activeInHierarchy) BuildDecorations();  //꺼져 있으면 OnEnable에서 (꺼진 채로 TMP를 만들면 예외)
        else if (built) RefreshStaticVisuals();
    }

    private void OnEnable()
    {
        if (skill != null) BuildDecorations();
    }

    private void OnDestroy()
    {
        transform.DOKill();
        if (burst != null) { burst.rectTransform.DOKill(); burst.DOKill(); }
    }

    // ───────── 배치 ─────────

    private void ApplyLayout()
    {
        RectTransform self = (RectTransform)transform;
        float size = isAugment ? AugmentSize : CharacterSize;

        self.anchorMin = self.anchorMax = new Vector2(1f, 0f);
        self.pivot = new Vector2(0.5f, 0.5f);
        self.sizeDelta = new Vector2(size, size);
        self.anchoredPosition = new Vector2(-RightMargin, isAugment ? AugmentY : CharacterY);

        // 루트 이미지는 터치 영역으로만 (투명). 실제 아이콘은 자식들이 그림
        backImg.color = new Color(1f, 1f, 1f, 0f);

        if (fillImg.transform != transform)
        {
            Stretch(fillImg.rectTransform, IconPadding);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Radial360;
            fillImg.fillOrigin = (int)Image.Origin360.Top;
            fillImg.fillClockwise = true;
            fillImg.color = Color.white;
            fillImg.preserveAspect = false;
        }
    }

    // ───────── 매 프레임 ─────────

    private void Update()
    {
        if (skill == null) return;

        float ratio = skill.CooldownRatio;
        fillImg.fillAmount = ratio;

        if (chargeData != null && chargeCountText != null)
        {
            int count = chargeData.CurrentCharges;
            if (count != shownChargeCount)
            {
                shownChargeCount = count;
                chargeCountText.text = count.ToString();
            }
        }

        if (!built) return;

        bool ready = ratio >= 0.999f;

        // 남은 초
        float remain = detailData != null ? detailData.RemainCooldown : 0f;
        int seconds = ready || remain <= 0.05f ? -1 : Mathf.CeilToInt(remain);
        if (seconds != shownSeconds)
        {
            shownSeconds = seconds;
            cooldownText.text = seconds > 0 ? seconds.ToString() : "";
        }

        // 준비 완료 순간 번쩍 / 준비된 동안 테두리 맥동 (쿨타임 중엔 어둡게)
        if (ready && !wasReady) PlayReadyBurst();
        wasReady = ready;

        float glow = ready ? 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 4f) : 0.45f;
        frame.color = new Color(accent.r * glow, accent.g * glow, accent.b * glow, 1f);

        // 증강 스킬 레벨 라벨
        string label = BuildLevelLabel();
        if (label != shownLabel)
        {
            shownLabel = label;
            levelPill.gameObject.SetActive(!string.IsNullOrEmpty(label));
            levelText.text = label;
        }

        // 지금 쓸 수 없는 상태면 흐리게 (자동 스킬은 그대로)
        bool usable = GameManager.Instance != null && GameManager.Instance.currentState == GameState.Playing && !TimeFreeze.IsActive;
        bool isAuto = detailData != null && detailData.IsAutoSkill;
        group.alpha = Mathf.MoveTowards(group.alpha, usable || isAuto ? 1f : 0.6f, Time.unscaledDeltaTime * 4f);
    }

    private string BuildLevelLabel()
    {
        int level = detailData != null ? detailData.SkillLevel : 0;
        if (level <= 0) return "";

        bool isAuto = detailData.IsAutoSkill;
        if (level == labelLevel && isAuto == labelIsAuto) return cachedLabel;

        labelLevel = level;
        labelIsAuto = isAuto;
        return cachedLabel = isAuto ? string.Format(RunPopupUI.Loc("skill_auto_level_format", "AUTO · Lv.{0}"), level) : $"Lv.{level}";
    }

    public void Clear()
    {
        skill = null;
        chargeData = null;
        detailData = null;
        if (chargeCountText != null) chargeCountText.gameObject.SetActive(false);
    }

    public void OnSkillButtonClicked()
    {
        if (skill == null) return;

        bool usable = GameManager.Instance != null && GameManager.Instance.currentState == GameState.Playing && !TimeFreeze.IsActive;
        bool ready = skill.CooldownRatio >= 0.999f && usable;  //발사 전 등 실제로 안 나가는 상태면 "아직 못 씀" 반응
        bool isAuto = detailData != null && detailData.IsAutoSkill;

        transform.DOKill(true);
        if (ready && !isAuto) transform.DOPunchScale(Vector3.one * -0.1f, 0.2f, 6, 0.5f).SetUpdate(true);  //눌림
        else ((RectTransform)transform).DOShakeAnchorPos(0.25f, new Vector2(8f, 0f), 20, 0f, false, true).SetUpdate(true);  //아직 못 씀

        skill.TryUseSkill();
    }

    private void PlayReadyBurst()  //준비 완료: 통통 + 테두리가 퍼지며 사라짐
    {
        transform.DOKill(true);
        transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 6, 0.6f).SetUpdate(true);

        RectTransform rect = burst.rectTransform;
        rect.DOKill();
        burst.DOKill();
        rect.localScale = Vector3.one;
        burst.color = new Color(1f, 1f, 1f, 0.9f);
        rect.DOScale(1.35f, 0.4f).SetEase(Ease.OutCubic).SetUpdate(true);
        burst.DOFade(0f, 0.4f).SetUpdate(true);
    }

    // ───────── 꾸밈 요소 (한 번만 생성, 켜진 상태에서) ─────────

    private void BuildDecorations()
    {
        if (chargeData != null) EnsureChargeCountText();  //충전형(토끼 등)은 남은 횟수 숫자
        if (chargeCountText != null) chargeCountText.gameObject.SetActive(chargeData != null);

        if (!built)
        {
            built = true;

            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();

            CombatUiStyle style = CombatUiStyle.Get();
            Sprite frameSprite = style != null ? style.nodeFrame : null;

            // 맨 뒤부터: 테두리 → 어두운 아이콘 → (fillImg 밝은 아이콘) → 번쩍임 → 남은 초 → 레벨 라벨
            frame = NewImage("Frame", frameSprite, accent);
            frame.type = Image.Type.Sliced;
            Stretch(frame.rectTransform, 0f);
            frame.transform.SetSiblingIndex(0);

            dimIcon = NewImage("DimIcon", null, DimIconColor);
            Stretch(dimIcon.rectTransform, IconPadding);
            dimIcon.transform.SetSiblingIndex(1);

            burst = NewImage("ReadyBurst", frameSprite, new Color(1f, 1f, 1f, 0f));
            burst.type = Image.Type.Sliced;
            Stretch(burst.rectTransform, 0f);

            cooldownText = NewText("CooldownText", 64f, Color.white);
            Stretch(cooldownText.rectTransform, 0f);
            cooldownText.enableAutoSizing = true;
            cooldownText.fontSizeMin = 24f;
            cooldownText.fontSizeMax = 64f;

            levelPill = NewRect("LevelPill");
            levelPill.anchorMin = levelPill.anchorMax = new Vector2(0.5f, 0f);
            levelPill.pivot = new Vector2(0.5f, 0.5f);
            levelPill.anchoredPosition = new Vector2(0f, 2f);  //아래 가장자리에 걸치게
            Image pillBack = levelPill.gameObject.AddComponent<Image>();
            pillBack.sprite = style != null ? style.badge : null;
            pillBack.type = Image.Type.Sliced;
            pillBack.color = new Color(0.04f, 0.08f, 0.2f, 0.95f);
            pillBack.raycastTarget = false;

            levelText = NewText("Text", 24f, AugmentColor, levelPill);
            Stretch(levelText.rectTransform, 0f);
            levelText.rectTransform.offsetMin = new Vector2(10f, 0f);
            levelText.rectTransform.offsetMax = new Vector2(-10f, 0f);
            levelText.enableAutoSizing = true;
            levelText.fontSizeMin = 14f;
            levelText.fontSizeMax = 24f;
            levelPill.gameObject.SetActive(false);
        }

        RefreshStaticVisuals();
        if (chargeCountText != null) chargeCountText.transform.SetAsLastSibling();  //충전 횟수는 맨 위에
    }

    private void RefreshStaticVisuals()  //스킬이 바뀔 때 (아이콘/색/크기)
    {
        if (!built) return;

        dimIcon.sprite = skill != null ? skill.SkillIcon : null;
        float size = isAugment ? AugmentSize : CharacterSize;
        levelPill.sizeDelta = new Vector2(size * 0.78f, 34f);
        levelText.color = accent;
        frame.color = accent;
    }

    private void EnsureChargeCountText()  //버튼 오른쪽 아래에 남은 사용 횟수 숫자
    {
        if (chargeCountText != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        TMP_Text sample = canvas != null ? canvas.GetComponentInChildren<TMP_Text>(true) : null;  //같은 UI 폰트 사용

        GameObject textObject = new GameObject("ChargeCountText", typeof(RectTransform));
        textObject.transform.SetParent(transform, false);

        RectTransform rect = (RectTransform)textObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();

        if (sample != null) text.font = sample.font;

        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.BottomRight;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 72f;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(0, 0, 0, 255);

        chargeCountText = text;
    }

    private RectTransform NewRect(string name, Transform parent = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent != null ? parent : transform, false);
        return (RectTransform)go.transform;
    }

    private Image NewImage(string name, Sprite sprite, Color color)
    {
        Image image = NewRect(name).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TMP_Text NewText(string name, float size, Color color, Transform parent = null)
    {
        TextMeshProUGUI text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        CombatUiStyle style = CombatUiStyle.Get();
        if (style != null && style.boldFont != null) text.font = style.boldFont;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(0, 0, 0, 255);
        return text;
    }

    private static void Stretch(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }
}
