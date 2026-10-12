using System;
using UnityEngine;

// 앱 업데이트 없이 바꿀 수 있는 밸런스 값 (Unity Remote Config에서 받아옴, 기본값은 전부 1 = 지금 밸런스 그대로).
// 마지막으로 받은 값은 PlayerPrefs에 보관해 오프라인으로 켜도 같은 값을 쓴다.
public static class RemoteTuning
{
    private const string CacheKey = "REMOTE_TUNING_CACHE";

    [Serializable]
    public class Values
    {
        public float enemyHpMultiplier = 1f;      //일반/엘리트 적 체력
        public float bossHpMultiplier = 1f;       //보스 체력
        public float bossCooldownMultiplier = 1f; //보스 패턴 쿨타임
        public float stageGoldMultiplier = 1f;    //스테이지 클리어 골드
    }

    private static Values current;

    public static Values Current
    {
        get
        {
            if (current == null) LoadCache();
            return current;
        }
    }

    public static float GetEnemyHpMultiplier(bool isBoss) => Sanitize(isBoss ? Current.bossHpMultiplier : Current.enemyHpMultiplier);
    public static float BossCooldownMultiplier => Sanitize(Current.bossCooldownMultiplier);
    public static float StageGoldMultiplier => Sanitize(Current.stageGoldMultiplier);

    public static void Apply(Values values)  //원격 값 수신 후
    {
        if (values == null) return;

        current = values;
        PlayerPrefs.SetString(CacheKey, JsonUtility.ToJson(values));
        PlayerPrefs.Save();
    }

    private static void LoadCache()
    {
        current = new Values();

        try
        {
            string json = PlayerPrefs.GetString(CacheKey, "");
            if (!string.IsNullOrEmpty(json)) JsonUtility.FromJsonOverwrite(json, current);
        }
        catch (Exception)
        {
            current = new Values();
        }
    }

    private static float Sanitize(float value)  //잘못된 값이 들어와도 게임이 망가지지 않게
    {
        if (float.IsNaN(value) || value <= 0f) return 1f;
        return Mathf.Clamp(value, 0.25f, 4f);
    }
}
