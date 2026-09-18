using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.UI;

// 기록 선택 화면. 메인 로비의 [새로 시작]과 [이어하기]가 같은 화면을 모드만 바꿔 연다.
//
// 새로 시작 — 빈자리를 고르면 곧바로 그 자리로 새로 시작하고, 기록이 있는 자리를 고르면 덮어쓸지 한 번 더 묻는다.
// 불러오기 — 기록이 있는 자리를 고르면 그 기록을 연다. 빈자리는 골라도 아무 일도 없다.
// 화면을 둘로 나누지 않은 이유: 자리 목록 · 지우기 · 확인 창이 전부 같아서, 나누면 한쪽만 고쳐지는 일이 생긴다.
//
// Delete 키로는 기록을 지운다(역시 확인을 거친다). 둘 다 되돌릴 수 없는 동작이라 확인 창은 매번 「취소」에 포커스를 두고 열린다.
//
// 세이브 파일은 SaveManager 가 들고 있고 이 화면은 자리별로 읽고 지우기만 한다. 씬 전환은 여는 쪽(MainLobbyView)이
// OnNewGameRequested · OnLoadRequested 를 받아서 한다 — 페이더 연출과 연타 방지를 로비가 이미 쥐고 있기 때문이다.
//
// 키보드는 Keyboard 를 직접 읽는다. 프로젝트가 새 Input System 전용이라 구형 Input 은 예외를 던지고,
// EventSystem 내비게이션은 씬마다 설정이 달라 같은 결과를 보장하지 못한다 (SettingsPanelView 와 같은 이유).
//
// **Prefabs/UI/SaveSlotSelect.prefab 의 루트에 붙어 있습니다.** 줄 하나는 SaveSlotRow.prefab(SaveSlotRowView).
public class SaveSlotSelectView : MonoBehaviour {
    #region 인스펙터 변수

    public enum Mode {
        NewGame, // [새로 시작]
        Load, // [이어하기]
    }

    [Serializable]
    public class DisplayName {
        public string id; // 씬 이름 또는 거울(SaveMirror)의 id.
        public string name; // 화면에 보일 이름.
    }

    [Header("연결 — 제목")]
    public TMP_Text titleLabel; // 모드에 따라 문구가 바뀐다.
    public TMP_Text subtitleLabel;

    [Header("연결 — 목록")]
    public SaveSlotRowView rowPrefab; // 자리 하나. SaveManager.slotCount 만큼 찍어낸다.
    public RectTransform slotList; // 줄이 들어갈 자리. VerticalLayoutGroup 이 줄 간격을 잡는다.
    public Button backButton;

    [Header("연결 — 확인 창")]
    public GameObject confirmRoot; // 뒤를 덮는 막과 창을 함께 켜고 끈다.
    public TMP_Text confirmTitle;
    public TMP_Text confirmSummary;
    public Button confirmCancelButton;
    public Button confirmAcceptButton;
    public Image confirmCancelBackground;
    public Image confirmAcceptBackground;
    public GameObject confirmCancelFocus; // 포커스가 온 버튼에만 켜지는 포인트 테두리.
    public GameObject confirmAcceptFocus;
    public TMP_Text confirmCancelLabel;
    public TMP_Text confirmAcceptLabel;

    [Header("표시 이름")]
    // 세이브에는 씬 이름 · 거울 id 같은 내부 값만 적힌다. 여기서 사람이 읽을 이름으로 바꾼다.
    // 표에 없는 값은 아래 문구로 대신한다 — 내부 id 가 그대로 플레이어 화면에 나가지 않게 하기 위함이다.
    public DisplayName[] sceneNames = {
        new DisplayName { id = "First", name = "프롤로그" },
        new DisplayName { id = "CoreScene", name = "서커스 극장" },
    };
    public DisplayName[] checkpointNames = new DisplayName[0]; // **거울을 새로 놓으면 그 id 와 이름을 여기에 추가하세요.**

