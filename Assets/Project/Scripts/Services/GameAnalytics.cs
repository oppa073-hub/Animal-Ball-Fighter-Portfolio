using UnityEngine;

// 분석 이벤트 기록 창구. (Unity Analytics 연동은 UgsBootstrap에서 동의 확인 후 켜짐)
public static class GameAnalytics
{
    public static void Record(string eventName, params (string key, object value)[] parameters)
    {
        UgsBootstrap.RecordEvent(eventName, parameters);
    }
}
