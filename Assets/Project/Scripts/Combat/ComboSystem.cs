using DG.Tweening;
using TMPro;
using UnityEngine;

// 콤보: 마지막 적 타격 후 comboWindow초 안에 다시 적을 맞히면 이어진다. 콤보가 오를수록 충돌 피해 증가(상한 있음).
// 화면 상단 중앙에 "N COMBO"를 띄운다. 스테이지 씬에서 처음 쓸 때 생성된다(씬 로컬).
public class ComboSystem : MonoBehaviour
{
    private const float ComboWindow = 1.2f;  //게임 시간 기준 (산데비스탄 감속 중엔 실제로 더 길어짐)
    private const float DamageBonusPerCombo = 0.05f;
    private const float MaxDamageBonus = 0.5f;
    private const int ShowFromCombo = 2;

    private static ComboSystem instance;

    private int combo;
    private float lastHitTime = -999f;

    private TMP_Text comboText;
    private bool hudSearchFailed;
    private RectTransform comboRect;
    private CanvasGroup comboGroup;
    private Tween fadeTween;

    public static int CurrentCombo => instance != null ? instance.combo : 0;

    // 플레이어가 적을 맞힐 때 호출. 이번 타격에 곱할 피해 배율을 돌려준다.
    public static float RegisterHit()
    {
        if (instance == null) instance = new GameObject("ComboSystem").AddComponent<ComboSystem>();
        return instance.RegisterHitInternal();
    }

    public static void ResetCombo()  //방 시작 등
    {
        if (instance != null) instance.Break();
    }

    private float RegisterHitInternal()
    {
        combo = Time.time - lastHitTime <= ComboWindow ? combo + 1 : 1;
        lastHitTime = Time.time;

        GameEvents.RaiseComboChanged(combo);
        ShowCombo();

        return 1f + Mathf.Min(MaxDamageBonus, (combo - 1) * DamageBonusPerCombo);
    }

    private void Update()
    {
        if (combo > 0 && Time.time - lastHitTime > ComboWindow) Break();
    }

    private void Break()
    {
        if (combo == 0) return;

        combo = 0;
        GameEvents.RaiseComboChanged(0);
        HideCombo();
    }

    private void ShowCombo()
    {
        if (combo < ShowFromCombo) return;
        if (!EnsureText()) return;

        comboText.text = $"{combo} COMBO";
        comboText.color = GetComboColor(combo);

        fadeTween?.Kill();
        comboGroup.alpha = 1f;

        comboRect.DOKill();
        comboRect.localScale = Vector3.one * 1.35f;
        comboRect.DOScale(1f, 0.18f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private void HideCombo()
    {
        if (comboGroup == null) return;

        fadeTween?.Kill();
        fadeTween = comboGroup.DOFade(0f, 0.3f).SetUpdate(true);
    }

    private static Color GetComboColor(int value)
    {
        if (value >= 15) return new Color(1f, 0.3f, 0.3f);
        if (value >= 10) return new Color(1f, 0.55f, 0.15f);
        if (value >= 5) return new Color(1f, 0.85f, 0.2f);
        return Color.white;
    }

    private bool EnsureText()  //HUD 캔버스에 콤보 텍스트를 만든다 (씬 수정 없이)
    {
        if (comboText != null) return true;
        if (hudSearchFailed) return false;  //매 타격마다 다시 찾지 않음

        Canvas hud = FindHudCanvas();
        if (hud == null)
        {
            hudSearchFailed = true;
            Debug.LogWarning("[Combo] HUD 캔버스를 찾지 못해 콤보 텍스트를 표시하지 않습니다.");
            return false;
        }

        GameObject textObject = new GameObject("ComboText", typeof(RectTransform), typeof(CanvasGroup));
        textObject.transform.SetParent(SafeAreaFitter.GetContentRoot(hud), false);

        comboRect = (RectTransform)textObject.transform;
        comboRect.anchorMin = comboRect.anchorMax = new Vector2(0.5f, 0.74f);
        comboRect.sizeDelta = new Vector2(600f, 120f);

        comboGroup = textObject.GetComponent<CanvasGroup>();
        comboGroup.blocksRaycasts = false;
        comboGroup.interactable = false;

        TMP_Text sample = hud.GetComponentInChildren<TMP_Text>(true);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        if (sample != null) text.font = sample.font;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 72f;
        text.fontStyle = FontStyles.Bold | FontStyles.Italic;
        text.outlineWidth = 0.22f;
        text.outlineColor = new Color32(0, 0, 0, 255);

        comboText = text;
        comboGroup.alpha = 0f;
        return true;
    }

    internal static Canvas FindHudCanvas()  //스테이지 HUD (메인 카메라를 쓰는 Screen Space Camera 캔버스, 정렬 0)
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || !canvas.gameObject.activeInHierarchy) continue;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == Camera.main && canvas.sortingOrder == 0) return canvas;
        }

        return null;
    }

    private void OnDestroy()
    {
        fadeTween?.Kill();
        if (comboRect != null) comboRect.DOKill();
        if (instance == this) instance = null;
    }
}
