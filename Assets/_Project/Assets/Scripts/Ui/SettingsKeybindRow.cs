using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 키보드·게임패드 할당을 보여주고 다시 지정하는 줄. 컨트롤 탭의 10줄이 쓴다.
//
// 실제 재지정은 InputBindings 가 Input System 의 PerformInteractiveRebinding 으로 한다. 이 줄은 대기 모양과 결과 표시만 맡고,
// 새 키는 GameSettings.Draft.bindingOverrides 에만 적힌다 — 「적용」을 눌러야 게임에 반영된다.
//
// 조작: Enter · 키보드 칸 클릭 → 키보드/마우스 입력 대기 / 게임패드 칸 클릭 → 패드 입력 대기
//       ESC → 대기 취소 / Backspace → 키보드 할당 해제
//
// **Prefabs/UI/SettingsPanel.prefab 의 컨트롤 탭 줄에 붙어 있습니다.**
public class SettingsKeybindRow : SettingsRowView {
    #region 인스펙터 변수

    [Header("역할")]
    public int actionIndex; // InputBindings.Entries 의 순번.

    [Header("연결")]
    public Image keyChip; // 키보드 칸 테두리.
    public TMP_Text keyLabel; // 키보드 키 이름.
    public Image padChip; // 게임패드 칸 테두리.
    public TMP_Text padLabel; // 패드 버튼 이름.

    [Header("색상")]
    public Color chipColor = UiTheme.PanelRaised;
    public Color chipWaitingColor = UiTheme.Panel; // 입력 대기 중인 칸은 배경을 한 단계 어둡게 하고 글자를 포인트 컬러로 바꾼다.
    public Color textColor = UiTheme.TextBody;
    public Color focusedTextColor = UiTheme.TextHigh;
    public Color waitingColor = UiTheme.AccentBright;
    public Color lockedTextColor = UiTheme.TextMuted; // 바꿀 수 없는 칸(일시정지의 ESC · 입력 에셋에 바인딩이 없는 칸).

    [Header("문구")]
    public string waitingText = "..."; // 입력 대기 중 키 칸에 보여줄 문구.
    public string emptyText = "-"; // 할당이 비었을 때. 폰트에 없는 기호를 쓰면 빈칸으로 보이므로 ASCII 로 둔다.

    #endregion
    #region 상태

    public bool IsWaiting { get; private set; }
    BindingDevice waitingDevice; // 어느 칸이 기다리는 중인지.

    public override bool HoldsFocus => IsWaiting;

    #endregion
    #region 유니티 라이프 사이클

    // 탭을 넘기거나 창을 닫으면 대기를 남기지 않는다. 남기면 보이지 않는 줄이 다음 키를 가져간다.
    void OnDisable() {
        CancelRebind();
    }

    #endregion
    #region 표시

    public override void SetFocused(bool on) {
        base.SetFocused(on);

        if (!on) CancelRebind(); // 포커스를 잃으면 대기 상태로 남겨두지 않는다.
        Refresh(); // 키 이름 글자색이 포커스를 따라간다.
    }

    public override void Refresh() {
        if (GameSettings.Draft == null) return;

        string overrides = GameSettings.Draft.bindingOverrides;
        DrawChip(BindingDevice.Keyboard, keyChip, keyLabel, overrides);
        DrawChip(BindingDevice.Gamepad, padChip, padLabel, overrides);
    }

    void DrawChip(BindingDevice device, Image chip, TMP_Text text, string overrides) {
        bool waiting = IsWaiting && waitingDevice == device;

        if (chip != null) chip.color = waiting ? chipWaitingColor : chipColor;
        if (text == null) return;

        if (waiting) {
            text.text = waitingText;
            text.color = waitingColor;
            return;
        }

        string name = InputBindings.DisplayName(actionIndex, device, overrides);
        text.text = string.IsNullOrEmpty(name) ? emptyText : name;

        bool editable = InputBindings.CanRebind(actionIndex, device, overrides);
        text.color = !editable ? lockedTextColor : IsFocused ? focusedTextColor : textColor;
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

        return BeginRebind(BindingDevice.Keyboard);
    }

    // Backspace. 키보드 칸만 비운다 — 패드 칸은 키보드로 줄을 고른 사람이 실수로 지울 일이 더 많다.
    public override bool Clear() {
        if (IsWaiting || GameSettings.Draft == null) return false;

        string overrides = GameSettings.Draft.bindingOverrides;
        if (!InputBindings.CanRebind(actionIndex, BindingDevice.Keyboard, overrides)) return false;

        GameSettings.Draft.bindingOverrides = InputBindings.ClearBinding(actionIndex, BindingDevice.Keyboard, overrides);
        Refresh();
        return true;
    }

    // 칸을 눌렀을 때만 대기에 들어간다. 동작 이름 쪽을 누른 것은 줄을 고른 것일 뿐이다.
    protected override void PointerClicked(PointerEventData eventData) {
        if (IsInside(keyChip, eventData)) BeginRebind(BindingDevice.Keyboard);
        else if (IsInside(padChip, eventData)) BeginRebind(BindingDevice.Gamepad);
    }

    static bool IsInside(Image chip, PointerEventData eventData) {
        return chip != null
            && RectTransformUtility.RectangleContainsScreenPoint(chip.rectTransform, eventData.position, eventData.pressEventCamera);
    }

    bool BeginRebind(BindingDevice device) {
        if (GameSettings.Draft == null) return false;

        string overrides = GameSettings.Draft.bindingOverrides;
        if (!InputBindings.CanRebind(actionIndex, device, overrides)) return false;

        // 이 줄이 다른 칸을 기다리던 중이면 먼저 정리한다. 상태를 비운 뒤에 취소해야 취소 콜백이 되돌아와도 아무 일이 없다.
        CancelRebind();

        IsWaiting = true;
        waitingDevice = device;

        if (!InputBindings.StartRebind(actionIndex, device, overrides, HandleRebound, HandleRebindCanceled)) {
            IsWaiting = false;
        }

        Refresh();
        return IsWaiting;
    }

    void HandleRebound(string overrides) {
        IsWaiting = false;
        GameSettings.Draft.bindingOverrides = overrides;

        // 겹친 키를 맞바꿨으면 다른 줄의 키도 바뀌었다. 컨트롤 탭 전체를 다시 그린다.
        SettingsPanelView panel = GetComponentInParent<SettingsPanelView>();
        if (panel != null) panel.RefreshAllRows();
        else Refresh();
    }

    // ESC 로 빠져나왔거나, 다른 줄이 대기를 시작해 이 줄의 대기가 밀려난 경우.
    void HandleRebindCanceled() {
        if (!IsWaiting) return;

        IsWaiting = false;
        Refresh();
    }

    public void CancelRebind() {
        if (!IsWaiting) return;

        IsWaiting = false; // 먼저 비운다. 아래 취소가 HandleRebindCanceled 를 곧바로 부른다.
        InputBindings.CancelRebind();

        // 꺼지는 중(OnDisable)에는 다시 그리지 않는다. 그리면 창이 닫히는 순간 편집용 사본을 새로 만들어 남긴다.
        if (isActiveAndEnabled) Refresh();
    }

    #endregion
}