    [Header("문구 — 모드")]
    public LocalizedString newGameTitle = new("Ui", "saveslot.new_game_title");
    public LocalizedString newGameSubtitle = new("Ui", "saveslot.new_game_subtitle");
    public LocalizedString loadTitle = new("Ui", "saveslot.load_title");
    public LocalizedString loadSubtitle = new("Ui", "saveslot.load_subtitle");

    [Header("문구")]
    public LocalizedString unknownRegionText = new("Ui", "saveslot.unknown_region"); // sceneNames 에 없는 씬.
    public LocalizedString checkpointFormat = new("Ui", "saveslot.checkpoint_format"); // {0} = 거울 이름.
    public LocalizedString unnamedCheckpointText = new("Ui", "saveslot.unnamed_checkpoint"); // checkpointNames 에 없는 거울.
    public LocalizedString quickSaveText = new("Ui", "saveslot.quick_save"); // 거울이 아닌 개발용 F5 저장(checkpointId 가 비어 있다).
    public LocalizedString overwriteTitleFormat = new("Ui", "saveslot.overwrite_title_format"); // {0} = 화면에 보이는 번호.
    public LocalizedString deleteTitleFormat = new("Ui", "saveslot.delete_title_format");
    public LocalizedString overwriteAcceptText = new("Ui", "saveslot.overwrite");
    public LocalizedString deleteAcceptText = new("Ui", "saveslot.delete");
    public LocalizedString summaryFormat = new("Ui", "saveslot.summary_format"); // {0} 지역 {1} 현재 {2} 최대 {3} 조각.
    public LocalizedString cancelLabelText = new("Ui", "common.cancel");

    [Header("색상")]
    public Color buttonColor = UiTheme.Panel;
    public Color focusedButtonColor = UiTheme.PanelRaised;
    public Color cancelLabelColor = UiTheme.TextBody;
    public Color focusedCancelLabelColor = UiTheme.TextHigh;
    public Color dangerLabelColor = UiTheme.AccentBright; // 되돌릴 수 없는 쪽 버튼. 포커스와 무관하게 적색 글자로 둔다.

    #endregion
    #region 상태

    enum ConfirmMode { None, Overwrite, Delete }

    Mode mode = Mode.NewGame;

    readonly List<SaveSlotRowView> rows = new List<SaveSlotRowView>();
    int rowIndex;

    ConfirmMode confirmMode;
    bool acceptFocused; // 확인 창에서 「덮어쓰기/지우기」 쪽에 포커스가 있는지.

    // 이 화면(또는 확인 창)을 연 프레임. 여는 데 쓴 Enter 가 같은 프레임에 한 번 더 먹혀
    // 곧바로 자리가 골라지거나 확인 창이 닫히는 것을 막는다.
    int openedFrame = -1;

    bool isBusy; // 새로 시작을 요청한 뒤. 씬이 넘어가는 동안 입력을 받지 않는다.
    bool isReady;

    static bool warnedMissingManager; // SaveManager 가 없다는 경고를 한 번만 찍는다. 열 때마다 찍으면 콘솔이 덮인다.

    // 자리를 골라 새로 시작할 때. 인자는 자리 번호(0부터). 여는 쪽이 씬을 넘긴다.
    public event Action<int> OnNewGameRequested;

    // 기록을 골라 불러올 때. 자리는 이미 SaveManager 에 정해진 뒤다. 여는 쪽이 기록의 씬을 열고 상태를 되돌린다.
    public event Action<int, SaveData> OnLoadRequested;

    // 뒤로 가기로 닫혔을 때.
    public event Action OnClosed;

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        isReady = ValidateReferences();
        if (!isReady) return;

        backButton.onClick.AddListener(Close);
        confirmCancelButton.onClick.AddListener(CloseConfirm);
        confirmAcceptButton.onClick.AddListener(AcceptConfirm);

