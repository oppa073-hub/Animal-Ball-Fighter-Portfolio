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
        }

        this.roomNumber.text = roomNumber.ToString();
    }
}
