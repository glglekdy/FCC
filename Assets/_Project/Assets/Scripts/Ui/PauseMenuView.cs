using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 게임 중 ESC 로 여는 일시정지 메뉴. 계속하기 · 설정 · 메뉴로 돌아가기 세 줄과, 나가기 전에 한 번 묻는 확인 창을 맡는다.
// Figma 「FCC_UI」의 `일시중단 메뉴` · `메인메뉴로 나가기` 두 안을 옮긴 화면이다.
//
// 생김새는 전부 Prefabs/UI/PauseMenu.prefab 에 있고, 이 스크립트는 선택 상태 · 입력 · 시간 정지 · 나가기 전 저장만 맡는다
// (MainLobbyView 와 같은 구조). 줄 하나의 표시는 PauseMenuItemView 담당.
//
// **프리팹 루트는 켜 둔 채로 두세요.** 닫혀 있을 때는 창(windowRoot)만 끈다. 루트를 끄면 Update 가 돌지 않아
// ESC 로 열 수 없다 (SkillLoadoutView 와 같은 이유).
//
// 키보드는 Keyboard 를 직접 읽는다. 프로젝트가 새 Input System 전용이라 구형 Input 은 예외를 던지고,
// EventSystem 내비게이션은 씬마다 설정이 달라 같은 결과를 보장하지 못한다 (SettingsPanelView 와 같은 이유).
//
// 프리팹을 처음부터 다시 찍어내려면 에디터 메뉴 Tools ▸ FCC ▸ Build Pause Menu Prefab.
public class PauseMenuView : MonoBehaviour {
    #region 인스펙터 변수

    [Header("연결 — 프리팹이 채워 둔 값입니다")]
    public GameObject windowRoot; // 메뉴 전체(바탕 · 머리 · 줄 · 꼬리 · 확인 창). 열고 닫을 때 이것만 켜고 끈다.
    // 메뉴 3줄. **배열 순서가 그대로 ↑↓ 이동 순서입니다.** 무엇을 하는 줄인지는 각 줄의 action 이 정한다.
    public PauseMenuItemView[] items;

    [Header("연결 — 확인 창")]
    public GameObject confirmRoot; // 뒤를 덮는 막과 창을 함께 켜고 끈다.
    public TMP_Text confirmTitle; // 저장할 수 있는지에 따라 문구가 바뀐다.
    public TMP_Text confirmMessage; // 어느 기억 자리에 저장되는지.
    public Button confirmCancelButton;
    public Button confirmAcceptButton;
    public Image confirmCancelBackground;
    public Image confirmAcceptBackground;
    public GameObject confirmCancelFocus; // 포커스가 온 버튼에만 켜지는 포인트 테두리.
    public GameObject confirmAcceptFocus;
    public TMP_Text confirmCancelLabel;
    public TMP_Text confirmAcceptLabel;

    [Header("연결 — 씬에서 직접 이어주세요")]
    // **씬의 SettingsPanel 을 물려주세요.** 비어 있으면 [설정] 줄이 잠긴 채로 표시된다.
    public SettingsPanelView settingsPanel;

    [Header("씬 전환")]
    // **빌드 세팅(File ▸ Build Profiles)에 등록된 메뉴 씬 이름을 정확히 적으세요.**
    public string mainMenuSceneName = "Main_menu";
    public string playerTag = "Player"; // 입력을 끄고 저장할 플레이어를 찾을 태그. 씬마다 플레이어가 달라 열 때마다 새로 찾는다.

    [Header("저장")]
    // 나갈 때 복귀 지점을 이 자리에 마지막으로 저장한 곳(보통 거울)으로 둘지. 끄면 지금 서 있는 자리가 복귀 지점이 된다.
    // 기본을 켜 둔 이유: 정식 저장 지점은 거울이고, 지금 자리를 그대로 적으면 낭떠러지 위나 전투 한복판에서 저장돼
    // 불러오자마자 떨어지거나 맞는다. 같은 씬에 저장한 기록이 없을 때만 지금 자리를 쓴다.
    public bool keepLastRespawn = true;

