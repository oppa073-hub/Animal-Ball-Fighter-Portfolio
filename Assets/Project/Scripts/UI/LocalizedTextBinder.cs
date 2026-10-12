using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

// 코드로 만든 글자를 언어 변경 때 다시 번역한다 (UI 표 키 + 기본값). 한 번 만들고 계속 남아 있는 로비 버튼 이름 등에 쓴다
[RequireComponent(typeof(TMP_Text))]
public class LocalizedTextBinder : MonoBehaviour
{
    private TMP_Text text;
    private string key;
    private string fallback;

    public static void Bind(TMP_Text target, string key, string fallback)
    {
        if (target == null) return;

        LocalizedTextBinder binder = target.GetComponent<LocalizedTextBinder>();
        if (binder == null) binder = target.gameObject.AddComponent<LocalizedTextBinder>();

        binder.text = target;
        binder.key = key;
        binder.fallback = fallback;
        binder.Refresh();
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
        Refresh();  //꺼져 있는 동안 언어가 바뀌었을 수 있음
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
    }

    private void HandleLocaleChanged(Locale locale) => Refresh();

    private void Refresh()
    {
        if (text == null || string.IsNullOrEmpty(key)) return;

        text.text = LobbyUiKit.Loc(key, fallback);
    }
}
