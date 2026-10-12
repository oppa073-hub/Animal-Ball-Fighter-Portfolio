using UnityEngine;

//골드 추가 버튼에 붙인다. 버튼이 켜지면 골드 숫자를 왼쪽으로 밀어 가리지 않게 하고, 꺼지면 원래 자리로 돌린다
[ExecuteAlways]
public class GoldTextAvoidButton : MonoBehaviour
{
    [SerializeField] private RectTransform goldText;
    [SerializeField] private float rightOffsetWhenShown = -42f;   //버튼 왼쪽 끝(-36)보다 조금 더 안쪽
    [SerializeField] private float rightOffsetWhenHidden = -25f;  //원래 위치

    private void OnEnable() => ApplyRightOffset(rightOffsetWhenShown);

    private void OnDisable() => ApplyRightOffset(rightOffsetWhenHidden);

    private void ApplyRightOffset(float right)
    {
        if (goldText == null) return;

        goldText.offsetMax = new Vector2(right, goldText.offsetMax.y);
    }
}
