using System.Collections.Generic;
using UnityEngine;

// 무한 모드 맵 교체용 맵 목록 (Resources/EndlessMapSet).
// 에디터 메뉴 Tools > BounceSquad > 무한 모드 맵 만들기 가 일반 스테이지 씬의 Props를 프리팹으로 떼어 채운다.
[CreateAssetMenu(fileName = "EndlessMapSet", menuName = "BounceSquad/Endless Map Set")]
public class EndlessMapSet : ScriptableObject
{
    public const string ResourcePath = "EndlessMapSet";

    [System.Serializable]
    public class MapEntry
    {
        public StageData stage;          //이름 표시용
        public GameObject mapPrefab;     //스테이지 씬의 Props 복사본
        public Color lightColor = Color.white;
        public float lightIntensity = 1f;
    }

    public List<MapEntry> maps = new List<MapEntry>();

    public static EndlessMapSet Load() => Resources.Load<EndlessMapSet>(ResourcePath);
}
