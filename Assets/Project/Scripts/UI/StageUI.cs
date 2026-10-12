using UnityEngine;
using TMPro;
using UnityEngine.Localization;

public class StageUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI roomType;
    [SerializeField] private TextMeshProUGUI roomNumber;

    [Header("Localization")]
    [SerializeField] private LocalizedString normalRoomText;
    [SerializeField] private LocalizedString eliteRoomText;
    [SerializeField] private LocalizedString eventRoomText;
    [SerializeField] private LocalizedString bossRoomText;

    private void OnEnable() => CombatTopBar.SetVisible(true);
    private void OnDisable() => CombatTopBar.SetVisible(false);

    public void Initialize(RoomType roomType, int roomNumber)
    {
        switch (roomType)
        {
            case RoomType.Normal:
                this.roomType.text = normalRoomText.GetLocalizedString();
                break;

            case RoomType.Elite:
                this.roomType.text = eliteRoomText.GetLocalizedString();
                break;

            case RoomType.Event:
                this.roomType.text = eventRoomText.GetLocalizedString();
                break;

            case RoomType.Boss:
                this.roomType.text = bossRoomText.GetLocalizedString();
                break;

            case RoomType.Shop:
                this.roomType.text = RunPopupUI.Loc("door_shop", "Shop");
                break;

            case RoomType.Rest:
                this.roomType.text = RunPopupUI.Loc("door_rest", "Campfire");
                break;
        }

        this.roomNumber.text = roomNumber.ToString();

        // 위쪽 가운데 통합 바(CombatTopBar)로 표시하고, 예전 오른쪽 위 글자(무한 모드 기록/골드 포함)는 숨김
        CombatTopBar topBar = CombatTopBar.GetOrCreate();
        if (topBar != null)
        {
            topBar.ShowRoom(roomType, roomNumber, this.roomType.text);
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }
    }
}
