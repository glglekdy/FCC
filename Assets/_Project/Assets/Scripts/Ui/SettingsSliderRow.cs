using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 막대를 좌우로 움직이는 줄. 볼륨 3종과 화면 흔들림이 쓴다.
//
// Unity 의 Slider 컴포넌트를 쓰지 않는 이유: 이 설정창은 마우스가 아니라 방향키로 움직이는 화면이고,
// 눈금(step)이 정해져 있어 Slider 의 드래그·핸들 처리가 전부 군더더기가 된다. 채움 폭만 바꾸면 끝이다.
//
// **Prefabs/UI/SettingsPanel.prefab 의 슬라이더 줄에 붙어 있습니다.**
public class SettingsSliderRow : SettingsRowView {
    #region 인스펙터 변수

    [Header("연결")]
    public RectTransform track; // 바닥 막대. 폭을 기준으로 채움과 손잡이 위치를 계산한다.
    public RectTransform fill; // 채워진 부분.
    public RectTransform knob; // 채움 끝에 붙는 손잡이.
    public TMP_Text valueLabel; // 오른쪽 수치.

    [Header("범위")]
    public int min; // 최솟값.
    public int max = 100; // 최댓값.
    public int step = 5; // 방향키 한 번에 움직이는 양.
    public string suffix = ""; // 수치 뒤에 붙는 문구("%" 등). 비우면 숫자만 나온다.

    [Header("색상")]
    public Color valueColor = UiTheme.TextBody;
    public Color focusedValueColor = UiTheme.TextHigh;

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
        int after = Mathf.Clamp(before + dir * step, min, max);

        if (after == before) return false;

        SettingsAccess.SetInt(field, after);
        Refresh();
        return true;
    }

    #endregion
}
