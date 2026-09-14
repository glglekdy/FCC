using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 키보드·게임패드 할당을 보여주는 줄. 컨트롤 탭의 10줄이 쓴다.
//
// **지금은 표시와 값 보관까지만 합니다.** 실제 재지정(Input System 의 PerformInteractiveRebinding)은
// 프로젝트에 아직 없어서, Enter 를 누르거나 키보드 칸을 클릭하면 입력 대기 모양만 켜고 다음 입력에서 빠져나온다.
// 리바인딩을 붙일 때는 BeginRebind() 안의 표시해둔 자리에서 시작하면 된다.
//
// **Prefabs/UI/SettingsPanel.prefab 의 컨트롤 탭 줄에 붙어 있습니다.**
public class SettingsKeybindRow : SettingsRowView {
    #region 인스펙터 변수

    [Header("역할")]
    public int actionIndex; // SettingsData.keyboardBindings / padBindings 의 순번.

    [Header("연결")]
    public Image keyChip; // 키보드 칸 테두리.
    public TMP_Text keyLabel; // 키보드 키 이름.
    public Image padChip; // 게임패드 칸 테두리.
    public TMP_Text padLabel; // 패드 버튼 이름.

    [Header("색상")]
    public Color chipColor = UiTheme.PanelRaised;
    public Color chipWaitingColor = UiTheme.Panel; // 입력 대기 중인 칸은 배경을 한 단계 어둡게 하고 테두리를 포인트 컬러로 바꾼다.
    public Color textColor = UiTheme.TextBody;
    public Color focusedTextColor = UiTheme.TextHigh;
    public Color waitingColor = UiTheme.AccentBright;

    [Header("문구")]
    public string waitingText = "..."; // 입력 대기 중 키 칸에 보여줄 문구.
    public string emptyText = "-"; // 할당이 비었을 때. 폰트에 없는 기호를 쓰면 빈칸으로 보이므로 ASCII 로 둔다.

    #endregion
    #region 상태

    public bool IsWaiting { get; private set; }

    public override bool HoldsFocus => IsWaiting;

    #endregion
    #region 표시

    public override void SetFocused(bool on) {
        base.SetFocused(on);

        if (!IsWaiting) {
            if (keyLabel != null) keyLabel.color = on ? focusedTextColor : textColor;
            if (padLabel != null) padLabel.color = on ? focusedTextColor : textColor;
        }

        if (!on) CancelRebind(); // 포커스를 잃으면 대기 상태로 남겨두지 않는다.
    }

    public override void Refresh() {
        if (keyLabel != null && !IsWaiting) keyLabel.text = Read(GameSettings.Draft.keyboardBindings, emptyText);
        if (padLabel != null) padLabel.text = Read(GameSettings.Draft.padBindings, emptyText);

        if (keyChip != null) keyChip.color = IsWaiting ? chipWaitingColor : chipColor;
        if (padChip != null) padChip.color = chipColor;

        if (IsWaiting) {
            if (keyLabel != null) {
                keyLabel.text = waitingText;
                keyLabel.color = waitingColor;
            }
        }
    }

    string Read(string[] list, string fallback) {
        if (list == null || actionIndex < 0 || actionIndex >= list.Length) return fallback;
        return string.IsNullOrEmpty(list[actionIndex]) ? fallback : list[actionIndex];
    }

    #endregion
    #region 입력

    // 좌우 입력은 받지 않는다. 키 할당은 "왼쪽/오른쪽으로 넘길 수 있는 목록" 이 아니기 때문이다.
    public override bool Adjust(int dir) {
        return false;
    }

    public override bool Submit() {
        if (IsWaiting) {
            CancelRebind();
            return true;
        }

        BeginRebind();
        return true;
    }

    // 키보드 칸을 눌렀을 때만 Enter 와 같게 동작한다. 동작 이름 쪽을 누른 것은 줄을 고른 것일 뿐이다.
    // 패드 칸은 아직 대기 상태가 키보드 칸에만 있어 받지 않는다.
    protected override void PointerClicked(PointerEventData eventData) {
        if (keyChip == null) return;
        if (!RectTransformUtility.RectangleContainsScreenPoint(keyChip.rectTransform, eventData.position, eventData.pressEventCamera)) return;

        Submit();
    }

    void BeginRebind() {
        IsWaiting = true;
        Refresh();

        // **여기서 Input System 의 PerformInteractiveRebinding 을 시작하면 됩니다.**
        // 끝나면 GameSettings.Draft.keyboardBindings[actionIndex] 에 키 이름을 넣고 CancelRebind() 를 부르면
        // 나머지 표시는 알아서 맞춰집니다.
    }

    public void CancelRebind() {
        if (!IsWaiting) return;

        IsWaiting = false;
        Refresh();

        if (keyLabel != null) keyLabel.color = IsFocused ? focusedTextColor : textColor;
    }

    #endregion
}
