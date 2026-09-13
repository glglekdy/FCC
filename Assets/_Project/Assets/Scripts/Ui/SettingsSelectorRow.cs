using TMPro;
using UnityEngine;

// 좌우 화살표로 항목을 넘기는 줄(`< 한국어 >`). 언어 · 대사 속도 · 화면 모드 · 해상도 · 프레임 제한이 쓴다.
//
// 드롭다운을 쓰지 않는 이유: 드롭다운은 열린 목록이 화면을 덮어 뒤쪽 설정이 가려지고, 방향키 조작과도
// 맞물리지 않는다. 선택지가 많아야 해상도 정도라 넘기는 방식이 더 빠르다.
//
// **Prefabs/UI/SettingsPanel.prefab 의 선택형 줄에 붙어 있습니다.**
public class SettingsSelectorRow : SettingsRowView {
    #region 인스펙터 변수

    [Header("연결")]
    public TMP_Text valueLabel; // 가운데 값 문구.
    public TMP_Text prevArrow; // 왼쪽 `<`.
    public TMP_Text nextArrow; // 오른쪽 `>`.

    [Header("색상")]
    public Color valueColor = UiTheme.TextBody;
    public Color focusedValueColor = UiTheme.TextHigh;
    public Color arrowColor = UiTheme.TextMuted;
    public Color arrowDisabledColor = UiTheme.TextDim; // 더 넘길 곳이 없는 쪽 화살표.

    [Header("동작")]
    public bool wrap; // 끝에서 반대쪽으로 넘어갈지. 해상도처럼 목록이 긴 항목은 꺼두는 편이 덜 헷갈린다.

    #endregion
    #region 표시

    public override void SetFocused(bool on) {
        base.SetFocused(on);
        if (valueLabel != null) valueLabel.color = on ? focusedValueColor : valueColor;
    }

    public override void Refresh() {
        int count = SettingsAccess.OptionCount(field);
        int index = Mathf.Clamp(SettingsAccess.GetInt(field), 0, count - 1);

        if (valueLabel != null) valueLabel.text = SettingsAccess.OptionLabel(field, index);

        // 양 끝에서 화살표를 흐리게 해, 더 넘길 곳이 없다는 것을 눌러보기 전에 알 수 있게 한다.
        if (prevArrow != null) prevArrow.color = (wrap || index > 0) ? arrowColor : arrowDisabledColor;
        if (nextArrow != null) nextArrow.color = (wrap || index < count - 1) ? arrowColor : arrowDisabledColor;
    }

    #endregion
    #region 입력

    public override bool Adjust(int dir) {
        int count = SettingsAccess.OptionCount(field);
        if (count <= 1) return false;

        int before = Mathf.Clamp(SettingsAccess.GetInt(field), 0, count - 1);
        int after = before + dir;

        if (wrap) after = (after % count + count) % count;
        else after = Mathf.Clamp(after, 0, count - 1);

        if (after == before) return false;

        SettingsAccess.SetInt(field, after);
        Refresh();
        return true;
    }

    #endregion
}
