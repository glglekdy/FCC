using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// 막대를 좌우로 움직이는 줄. 볼륨 3종과 화면 흔들림이 쓴다.
//
// Unity 의 Slider 컴포넌트를 쓰지 않는 이유: 방향키와 마우스가 같은 눈금(step)을 공유해야 하는데, Slider 는
// 자체 선택(Selectable)·방향키 처리를 따로 들고 있어 설정창의 줄 이동과 부딪친다. 막대를 누르거나 끌면
// 커서 위치를 눈금에 맞춰 값으로 바꾸고, 채움 폭만 다시 그리면 된다.
//
// **Prefabs/UI/SettingsPanel.prefab 의 슬라이더 줄에 붙어 있습니다.**
public class SettingsSliderRow : SettingsRowView, IInitializePotentialDragHandler {
    #region 인스펙터 변수

    [Header("연결")]
    public RectTransform track; // 바닥 막대. 폭을 기준으로 채움과 손잡이 위치를 계산한다.
    public RectTransform fill; // 채워진 부분.
    public RectTransform knob; // 채움 끝에 붙는 손잡이.
    public TMP_Text valueLabel; // 오른쪽 수치.

    [Header("범위")]
    public int min; // 최솟값.
    public int max = 100; // 최댓값.
    public int step = 5; // 방향키 한 번에 움직이는 양. 마우스로 끌어도 이 눈금에 맞춰 떨어진다.
    public string suffix = ""; // 수치 뒤에 붙는 문구("%" 등). 비우면 숫자만 나온다.

    [Header("마우스")]
    public float trackSlack = 16f; // 막대 양 끝 바깥으로 이만큼까지 눌러도 막대를 누른 것으로 친다. 0과 최댓값을 끝까지 맞추기 쉽게.

    [Header("색상")]
    public Color valueColor = UiTheme.TextBody;
    public Color focusedValueColor = UiTheme.TextHigh;

    #endregion
    #region 상태

    bool dragging; // 막대 위에서 누르기 시작했는지. 라벨을 누르고 끌어오면 값이 튀지 않도록 막는다.

    #endregion
    #region 표시

    public override void SetFocused(bool on) {
        base.SetFocused(on);
        if (valueLabel != null) valueLabel.color = on ? focusedValueColor : valueColor;
    }

    public override void Refresh() {
        int value = Mathf.Clamp(SettingsAccess.GetInt(field), min, max);

        if (valueLabel != null) valueLabel.text = value + suffix;
        if (track == null) return;

        float ratio = max > min ? (value - min) / (float)(max - min) : 0f;
        float width = track.rect.width;

        // 채움은 왼쪽 끝에서 자라난다. 앵커를 건드리지 않고 폭만 바꿔야 프리팹에서 잡아둔 위치가 유지된다.
        if (fill != null) fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width * ratio);

        if (knob != null) {
            Vector2 p = knob.anchoredPosition;
            // 손잡이가 막대 밖으로 반쯤 튀어나가지 않도록 자기 폭의 절반만큼 안쪽으로 당긴다.
            p.x = width * ratio - knob.rect.width * 0.5f;
            knob.anchoredPosition = p;
        }
    }

    #endregion
    #region 입력

    public override bool Adjust(int dir) {
        int before = Mathf.Clamp(SettingsAccess.GetInt(field), min, max);
        return SetValue(before + dir * step);
    }

    bool SetValue(int value) {
        int before = Mathf.Clamp(SettingsAccess.GetInt(field), min, max);
        int after = Mathf.Clamp(value, min, max);

        if (after == before) return false;

        SettingsAccess.SetInt(field, after);
        Refresh();
        return true;
    }

    // 끌기 시작 문턱을 없앤다. 문턱이 있으면 누른 뒤 몇 픽셀 움직이는 동안 손잡이가 따라오지 않아 끈적하게 느껴진다.
    public void OnInitializePotentialDrag(PointerEventData eventData) {
        eventData.useDragThreshold = false;
    }

    protected override void PointerPressed(PointerEventData eventData) {
        dragging = IsWithinX(track, eventData, trackSlack);
        if (dragging) SetFromPointer(eventData);
    }

    // 막대 밖으로 커서가 벗어나도 계속 따라간다. 끝까지 밀어붙일 때 막대를 정확히 따라 긋는 사람은 없다.
    protected override void PointerDragged(PointerEventData eventData) {
        if (dragging) SetFromPointer(eventData);
    }

    void SetFromPointer(PointerEventData eventData) {
        if (track == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, eventData.position, eventData.pressEventCamera, out Vector2 p)) return;

        Rect r = track.rect;
        float exact = Mathf.Lerp(min, max, Mathf.InverseLerp(r.xMin, r.xMax, p.x));

        // 방향키와 같은 눈금에 떨어뜨린다. 마우스로 37 을 만들어 두면 방향키로는 다시 5 단위에 맞출 수 없게 된다.
        int value = step > 0 ? min + Mathf.RoundToInt((exact - min) / step) * step : Mathf.RoundToInt(exact);
        SetValue(value);
    }

    #endregion
}