        // 취소 버튼 글자는 색만 코드가 바꾸고 문구는 프리팹에 직접 적혀 있었다. 여기서 한 번만 덮어쓴다.
        confirmCancelLabel.text = LocalizationText.Resolve(cancelLabelText, confirmCancelLabel.text);
    }

    void OnEnable() {
        if (!isReady) return;

        openedFrame = Time.frameCount;
        isBusy = false;

        bool load = mode == Mode.Load;
        titleLabel.text = load ? LocalizationText.Resolve(loadTitle, "기억 불러오기") : LocalizationText.Resolve(newGameTitle, "기억 선택");
        subtitleLabel.text = load
            ? LocalizationText.Resolve(loadSubtitle, "이어서 할 기억을 고르세요.")
            : LocalizationText.Resolve(newGameSubtitle, "새로 시작할 자리를 고르세요. 기억이 있는 자리를 고르면 덮어씁니다.");

        RebuildRows();
        CloseConfirm();

        // 새로 시작은 실수로 덮어쓰지 않도록 빈자리에서, 불러오기는 마지막으로 하던 기록에서 시작한다.
        FocusRow(load ? LastPlayedIndex() : FirstEmptyIndex());
    }

    // 모드를 정하고 연다. SetActive(true) 로 바로 켜면 지난번 모드가 그대로 쓰인다.
    public void Open(Mode openMode) {
        mode = openMode;

        // 이미 떠 있으면 OnEnable 이 다시 불리지 않으므로 끄고 켜서 새 모드로 다시 그린다.
        if (gameObject.activeSelf) gameObject.SetActive(false);
        gameObject.SetActive(true);
    }

    void Update() {
        if (!isReady || isBusy) return;
        if (Time.frameCount == openedFrame) return;

        if (confirmMode != ConfirmMode.None) HandleConfirmInput();
        else HandleListInput();
    }

    // 연결이 빠지면 NullReference 가 한참 뒤 엉뚱한 곳에서 터진다. 무엇이 빠졌는지 여기서 이름으로 알린다.
    bool ValidateReferences() {
        List<string> missing = new();

        if (rowPrefab == null) missing.Add(nameof(rowPrefab));
        if (slotList == null) missing.Add(nameof(slotList));
        if (backButton == null) missing.Add(nameof(backButton));
        if (confirmRoot == null) missing.Add(nameof(confirmRoot));
        if (confirmTitle == null) missing.Add(nameof(confirmTitle));
        if (confirmSummary == null) missing.Add(nameof(confirmSummary));
        if (confirmCancelButton == null) missing.Add(nameof(confirmCancelButton));
        if (confirmAcceptButton == null) missing.Add(nameof(confirmAcceptButton));
        if (confirmCancelBackground == null) missing.Add(nameof(confirmCancelBackground));
        if (confirmAcceptBackground == null) missing.Add(nameof(confirmAcceptBackground));
        if (confirmCancelFocus == null) missing.Add(nameof(confirmCancelFocus));
        if (confirmAcceptFocus == null) missing.Add(nameof(confirmAcceptFocus));
        if (confirmCancelLabel == null) missing.Add(nameof(confirmCancelLabel));
        if (confirmAcceptLabel == null) missing.Add(nameof(confirmAcceptLabel));
        if (titleLabel == null) missing.Add(nameof(titleLabel));
        if (subtitleLabel == null) missing.Add(nameof(subtitleLabel));

        if (missing.Count == 0) return true;

        Debug.LogError($"[SaveSlotSelectView] '{name}' 의 프리팹 연결이 비어 있습니다 — {string.Join(", ", missing)}. " +
            "Prefabs/UI/SaveSlotSelect 프리팹을 쓰세요. 없으면 Tools ▸ FCC ▸ Build Save Slot Prefabs 로 만들 수 있습니다.", this);
        return false;
    }

    #endregion
    #region 목록

    // 열 때마다 다시 읽는다. 게임 중 거울에서 저장해 파일이 바뀌었을 수 있다.
    // 줄은 한 번 찍은 것을 계속 쓰고 모자라거나 남을 때만 더하고 뺀다 — 매번 새로 찍으면 레이아웃이 한 프레임 튄다.
    void RebuildRows() {
        int count = SlotCount();

        while (rows.Count < count) {
            SaveSlotRowView row = Instantiate(rowPrefab, slotList);
            row.name = rowPrefab.name + "_" + (rows.Count + 1);
            row.Bind(OnRowHovered, OnRowClicked);
            rows.Add(row);
        }

        // Destroy 는 프레임 끝에 지우므로 부모에서 먼저 떼어낸다. 그래야 같은 프레임의 레이아웃 계산에 옛 줄이 끼지 않는다.
        while (rows.Count > count) {
            SaveSlotRowView extra = rows[rows.Count - 1];
            rows.RemoveAt(rows.Count - 1);
            extra.transform.SetParent(null, false);
            Destroy(extra.gameObject);
        }

        for (int i = 0; i < rows.Count; i++) RefreshRow(i);
    }

    void RefreshRow(int index) {
        SaveData data = SaveManager.Instance != null ? SaveManager.Instance.ReadSlot(index) : null;
        rows[index].SetSlot(index, data, DescribeRegion(data), CheckpointName(data), mode == Mode.NewGame);
    }

    // SaveManager 가 없는 씬(프리팹만 올려 배치를 확인하는 경우)에서도 화면이 비지 않도록 기본 3자리를 보여준다.
    int SlotCount() {
        if (SaveManager.Instance != null) return Mathf.Max(1, SaveManager.Instance.slotCount);

        if (!warnedMissingManager) {
            warnedMissingManager = true;
            Debug.LogWarning("[SaveSlotSelectView] SaveManager 가 없어 모든 자리를 빈자리로 표시합니다. " +
                "메인 메뉴 씬에 SaveManager 가 있어야 기록이 보이고 저장 자리도 바뀝니다.", this);
        }

        return 3;
    }

    int FirstEmptyIndex() {
        for (int i = 0; i < rows.Count; i++) {
            if (!rows[i].HasData) return i;
        }
        return 0;
    }

    // 마지막으로 고른 자리에 기록이 있으면 그 자리, 없으면 기록이 있는 첫 자리.
    int LastPlayedIndex() {
        int active = SaveManager.Instance != null ? SaveManager.Instance.ActiveSlot : 0;
        if (active >= 0 && active < rows.Count && rows[active].HasData) return active;

        for (int i = 0; i < rows.Count; i++) {
            if (rows[i].HasData) return i;
        }
        return 0;
    }

    void FocusRow(int index) {
        if (rows.Count == 0) {
            rowIndex = 0;
            return;
        }

        rowIndex = (index % rows.Count + rows.Count) % rows.Count;
        for (int i = 0; i < rows.Count; i++) rows[i].SetFocused(i == rowIndex);
    }

    // 확인 창이 떠 있는 동안 뒤쪽 줄의 포커스가 움직이면, 창이 묻고 있는 자리와 강조된 자리가 어긋나 보인다.
    void OnRowHovered(SaveSlotRowView row) {
        if (isBusy || confirmMode != ConfirmMode.None) return;

        int index = rows.IndexOf(row);
        if (index >= 0 && index != rowIndex) FocusRow(index);
    }

    // 클릭한 줄을 먼저 고른 뒤 실행한다. 키보드로 골라 Enter 를 누른 것과 결과가 같도록.
    void OnRowClicked(SaveSlotRowView row) {
        if (isBusy || confirmMode != ConfirmMode.None) return;

        int index = rows.IndexOf(row);
        if (index < 0) return;

        FocusRow(index);
        Choose();
    }

    void HandleListInput() {
        if (WasPressed(Key.UpArrow, Key.W)) { FocusRow(rowIndex - 1); return; }
        if (WasPressed(Key.DownArrow, Key.S)) { FocusRow(rowIndex + 1); return; }
        if (WasPressed(Key.Enter, Key.NumpadEnter, Key.Space)) { Choose(); return; }

        if (WasPressed(Key.Delete)) {
            if (rows.Count > 0 && rows[rowIndex].HasData) OpenConfirm(ConfirmMode.Delete);
            return;
        }

        if (WasPressed(Key.Escape)) Close();
    }

    void Choose() {
        if (rows.Count == 0) return;

        SaveSlotRowView row = rows[rowIndex];

        if (mode == Mode.Load) {
            if (row.HasData) LoadSlot(row.SlotIndex, row.Data);
            return; // 불러오기에서 빈자리는 고를 것이 없다.
        }

        if (row.HasData) OpenConfirm(ConfirmMode.Overwrite);
        else StartNewGame(row.SlotIndex);
    }

    #endregion
    #region 확인 창

    void OpenConfirm(ConfirmMode mode) {
        SaveSlotRowView row = rows[rowIndex];
        SaveData data = row.Data;
        if (data == null) return;

        confirmMode = mode;
        acceptFocused = false; // 되돌릴 수 없는 동작이라 매번 「취소」에서 시작한다.
        openedFrame = Time.frameCount;

        bool delete = mode == ConfirmMode.Delete;
        string titleFormat = delete
            ? LocalizationText.Resolve(deleteTitleFormat, "기억 {0:00}을 지울까요?")
            : LocalizationText.Resolve(overwriteTitleFormat, "기억 {0:00}을 덮어쓸까요?");
        confirmTitle.text = string.Format(titleFormat, row.SlotIndex + 1);
        confirmAcceptLabel.text = delete
            ? LocalizationText.Resolve(deleteAcceptText, "지우기")
            : LocalizationText.Resolve(overwriteAcceptText, "덮어쓰기");

        bool knownEgo = data.maxHealth > 0;
        confirmSummary.text = string.Format(LocalizationText.Resolve(summaryFormat, "{0}   |   자아 {1} / {2}   |   기억 조각 {3}"), DescribeRegion(data),
            knownEgo ? data.currentHealth.ToString() : "-",
            knownEgo ? data.maxHealth.ToString() : "-",
            data.memoryShardCount);

        confirmRoot.SetActive(true);
        RefreshConfirmFocus();
        ClearUiSelection();
    }

    void CloseConfirm() {
        confirmMode = ConfirmMode.None;
        confirmRoot.SetActive(false);
        ClearUiSelection();
    }

    void AcceptConfirm() {
        if (confirmMode == ConfirmMode.None || rows.Count == 0) return;

        ConfirmMode mode = confirmMode;
        int slot = rows[rowIndex].SlotIndex;
        CloseConfirm();

        // 덮어쓰기도 파일을 먼저 지운다. 남겨두면 새로 시작한 뒤 첫 거울에 닿기 전에 게임을 끄는 경우,
        // 다음 실행의 [이어하기]가 방금 버리기로 한 옛 진행을 그대로 열어버린다.
        if (SaveManager.Instance != null) SaveManager.Instance.DeleteSlot(slot);

        if (mode == ConfirmMode.Overwrite) {
            StartNewGame(slot);
            return;
        }

        RefreshRow(rowIndex); // 지운 자리를 곧바로 빈자리로 다시 그린다. 포커스는 그 자리에 남는다.
    }

    void HandleConfirmInput() {
        if (WasPressed(Key.LeftArrow, Key.A, Key.RightArrow, Key.D, Key.Tab)) {
            acceptFocused = !acceptFocused;
            RefreshConfirmFocus();
            return;
        }

        if (WasPressed(Key.Enter, Key.NumpadEnter, Key.Space)) {
            if (acceptFocused) AcceptConfirm();
            else CloseConfirm();
            return;
        }

        if (WasPressed(Key.Escape)) CloseConfirm();
    }

    void RefreshConfirmFocus() {
        confirmCancelFocus.SetActive(!acceptFocused);
        confirmAcceptFocus.SetActive(acceptFocused);

        confirmCancelBackground.color = acceptFocused ? buttonColor : focusedButtonColor;
        confirmAcceptBackground.color = acceptFocused ? focusedButtonColor : buttonColor;

        confirmCancelLabel.color = acceptFocused ? cancelLabelColor : focusedCancelLabelColor;
        confirmAcceptLabel.color = dangerLabelColor;
    }

    #endregion
    #region 나가기

    void StartNewGame(int slot) {
        if (SaveManager.Instance != null) SaveManager.Instance.UseSlot(slot);

        if (OnNewGameRequested == null) {
            Debug.LogWarning("[SaveSlotSelectView] 새로 시작을 받아 줄 쪽이 없습니다. " +
                "MainLobbyView 의 saveSlotPanel 칸에 이 화면을 연결하세요.", this);
            return;
        }

        isBusy = true;
        OnNewGameRequested(slot);
    }

    void LoadSlot(int slot, SaveData data) {
        if (OnLoadRequested == null) {
            Debug.LogWarning("[SaveSlotSelectView] 불러오기를 받아 줄 쪽이 없습니다. " +
                "MainLobbyView 의 saveSlotPanel 칸에 이 화면을 연결하세요.", this);
            return;
        }

        // 자리를 먼저 바꿔야 한다. 불러온 씬에서 LoadGame() 이 읽는 파일도, 이후 거울이 쓰는 파일도 이 자리다.
        if (SaveManager.Instance != null) SaveManager.Instance.UseSlot(slot);

        isBusy = true;
        OnLoadRequested(slot, data);
    }

    public void Close() {
        if (isBusy) return;

        CloseConfirm();
        gameObject.SetActive(false);
        if (OnClosed != null) OnClosed();
    }

    #endregion
    #region 이름 풀기

    // 로비의 [이어하기] 옆 표기도 같은 이름을 쓰도록 공개한다. 이름 대응표를 두 곳에 두면 한쪽만 고쳐진다.
    // 고정된 두 씬(First·CoreScene)은 Ui 테이블 키로 먼저 찾는다. 그 외의 씬은 sceneNames 표를
    // 그대로 쓴다 — 아직 번역 키가 없는 새 씬을 추가해도 표만 채우면 바로 보이게 하기 위함이다.
    static readonly LocalizedString AreaPrologue = new("Ui", "area.prologue");
    static readonly LocalizedString AreaCircusTheater = new("Ui", "area.circus_theater");

    public string DescribeRegion(SaveData data) {
        if (data == null) return "";

        switch (data.sceneName) {
            case "First": return LocalizationText.Resolve(AreaPrologue, "프롤로그");
            case "CoreScene": return LocalizationText.Resolve(AreaCircusTheater, "서커스 극장");
        }

        return Lookup(sceneNames, data.sceneName, LocalizationText.Resolve(unknownRegionText, "알 수 없는 장소"));
    }

    string CheckpointName(SaveData data) {
        if (data == null) return "";
        if (string.IsNullOrEmpty(data.checkpointId)) return LocalizationText.Resolve(quickSaveText, "빠른 저장");
        return string.Format(LocalizationText.Resolve(checkpointFormat, "{0}에서 저장"),
            Lookup(checkpointNames, data.checkpointId, LocalizationText.Resolve(unnamedCheckpointText, "거울")));
    }

    static string Lookup(DisplayName[] table, string id, string fallback) {
        if (table == null || string.IsNullOrEmpty(id)) return fallback;

        foreach (DisplayName entry in table) {
            if (entry != null && entry.id == id && !string.IsNullOrEmpty(entry.name)) return entry.name;
        }

        return fallback;
    }

    #endregion
    #region 입력 도우미

    static bool WasPressed(params Key[] keys) {
        Keyboard kb = Keyboard.current;
        if (kb == null) return false;

        foreach (Key k in keys) {
            if (kb[k].wasPressedThisFrame) return true;
        }
        return false;
    }

    // 버튼을 마우스로 누르면 EventSystem 이 그 버튼을 "선택됨" 으로 붙잡아 둔다. 그대로 두면 다음 Enter 에
    // 줄 선택과 함께 그 버튼이 한 번 더 눌린다(「뒤로」가 같이 눌리는 식). 이 화면은 이동을 직접 처리하므로 선택을 늘 비운다.
    static void ClearUiSelection() {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    #endregion
}