    [Header("문구")]
    public string saveTitle = "저장하고 메인메뉴로 나가시겠습니까?";
    public string saveMessageFormat = "해당 게임은 기억 {0:00}에 저장됩니다."; // {0} = 화면에 보이는 자리 번호(1부터).
    public string unsavableTitle = "메인메뉴로 나가시겠습니까?"; // 저장할 수 없는 곳(SaveManager 가 없거나 던전 안)에서 열었을 때.
    public string unsavableMessage = "이곳에서는 진행이 저장되지 않습니다.";

    [Header("색상")]
    public Color buttonColor = UiTheme.Panel;
    public Color focusedButtonColor = UiTheme.PanelRaised;
    public Color cancelLabelColor = UiTheme.TextBody;
    public Color focusedCancelLabelColor = UiTheme.TextHigh;
    public Color dangerLabelColor = UiTheme.AccentBright; // 되돌릴 수 없는 쪽 버튼. 포커스와 무관하게 적색 글자로 둔다.

    #endregion
    #region 상태

    enum SavePlan {
        None,            // 저장하지 않고 나간다.
        KeepRespawn,     // 진행만 새로 적고 복귀 지점은 마지막 기록의 것을 그대로 둔다.
        CurrentPosition, // 지금 서 있는 자리를 복귀 지점으로 적는다.
    }

    int selectedIndex = -1;

    bool confirmOpen;
    bool acceptFocused; // 확인 창에서 「나가기」 쪽에 포커스가 있는지.
    SavePlan savePlan;
    SaveData lastRecord; // KeepRespawn 일 때 복귀 지점을 가져올 기록.

    // 입력을 받지 않을 프레임. 메뉴를 연 ESC · 다른 화면을 닫은 ESC 가 같은 프레임에 한 번 더 먹혀
    // 열자마자 닫히거나, 설정창을 닫은 ESC 에 메뉴까지 함께 닫히는 것을 막는다.
    int ignoreInputFrame = -1;

    PlayerInput pausedPlayerInput; // 열 때 꺼 둔 플레이어 입력. 닫을 때 이것만 다시 켠다.

    bool isReady; // 프리팹 연결이 온전한지. 어긋난 채로 두면 NullReference 가 쏟아지므로 Awake 에서 한 번만 검사한다.
    bool isLeaving; // 메인메뉴로 넘어가기 시작한 뒤. 전환 중의 입력과 시간 정지 강제를 멈춘다.

    public bool IsOpen { get; private set; }

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        isReady = ValidateReferences();
        if (!isReady) return;

        foreach (PauseMenuItemView item in items) {
            item.Bind(HandleItemHovered, HandleItemClicked);
        }

        confirmCancelButton.onClick.AddListener(CloseConfirm);
        confirmAcceptButton.onClick.AddListener(AcceptConfirm);

        if (settingsPanel != null) {
            settingsPanel.OnClosed += HandleSettingsClosed;
            settingsPanel.gameObject.SetActive(false);
        }

