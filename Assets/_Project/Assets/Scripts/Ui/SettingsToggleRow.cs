using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 켜고 끄는 줄. 데미지 수치 표시 · 수직 동기화가 쓴다.
//
// Unity 의 Toggle 대신 손잡이 위치를 직접 옮긴다. 마우스 클릭이 아니라 방향키로 넘기는 화면이고,
// 켜짐/꺼짐을 색과 위치 둘 다로 보여줘야 어두운 화면에서 구분이 되기 때문이다.
//
// **Prefabs/UI/SettingsPanel.prefab 의 토글 줄에 붙어 있습니다.**
public class SettingsToggleRow : SettingsRowView {
    #region 인스펙터 변수

    [Header("연결")]
    public RectTransform toggleTrack; // 손잡이가 움직이는 테두리 칸.
    public RectTransform toggleKnob; // 좌우로 옮겨지는 손잡이.
    public Image toggleKnobImage; // 손잡이 색. 켜짐일 때만 포인트 컬러가 된다.
    public TMP_Text stateLabel; // "켜짐" / "꺼짐".

    [Header("문구")]
    public string onText = "켜짐";
    public string offText = "꺼짐";

    [Header("색상")]
    public Color onKnobColor = UiTheme.AccentBright;
    public Color offKnobColor = UiTheme.TextDim;
    public Color stateColor = UiTheme.TextBody;
    public Color focusedStateColor = UiTheme.TextHigh;

    [Header("여백")]
    public float knobPadding = 4f; // 손잡이와 칸 사이 여백. 좌우 끝 위치를 이 값으로 계산한다.

    #endregion
    #region 표시

    public override void SetFocused(bool on) {
        base.SetFocused(on);
        if (stateLabel != null) stateLabel.color = on ? focusedStateColor : stateColor;
    }

    public override void Refresh() {
        bool on = SettingsAccess.GetBool(field);

        if (stateLabel != null) stateLabel.text = on ? onText : offText;
        if (toggleKnobImage != null) toggleKnobImage.color = on ? onKnobColor : offKnobColor;

        if (toggleKnob == null || toggleTrack == null) return;

        // 손잡이는 칸의 왼쪽 끝을 기준으로 놓여 있다. 켜지면 오른쪽 끝으로 보낸다.
        float travel = toggleTrack.rect.width - toggleKnob.rect.width - knobPadding * 2f;
        Vector2 p = toggleKnob.anchoredPosition;
        p.x = knobPadding + (on ? travel : 0f);
        toggleKnob.anchoredPosition = p;
    }

    #endregion
    #region 입력

    // 좌우 어느 쪽을 눌러도 상태가 바뀐다. 선택지가 둘뿐이라 방향을 따질 이유가 없다.
    public override bool Adjust(int dir) {
        SettingsAccess.SetBool(field, !SettingsAccess.GetBool(field));
        Refresh();
        return true;
    }

    // Enter 로도 넘길 수 있게 한다. 토글은 "확정" 할 것이 따로 없어 그대로 뒤집으면 된다.
    public override bool Submit() {
        return Adjust(1);
    }

    #endregion
}
