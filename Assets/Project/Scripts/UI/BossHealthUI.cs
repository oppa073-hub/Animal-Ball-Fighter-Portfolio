using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 보스 체력바. 씬에 있는 바(채움 이미지 + 이름표)에 실행 중 아래를 덧붙인다.
// - 체력 수치 (현재 / 최대)
// - 흰 잔상: 맞으면 흰 막대가 잠깐 남았다가 뒤따라 줄어듦
// - 분노선(70%) 눈금, 분노(2페이즈) 진입 후 채움이 붉게 맥동
// - 맞을 때 바가 살짝 흔들림
// 위쪽 통합 바(CombatTopBar)와 겹치지 않도록 그 아래로 내린다.
public class BossHealthUI : MonoBehaviour
{
    private const float RageThreshold = 0.7f;  //BossController의 2페이즈 기준과 같게
    private const float GhostDelay = 0.35f;
    private const float GhostSpeed = 0.6f;     //초당 비율
    private const float BarTopY = -205f;       //CombatTopBar(높이 86, 위 여백 22) + 보스 이름표 공간

    private static readonly Color RageColor = new Color(1f, 0.15f, 0.1f);

    private Health health;
    [SerializeField] private Image fillImg;
    [SerializeField] private TextMeshProUGUI bossName;

    private Image ghostImg;
    private TMP_Text hpText;
    private RectTransform rageMarker;
    private RectTransform rect;
    private Color baseFillColor;
    private bool built;
    private bool enraged;
    private float ghostRatio = 1f;
    private float ghostHoldTimer;
    private int lastHp;
    private int shownHp = -1, shownMaxHp = -1;  //값이 바뀔 때만 문자열을 새로 만든다
    private float rageShakeEndTime;

    public void Initialize(Health health, EnemyController enemy)
    {
        // 꺼진 상태에서 TextMeshPro를 만들면 초기화(Awake)가 안 돼 외곽선 설정 시 예외가 나므로 먼저 켠다
        // (UIManager.ShowBossHealth도 바로 뒤에 켜므로 동작은 같음)
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        Build();

        this.health = health;
        bossName.text = enemy.EnemyName;  //EnemyData의 번역 이름 (없으면 기본 이름)
        fillImg.fillAmount = 1f;
        fillImg.color = baseFillColor;

        ghostRatio = 1f;
        ghostImg.fillAmount = 1f;
        enraged = false;
        lastHp = health != null ? health.CurrentHp : 0;
        shownHp = shownMaxHp = -1;
        UpdateHpText();
    }

    private void OnEnable() => BossController.OnPhaseChanged += HandlePhaseChanged;
    private void OnDisable() => BossController.OnPhaseChanged -= HandlePhaseChanged;

    private void OnDestroy() => rect?.DOKill();

    private void HandlePhaseChanged(Transform boss, int phase)
    {
        if (phase < 2 || health == null || boss == null || boss.GetComponent<Health>() != health) return;

        enraged = true;
        rageShakeEndTime = Time.unscaledTime + 0.4f;
        rect.DOKill(true);
        rect.DOShakeAnchorPos(0.4f, 14f, 30, 90f, false, true).SetUpdate(true);
    }

    private void Update()
    {
        if (health == null) return;

        float ratio = health.HpRatio;
        fillImg.fillAmount = ratio;

        // 맞았을 때: 흔들림 + 잔상 대기
        if (health.CurrentHp < lastHp)
        {
            ghostHoldTimer = GhostDelay;
            if (Time.unscaledTime >= rageShakeEndTime)  //분노 흔들림 중에는 끊지 않음
            {
                rect.DOKill(true);
                rect.DOShakeAnchorPos(0.18f, 6f, 20, 90f, false, true).SetUpdate(true);
            }
        }
        lastHp = health.CurrentHp;

        // 흰 잔상이 잠시 뒤 실제 체력을 따라 내려감 (회복이면 바로 맞춤)
        if (ghostRatio < ratio) ghostRatio = ratio;
        else if (ghostHoldTimer > 0f) ghostHoldTimer -= Time.unscaledDeltaTime;
        else ghostRatio = Mathf.MoveTowards(ghostRatio, ratio, GhostSpeed * Time.unscaledDeltaTime);
        ghostImg.fillAmount = ghostRatio;

        if (enraged)  //분노: 붉게 맥동
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            fillImg.color = Color.Lerp(RageColor, new Color(1f, 0.5f, 0.3f), pulse);
        }

        if (rageMarker != null) rageMarker.gameObject.SetActive(!enraged);
        UpdateHpText();

        if (health.CurrentHp <= 0)
        {
            health = null;
        }
    }

    private void UpdateHpText()
    {
        if (hpText == null || health == null) return;

        int hp = Mathf.Max(0, health.CurrentHp);
        if (hp == shownHp && health.MaxHp == shownMaxHp) return;
        shownHp = hp;
        shownMaxHp = health.MaxHp;
        hpText.text = $"{hp:N0} / {shownMaxHp:N0}";
    }

    private void Build()
    {
        if (built) return;
        built = true;

        rect = (RectTransform)transform;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, BarTopY);
        baseFillColor = fillImg.color;

        CombatUiStyle style = CombatUiStyle.Get();

        // 흰 잔상 (채움 바로 뒤)
        GameObject ghostObject = new GameObject("BossHPGhost", typeof(RectTransform), typeof(Image));
        ghostObject.transform.SetParent(fillImg.transform.parent, false);
        ghostObject.transform.SetSiblingIndex(fillImg.transform.GetSiblingIndex());
        RectTransform ghostRect = (RectTransform)ghostObject.transform;
        RectTransform fillRect = fillImg.rectTransform;
        ghostRect.anchorMin = fillRect.anchorMin;
        ghostRect.anchorMax = fillRect.anchorMax;
        ghostRect.offsetMin = fillRect.offsetMin;
        ghostRect.offsetMax = fillRect.offsetMax;

        ghostImg = ghostObject.GetComponent<Image>();
        ghostImg.sprite = fillImg.sprite;
        ghostImg.type = Image.Type.Filled;
        ghostImg.fillMethod = fillImg.type == Image.Type.Filled ? fillImg.fillMethod : Image.FillMethod.Horizontal;
        ghostImg.fillOrigin = fillImg.fillOrigin;
        ghostImg.color = new Color(1f, 1f, 1f, 0.85f);
        ghostImg.raycastTarget = false;

        // 분노선 (70%)
        GameObject markerObject = new GameObject("RageMarker", typeof(RectTransform), typeof(Image));
        markerObject.transform.SetParent(transform, false);
        rageMarker = (RectTransform)markerObject.transform;
        rageMarker.anchorMin = rageMarker.anchorMax = new Vector2(RageThreshold, 0.5f);
        rageMarker.sizeDelta = new Vector2(6f, rect.sizeDelta.y + 14f);
        Image marker = markerObject.GetComponent<Image>();
        marker.color = new Color(1f, 0.85f, 0.3f, 0.95f);
        marker.raycastTarget = false;

        // 체력 수치 (바 안쪽 오른쪽)
        GameObject textObject = new GameObject("BossHPText", typeof(RectTransform));
        textObject.transform.SetParent(transform, false);
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, -4f);
        textRect.offsetMax = new Vector2(-14f, 4f);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        if (style != null && style.boldFont != null) text.font = style.boldFont;
        else if (bossName != null) text.font = bossName.font;
        text.fontSize = 28f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Right;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(20, 5, 5, 255);
        hpText = text;
    }
}