        confirmRoot.SetActive(false);
        windowRoot.SetActive(false);
    }

    void OnDestroy() {
        if (settingsPanel != null) settingsPanel.OnClosed -= HandleSettingsClosed;

        // 열린 채로 씬이 내려가면(에디터에서 씬을 바꾸는 경우 등) 멈춘 시간이 다음 씬까지 따라간다.
        // 메인메뉴로 나가는 중이면 ScreenFader 가 되돌리므로 건드리지 않는다.
        if (IsOpen && !isLeaving) Time.timeScale = 1f;
    }

    // SkillLoadoutView 가 정비 화면을 여는 동안 이 컴포넌트를 끄고, 닫을 때 다시 켠다.
    // 정비 화면을 닫은 ESC 가 다시 켜진 이 메뉴에 같은 프레임에 먹히면 닫자마자 일시정지가 열린다.
    void OnEnable() {
        ignoreInputFrame = Time.frameCount;
    }

    void Update() {
        if (!isReady || isLeaving) return;
        if (Time.frameCount == ignoreInputFrame) return;

        if (!IsOpen) {
            if (WasPressed(Key.Escape)) Open();
            return;
        }

        if (IsSettingsOpen()) return; // 설정창은 ESC 까지 자기 입력을 스스로 받는다. 떠 있는 동안 메뉴는 손을 뗀다.

        if (confirmOpen) HandleConfirmInput();
        else HandleMenuInput();
    }

    // 히트스톱(HitFeedback)은 실시간으로 기다렸다가 timeScale 을 되돌린다. 그 도중에 메뉴를 열면 메뉴 뒤에서 게임이 다시 흐른다.
    // 코루틴은 Update 뒤에 돌기 때문에 LateUpdate 에서 한 번 더 멈춰 둔다.
    void LateUpdate() {
        if (IsOpen && !isLeaving) Time.timeScale = 0f;
    }

    #endregion
    #region 프리팹 연결 검사

    // 연결이 빠지면 NullReference 가 한참 뒤 엉뚱한 곳에서 터진다. 무엇이 빠졌는지 여기서 이름으로 알린다.
    bool ValidateReferences() {
        List<string> missing = new();

        if (windowRoot == null) missing.Add(nameof(windowRoot));

        if (items == null || items.Length == 0) {
            missing.Add($"{nameof(items)}(메뉴 줄이 하나도 없습니다)");
        }
        else {
            for (int i = 0; i < items.Length; i++) {
                if (items[i] == null) missing.Add($"{nameof(items)}[{i}]");
            }
        }

        if (confirmRoot == null) missing.Add(nameof(confirmRoot));
        if (confirmTitle == null) missing.Add(nameof(confirmTitle));
        if (confirmMessage == null) missing.Add(nameof(confirmMessage));
        if (confirmCancelButton == null) missing.Add(nameof(confirmCancelButton));
        if (confirmAcceptButton == null) missing.Add(nameof(confirmAcceptButton));
        if (confirmCancelBackground == null) missing.Add(nameof(confirmCancelBackground));
        if (confirmAcceptBackground == null) missing.Add(nameof(confirmAcceptBackground));
        if (confirmCancelFocus == null) missing.Add(nameof(confirmCancelFocus));
        if (confirmAcceptFocus == null) missing.Add(nameof(confirmAcceptFocus));
        if (confirmCancelLabel == null) missing.Add(nameof(confirmCancelLabel));
        if (confirmAcceptLabel == null) missing.Add(nameof(confirmAcceptLabel));

        if (missing.Count == 0) return true;

        Debug.LogError($"[PauseMenuView] '{name}' 의 프리팹 연결이 비어 있어 일시정지 메뉴를 열 수 없습니다 — {string.Join(", ", missing)}. " +
            "Prefabs/UI/PauseMenu 프리팹을 씬에 놓으세요. 없으면 Tools ▸ FCC ▸ Build Pause Menu Prefab 으로 만들 수 있습니다.", this);
        return false;
    }

    #endregion
    #region 열기 · 닫기

    public void Open() {
        if (!isReady || IsOpen || isLeaving) return;
        if (!CanOpen()) return;

        IsOpen = true;
        ignoreInputFrame = Time.frameCount;

        Time.timeScale = 0f;
        PausePlayerInput();

        CloseConfirm();
        windowRoot.SetActive(true);

        RefreshLocks();
        Select(FirstUnlocked());
        ClearUiSelection();
    }

    // [계속하기] · ESC.
    public void Resume() {
        if (!IsOpen || isLeaving) return;

        IsOpen = false;
        CloseConfirm();
        windowRoot.SetActive(false);

        // 연 순간의 배속이 아니라 1 로 되돌린다. 히트스톱 도중에 열었다면 그때의 배속(0.02)이 저장돼 닫은 뒤에도
        // 느린 채로 남는다. 히트스톱이 아직 도는 중이면 그쪽이 끝날 때 배속을 알아서 되돌린다 (ScreenFader 도 1 로 되돌린다).
        Time.timeScale = 1f;

        ResumePlayerInput();
        ClearUiSelection();
    }

    // 대사 · 컷씬 · 정비 화면 · 깨어나기 연출처럼 플레이어 이동이 잠긴 동안에는 열지 않는다.
    // 대사 넘김(Space · 좌클릭)이 메뉴 조작과 겹쳐, 메뉴를 고르는 사이 뒤에서 대사가 넘어가 버리기 때문이다.
    bool CanOpen() {
        if (ScreenFader.Instance != null && ScreenFader.Instance.IsTransitioning) return false;
        if (Time.timeScale == 0f) return false; // 다른 화면이 이미 시간을 멈춰 두었다. 닫을 때 그 화면의 정지까지 풀어버리게 된다.

        GameObject player = FindPlayer();
        if (player != null && player.TryGetComponent(out Player_move move) && move.isMovementLocked) return false;

        return true;
    }

    // 메뉴를 고르는 키가 플레이어 조작과 겹친다(Space = 점프, 좌클릭 = 공격). 시간이 멈춰 있어도 입력 콜백은 그대로 돌아
    // 점프 힘이 걸렸다가 메뉴를 닫는 순간 튀어 오른다. 그래서 열려 있는 동안 플레이어 입력을 통째로 끈다.
    void PausePlayerInput() {
        GameObject player = FindPlayer();
        if (player == null || !player.TryGetComponent(out PlayerInput input)) return;
        if (!input.inputIsActive) return; // 이미 누군가 꺼 뒀다면 켜는 것도 그쪽에 맡긴다.

        input.DeactivateInput();
        pausedPlayerInput = input;
    }

    void ResumePlayerInput() {
        if (pausedPlayerInput != null) pausedPlayerInput.ActivateInput();
        pausedPlayerInput = null;
    }

    GameObject FindPlayer() {
        return GameObject.FindGameObjectWithTag(playerTag);
    }

    #endregion
    #region 선택 이동

    // 아직 열 수 없는 줄을 잠근다. 지금은 [설정] 하나뿐이다.
    void RefreshLocks() {
        foreach (PauseMenuItemView item in items) {
            item.SetUnlocked(item.action != PauseMenuAction.Settings || settingsPanel != null);
        }
    }

    int FirstUnlocked() {
        for (int i = 0; i < items.Length; i++) {
            if (items[i].IsUnlocked) return i;
        }
        return 0;
    }

    void Select(int index) {
        selectedIndex = index;

        for (int i = 0; i < items.Length; i++) {
            items[i].SetSelected(i == selectedIndex);
        }
    }

    // 잠긴 줄은 건너뛰고, 끝에 닿으면 반대편으로 감싼다.
    void MoveSelection(int delta) {
        int index = selectedIndex;

        for (int step = 0; step < items.Length; step++) {
            index = (index + delta + items.Length) % items.Length;
            if (!items[index].IsUnlocked) continue;

            Select(index);
            return;
        }
    }

    #endregion
    #region 입력 처리

    void HandleMenuInput() {
        if (WasPressed(Key.UpArrow, Key.W)) { MoveSelection(-1); return; }
        if (WasPressed(Key.DownArrow, Key.S)) { MoveSelection(1); return; }
        if (WasPressed(Key.Enter, Key.NumpadEnter, Key.Space)) { Activate(); return; }

        if (WasPressed(Key.Escape)) Resume();
    }

    // 마우스가 실제로 움직였을 때만 커서를 따라간다. ESC 로 메뉴가 뜨는 순간 가만히 있던 커서 밑에 줄이 깔려도
    // 포인터 진입이 발생하는데, 그대로 따르면 [계속하기]가 아니라 커서 밑의 줄(예: 메뉴로 돌아가기)이 골라진 채로 열린다.
    void HandleItemHovered(PauseMenuItemView item) {
        if (!CanPoint()) return;

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.delta.ReadValue() == Vector2.zero) return;

        SelectItem(item);
    }

    // 클릭은 분명히 그 줄을 고른 것이므로 움직임과 무관하게 받는다. 먼저 고른 뒤 실행해 키보드로 Enter 를 누른 것과 결과를 맞춘다.
    void HandleItemClicked(PauseMenuItemView item) {
        if (!CanPoint()) return;

        SelectItem(item);
        Activate();
    }

    void SelectItem(PauseMenuItemView item) {
        int index = Array.IndexOf(items, item);
        if (index >= 0 && index != selectedIndex) Select(index);
    }

    // 설정창이나 확인 창이 떠 있는 동안 뒤쪽 줄의 포커스가 움직이면, 앞 화면을 닫았을 때 엉뚱한 줄이 골라져 있다.
    bool CanPoint() {
        return IsOpen && !isLeaving && !confirmOpen && !IsSettingsOpen();
    }

    void Activate() {
        if (selectedIndex < 0 || selectedIndex >= items.Length) return;

        PauseMenuItemView item = items[selectedIndex];
        if (!item.IsUnlocked) return;

        switch (item.action) {
            case PauseMenuAction.Resume: Resume(); break;
            case PauseMenuAction.Settings: OpenSettings(); break;
            case PauseMenuAction.MainMenu: OpenConfirm(); break;
        }
    }

    #endregion
    #region 설정

    void OpenSettings() {
        if (settingsPanel == null) return;

        settingsPanel.gameObject.SetActive(true);
        ClearUiSelection();
    }

    bool IsSettingsOpen() {
        return settingsPanel != null && settingsPanel.gameObject.activeSelf;
    }

    // 설정창을 닫은 ESC 가 같은 프레임에 메뉴까지 닫지 않도록 그 프레임의 입력은 넘긴다.
    void HandleSettingsClosed() {
        ignoreInputFrame = Time.frameCount;
        ClearUiSelection();
    }

    #endregion
    #region 확인 창

    void OpenConfirm() {
        savePlan = DecideSavePlan(out lastRecord);
        bool saves = savePlan != SavePlan.None;

        confirmTitle.text = saves ? saveTitle : unsavableTitle;
        confirmMessage.text = saves ? string.Format(saveMessageFormat, SaveManager.Instance.ActiveSlot + 1) : unsavableMessage;

        confirmOpen = true;
        acceptFocused = false; // 되돌릴 수 없는 동작이라 매번 「취소」에서 시작한다 (SaveSlotSelectView 와 같은 규칙).
        ignoreInputFrame = Time.frameCount;

        confirmRoot.SetActive(true);
        RefreshConfirmFocus();
        ClearUiSelection();
    }

    void CloseConfirm() {
        confirmOpen = false;
        confirmRoot.SetActive(false);
        ClearUiSelection();
    }

    void AcceptConfirm() {
        if (!confirmOpen || isLeaving) return;

        SaveBeforeLeaving();

        isLeaving = true;
        ClearUiSelection();

        // 시간은 멈춘 채로 둔다. 막이 내려가는 동안 뒤에서 몬스터가 움직이면 안 되고, 씬을 넘기기 직전에 ScreenFader 가 1 로 되돌린다.
        ScreenFader.LoadScene(mainMenuSceneName);
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
    #region 나가기 전 저장

    // 확인 창 문구가 이 결과를 따라가야 하므로 창을 열 때 한 번 정하고, 「나가기」를 누르면 그대로 실행한다.
    SavePlan DecideSavePlan(out SaveData record) {
        record = null;

        SaveManager saves = SaveManager.Instance;
        if (saves == null) return SavePlan.None;

        // 플레이어가 없으면(사망 연출 중 등) 체력 · 조각 · 스킬이 전부 빈 값으로 적혀 기존 기억을 망친다.
        if (FindPlayer() == null) return SavePlan.None;

        if (keepLastRespawn) {
            record = saves.ReadSlot(saves.ActiveSlot);

            // 다른 씬에서 적힌 좌표는 이 씬에서 아무 의미가 없다.
            if (record != null && record.sceneName == SceneManager.GetActiveScene().name) return SavePlan.KeepRespawn;
            record = null;
        }

        // 던전은 같은 씬 안에 방을 만들었다 지우는 구조라, 안에서 적은 좌표는 불러오면 허공이 된다.
        if (DungeonRespawnController.Instance != null) return SavePlan.None;

        return SavePlan.CurrentPosition;
    }

    void SaveBeforeLeaving() {
        SaveManager saves = SaveManager.Instance;
        if (saves == null) return;

        switch (savePlan) {
            case SavePlan.KeepRespawn:
                saves.SaveGame(lastRecord.checkpointId, new Vector2(lastRecord.posX, lastRecord.posY));
                break;
            case SavePlan.CurrentPosition:
                // 거울이 아니므로 checkpointId 는 비운다. 기억 선택 화면에는 "빠른 저장" 으로 보인다.
                saves.SaveGame("");
                break;
        }
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
    // 줄 선택과 함께 그 버튼이 한 번 더 눌린다. 이 화면은 이동을 직접 처리하므로 선택을 늘 비운다 (SettingsPanelView 와 같은 이유).
    static void ClearUiSelection() {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    #endregion
}
