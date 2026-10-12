using UnityEngine;

// 외부 서비스 설정 (Resources/ServicesConfig 에셋). 값은 Unity 대시보드에서 만든 이름을 넣는다.
// 에셋이 없어도 기본값으로 동작한다.
[CreateAssetMenu(fileName = "ServicesConfig", menuName = "BounceSquad/Services Config")]
public class ServicesConfig : ScriptableObject
{
    public const string ResourcePath = "ServicesConfig";

    [Header("Unity Gaming Services")]
    [Tooltip("Unity 대시보드 Leaderboards에 만든 리더보드 ID")]
    public string endlessLeaderboardId = "endless_best_room";

    private static ServicesConfig cached;

    public static ServicesConfig Load()
    {
        if (cached == null) cached = Resources.Load<ServicesConfig>(ResourcePath);
        if (cached == null) cached = CreateInstance<ServicesConfig>();  //에셋이 없어도 기본값으로
        return cached;
    }
}
