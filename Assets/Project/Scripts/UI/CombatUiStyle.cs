using TMPro;
using UnityEngine;

// 코드로 생성하는 전투 UI(리롤 버튼, 시너지 배너, 엘리트 특성 팝업)가 쓰는 스프라이트/폰트 모음.
// Assets/Resources/CombatUiStyle.asset 하나를 Resources.Load로 읽는다.
[CreateAssetMenu(menuName = "Game/Combat UI Style")]
public class CombatUiStyle : ScriptableObject
{
    public TMP_FontAsset boldFont;

    [Header("Reroll Button")]
    public Sprite skyButton;
    public Sprite refreshIcon;
    public Sprite adIcon;

    [Header("Synergy Banner")]
    public Sprite ribbon;
    public Sprite star;

    [Header("Elite Trait Popup")]
    public Sprite badge;

    [Header("Skill Tree Popup (로비)")]
    public Sprite popupFrame;
    public Sprite nodeFrame;
    public Sprite greenButton;
    public Sprite yellowButton;
    public Sprite redButton;
    public Sprite closeIcon;
    public Sprite lockIcon;

    [Header("Combat Juice (처치 연출)")]
    public Sprite coinSprite;
    public Material burstParticleMaterial;  //처치 시 터지는 별 조각
    public Material trailMaterial;          //빠를 때/피버 중 공 꼬리, 피격 섬광
    public Material hitFlashMaterial;       //맞은 적을 잠깐 덮는 흰색

    private static CombatUiStyle cached;
    private static bool loaded;

    public static CombatUiStyle Get()
    {
        if (!loaded)
        {
            cached = Resources.Load<CombatUiStyle>("CombatUiStyle");
            loaded = true;
        }

        return cached;
    }
}
