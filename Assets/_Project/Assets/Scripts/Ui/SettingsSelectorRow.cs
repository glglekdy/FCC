using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// 좌우 화살표로 항목을 넘기는 줄(`< 한국어 >`). 언어 · 화면 모드 · 해상도 · 프레임 제한이 쓴다.
//
// 드롭다운을 쓰지 않는 이유: 드롭다운은 열린 목록이 화면을 덮어 뒤쪽 설정이 가려지고, 방향키 조작과도
// 맞물리지 않는다. 선택지가 많아야 해상도 정도라 넘기는 방식이 더 빠르다.
//
// 마우스로는 `<` 부터 `>` 까지의 값 칸을 반으로 갈라, 왼쪽 절반을 누르면 이전 · 오른쪽 절반을 누르면 다음으로 넘긴다.
// 화살표 글자만 받게 하면 20px 짜리 과녁이라 맞히기 어렵다.
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

    [Header("마우스")]
    public float arrowSlack = 24f; // 화살표 바깥으로 이만큼까지 눌러도 값 칸을 누른 것으로 친다.

    #endregion
    #region 상태

    static readonly Vector3[] corners = new Vector3[4]; // GetWorldCorners 버퍼. 클릭마다 새 배열을 만들지 않으려고 재사용한다.

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

    protected override void PointerClicked(PointerEventData eventData) {
        if (prevArrow == null || nextArrow == null) return;

        // 두 화살표는 줄의 서로 다른 자식이라, 둘 다 월드 좌표로 옮겨야 한 줄 위에서 비교할 수 있다.
        prevArrow.rectTransform.GetWorldCorners(corners);
        float left = corners[0].x;
        nextArrow.rectTransform.GetWorldCorners(corners);
        float right = corners[2].x;

        RectTransform self = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(self, eventData.position, eventData.pressEventCamera, out Vector3 world)) return;

        // slack 은 캔버스 기준 px 이라 월드 배율을 곱해 맞춘다. 창 크기에 따라 캔버스 배율이 바뀌기 때문이다.
        float slack = arrowSlack * self.lossyScale.x;
        if (world.x < left - slack || world.x > right + slack) return; // 라벨 쪽을 누른 것은 포커스만 옮기고 값은 두지 않는다.

        Adjust(world.x < (left + right) * 0.5f ? -1 : 1);
    }

    #endregion
}
