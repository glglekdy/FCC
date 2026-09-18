using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 설정창 한 화면. 탭 전환 · 줄 사이 이동 · 값 변경 · 적용/취소/기본값을 맡는다.
//
// 값은 GameSettings.Draft 만 만지고 실제 시스템에는 손대지 않는다. 「적용」을 눌러야 확정되는 구조라
// (해상도처럼 되돌리기 어려운 항목이 섞여 있다) 여기서 즉시 반영해버리면 「취소」가 의미를 잃는다.
//
// 조작은 전부 Time.unscaledDeltaTime 기준이다. 일시정지 중(timeScale 0)에 열리는 화면이기 때문이다.
//
// 키보드와 마우스를 함께 받는다. 줄 위의 마우스는 각 줄(SettingsRowView)이 받고, 탭 클릭은 여기서 받는다 —
// 탭 문구에는 클릭을 받는 면이 따로 없어, 화면 바탕(Screen)까지 내려온 클릭을 탭 칸 위치와 대조한다.
//
// **Prefabs/UI/SettingsPanel.prefab 의 루트에 붙어 있습니다.**
public class SettingsPanelView : MonoBehaviour, IPointerClickHandler {
    #region 인스펙터 변수

    [Serializable]
    public class Tab {
        public string name; // 하이어라키에서 알아보기 위한 이름. 화면에는 쓰이지 않는다.
        public TMP_Text label; // 탭바의 문구.
        public GameObject content; // 이 탭이 켜질 때만 보이는 내용 묶음.
        public RectTransform selectedBackground; // 골라진 탭 뒤에 깔리는 면. 탭 칸과 크기가 같아 마우스 클릭 범위로도 쓴다.
        public RectTransform selectedMarker; // 탭 아래 3px 포인트 선.
        public TMP_Text hint; // 화면 맨 아래 안내 문구. 탭마다 내용이 다르다.
    }

    [Header("연결")]
    public Tab[] tabs; // 일반 · 컨트롤 · 그래픽.
    public Button resetButton;
    public Button cancelButton;
    public Button applyButton;
    public InputActionAsset inputActions; // **Client.inputactions 를 넣으세요.** 키 재지정 줄이 이 에셋의 바인딩을 읽고 바꿉니다.

    [Header("색상")]
    public Color tabColor = UiTheme.TextBody;
    public Color selectedTabColor = UiTheme.TextHigh;

    [Header("조작")]
    public float holdDelay = 0.4f; // 방향키를 누르고 있을 때 연속 이동이 시작되기까지의 시간.
    public float holdInterval = 0.08f; // 연속 이동 간격.

    #endregion
    #region 상태

    int tabIndex;
    int rowIndex;
    readonly List<SettingsRowView> rows = new List<SettingsRowView>();

    float holdTimer;
    int holdDir;

    // 화면이 닫힐 때 알리는 통로. 여는 쪽(일시정지 메뉴·메인 로비)이 여기 붙어 뒷정리를 한다.
    public event Action OnClosed;

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        // 연결이 빠지면 NullReference 가 한참 뒤 엉뚱한 곳에서 터진다. 무엇이 빠졌는지 여기서 이름으로 알린다.
        if (tabs == null || tabs.Length == 0) Debug.LogError("[SettingsPanel] tabs 가 비어 있습니다.", this);
        if (applyButton == null) Debug.LogError("[SettingsPanel] applyButton 이 연결되지 않았습니다.", this);
        if (cancelButton == null) Debug.LogError("[SettingsPanel] cancelButton 이 연결되지 않았습니다.", this);
        if (resetButton == null) Debug.LogError("[SettingsPanel] resetButton 이 연결되지 않았습니다.", this);
        if (inputActions == null) Debug.LogError("[SettingsPanel] inputActions 가 연결되지 않았습니다. 컨트롤 탭의 키를 바꿀 수 없습니다.", this);

        // 메인 메뉴처럼 플레이어(PlayerInput)가 없는 씬에서도 컨트롤 탭이 키를 읽을 수 있도록 에셋을 알린다.
        InputBindings.Register(inputActions);

        if (applyButton != null) applyButton.onClick.AddListener(ApplyAndClose);
        if (cancelButton != null) cancelButton.onClick.AddListener(CancelAndClose);
        if (resetButton != null) resetButton.onClick.AddListener(ResetToDefaults);

