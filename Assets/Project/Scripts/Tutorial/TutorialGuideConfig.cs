using TMPro;
using UnityEngine;

// 튜토리얼 안내 UI가 쓰는 그림/글꼴 (Resources/TutorialGuideConfig 에셋 하나)
[CreateAssetMenu(fileName = "TutorialGuideConfig", menuName = "BounceSquad/Tutorial Guide Config")]
public class TutorialGuideConfig : ScriptableObject
{
    public const string ResourcePath = "TutorialGuideConfig";

    public Sprite catSprite;      //안내 고양이 얼굴
    public Sprite circleSprite;   //원형 마스크/테두리
    public Sprite ringSprite;     //강조 고리
    public Sprite bubbleSprite;   //말풍선 (왼쪽 꼬리, 9-slice)
    public Sprite handSprite;     //가리키는 손
    public TMP_FontAsset font;
    public Material fontMaterial;
    public Color textColor = new Color(0.16f, 0.2f, 0.32f);

    private static TutorialGuideConfig cached;

    public static TutorialGuideConfig Load()
    {
        if (cached == null) cached = Resources.Load<TutorialGuideConfig>(ResourcePath);
        return cached;
    }
}