        // 꺼진 탭의 줄까지 여기서 한 번에 물린다. 탭을 넘길 때마다 물리면 같은 줄에 콜백이 겹쳐 쌓인다.
        if (tabs != null) {
            foreach (Tab t in tabs) {
                if (t.content == null) continue;
                foreach (SettingsRowView row in t.content.GetComponentsInChildren<SettingsRowView>(true)) {
                    row.Bind(OnRowHovered, OnRowPressed);
                }
            }
        }
    }

    void OnEnable() {
        GameSettings.Load();
        GameSettings.Cancel(); // 열 때마다 Draft 를 확정값으로 되돌린다. 지난번에 취소하고 닫은 값이 남아 있으면 안 된다.

        ClearUiSelection();
        SelectTab(0);
    }

    // 편집용 사본은 창이 열려 있는 동안만 쓴다. 다음에 열 때는 확정값에서 새로 만든다.
    void OnDisable() {
        InputBindings.ReleaseEditCopy();
    }

    void Update() {
        // 키 입력을 기다리는 동안에는 화면 조작을 전부 쉰다. 누른 키는 새 할당이지 탭 전환(Q/E) · 줄 이동(W/S)이 아니다.
        // Input System 이 대기 중에 누른 키를 삼키긴 하지만, 패드 칸을 기다리는 중에 누른 키보드처럼 대기와 무관한 입력도 있다.
        if (InputBindings.IsRebinding) return;

        HandleNavigation();
        HandleShortcuts();
    }

    #endregion
    #region 탭

    public void SelectTab(int index) {
        if (tabs == null || tabs.Length == 0) return;

        tabIndex = Mathf.Clamp(index, 0, tabs.Length - 1);

        for (int i = 0; i < tabs.Length; i++) {
            bool on = i == tabIndex;
            Tab t = tabs[i];

            if (t.content != null) t.content.SetActive(on);
            if (t.label != null) t.label.color = on ? selectedTabColor : tabColor;
            if (t.selectedBackground != null) t.selectedBackground.gameObject.SetActive(on);
            if (t.selectedMarker != null) t.selectedMarker.gameObject.SetActive(on);
            if (t.hint != null) t.hint.gameObject.SetActive(on);
        }

        CollectRows();
        SetRow(0);
    }

    // 탭 칸 위를 눌렀는지 본다. 꺼져 있는 탭의 selectedBackground 도 RectTransform 크기는 그대로라 대조에 쓸 수 있다.
    public void OnPointerClick(PointerEventData eventData) {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (tabs == null) return;

        for (int i = 0; i < tabs.Length; i++) {
            RectTransform slot = tabs[i].selectedBackground;
            if (slot == null) continue;
            if (!RectTransformUtility.RectangleContainsScreenPoint(slot, eventData.position, eventData.pressEventCamera)) continue;

            if (i != tabIndex) SelectTab(i);
            return;
        }
    }

    // 켜져 있는 탭의 줄만 모은다. 꺼진 탭의 줄까지 섞이면 방향키가 안 보이는 줄로 내려간다.
    void CollectRows() {
        rows.Clear();

        Tab t = tabs[tabIndex];
        if (t.content == null) return;

        foreach (SettingsRowView row in t.content.GetComponentsInChildren<SettingsRowView>(false)) {
            rows.Add(row);
            row.Refresh();
            row.SetFocused(false);
        }
    }

    #endregion
    #region 줄 이동

    void SetRow(int index) {
        if (rows.Count == 0) { rowIndex = 0; return; }

        rowIndex = (index % rows.Count + rows.Count) % rows.Count;

        for (int i = 0; i < rows.Count; i++) rows[i].SetFocused(i == rowIndex);
    }

    // 커서가 스친 줄로 포커스를 옮긴다. 키 입력을 기다리는 줄이 있으면 넘기지 않는다 — 커서를 치우다 대기가 풀리면 안 된다.
    void OnRowHovered(SettingsRowView row) {
        if (rows.Count > 0 && rows[rowIndex].HoldsFocus) return;
        FocusRow(row);
    }

    // 누른 줄은 무조건 넘긴다. 사용자가 분명히 그 줄을 고른 것이다.
    void OnRowPressed(SettingsRowView row) {
        FocusRow(row);
    }

    void FocusRow(SettingsRowView row) {
        int index = rows.IndexOf(row);
        if (index < 0 || index == rowIndex) return; // 같은 줄에 다시 SetFocused 를 부르면 키 재지정 대기가 흔들린다.

        SetRow(index);
        UiAudio.PlayHover();
    }

    void HandleNavigation() {
        if (rows.Count == 0) return;

        if (WasPressed(Key.UpArrow, Key.W)) { SetRow(rowIndex - 1); UiAudio.PlayHover(); }
        if (WasPressed(Key.DownArrow, Key.S)) { SetRow(rowIndex + 1); UiAudio.PlayHover(); }

        // 좌우는 누르고 있으면 연속으로 먹힌다. 볼륨을 0에서 100까지 한 칸씩 스무 번 누르게 할 수는 없다.
        int dir = 0;
        if (IsHeld(Key.LeftArrow, Key.A)) dir = -1;
        else if (IsHeld(Key.RightArrow, Key.D)) dir = 1;

        if (dir == 0) {
            holdDir = 0;
            holdTimer = 0f;
            return;
        }

        if (dir != holdDir) {
            holdDir = dir;
            holdTimer = holdDelay;
            rows[rowIndex].Adjust(dir);
            return;
        }

        holdTimer -= Time.unscaledDeltaTime;
        if (holdTimer > 0f) return;

        holdTimer = holdInterval;
        rows[rowIndex].Adjust(dir);
    }

    void HandleShortcuts() {
        if (WasPressed(Key.Enter, Key.NumpadEnter) && rows.Count > 0) { rows[rowIndex].Submit(); UiAudio.PlayClick(); }
        if (WasPressed(Key.Backspace) && rows.Count > 0) rows[rowIndex].Clear();

        // 탭 전환은 Q/E 로. 좌우 방향키는 값 변경에 이미 쓰이고 있어 겹칠 수 없다.
        if (WasPressed(Key.Q)) SelectTab(tabIndex - 1 < 0 ? tabs.Length - 1 : tabIndex - 1);
        if (WasPressed(Key.E)) SelectTab((tabIndex + 1) % tabs.Length);

        if (WasPressed(Key.Escape)) CancelAndClose();
    }

    // 프로젝트가 새 Input System 전용(activeInputHandler = 1)이라 구형 UnityEngine.Input 은 예외를 던진다.
    // 설정창은 키 하나하나를 직접 보는 화면이라 InputAction 을 따로 파지 않고 Keyboard 를 바로 읽는다.
    static bool WasPressed(params Key[] keys) {
        Keyboard kb = Keyboard.current;
        if (kb == null) return false;

        foreach (Key k in keys) if (kb[k].wasPressedThisFrame) return true;
        return false;
    }

    static bool IsHeld(params Key[] keys) {
        Keyboard kb = Keyboard.current;
        if (kb == null) return false;

        foreach (Key k in keys) if (kb[k].isPressed) return true;
        return false;
    }

    #endregion
    #region 푸터 버튼

    public void ApplyAndClose() {
        UiAudio.PlayClick();
        GameSettings.Apply();
        Close();
    }

    public void CancelAndClose() {
        UiAudio.PlayClick();
        GameSettings.Cancel();
        Close();
    }

    public void ResetToDefaults() {
        UiAudio.PlayClick();
        InputBindings.CancelRebind();
        GameSettings.ResetToDefaults();
        RefreshAllRows();

        SetRow(rowIndex);
        ClearUiSelection();
    }

    // 여러 줄에 걸쳐 값이 바뀌었을 때(기본값 복원 · 키 맞바꾸기). 지금 탭만 다시 그리면 다른 탭이 옛 값을 들고 있게 된다.
    public void RefreshAllRows() {
        if (tabs == null) return;

        foreach (Tab t in tabs) {
            if (t.content == null) continue;
            foreach (SettingsRowView row in t.content.GetComponentsInChildren<SettingsRowView>(true)) row.Refresh();
        }
    }

    void Close() {
        ClearUiSelection();
        gameObject.SetActive(false);
        if (OnClosed != null) OnClosed();
    }

    // 버튼을 마우스로 누르면 EventSystem 이 그 버튼을 "선택됨" 으로 붙잡아 둔다. 그대로 두면 다음에 Enter/Space 를
    // 누를 때 줄의 Submit 과 함께 그 버튼이 한 번 더 눌린다(「기본값 복원」이 두 번 실행되는 식). 설정창은 줄 이동을
    // 직접 처리하므로 EventSystem 의 선택은 늘 비워둔다. 열 때도 비우는 이유는, 뒤에 깔린 일시정지 메뉴의 버튼이
    // 선택된 채로 남아 있으면 같은 일이 생기기 때문이다.
    static void ClearUiSelection() {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    #endregion
}
