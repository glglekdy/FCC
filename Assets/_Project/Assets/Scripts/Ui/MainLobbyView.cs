using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 메인 로비(커튼콜 정면) 화면. 타이틀과 메뉴 5개를 띄우고 ↑↓ 와 마우스로 항목을 고른다.
// Figma 「FCC_UI」의 MainLobby_new 안을 옮긴 화면이다.
//
// 예전에는 시작할 때 커튼이 좌우로 열리며 알맹이가 떠오르는 연출이 있었는데 뺐다. 커튼은 이제 화면 가장자리에
// 고정된 무대 막이라 이 스크립트가 만지지 않는다 (씬 전환의 암전은 ScreenFader 가 따로 맡는다).
//
// 생김새(커튼·타이틀·메뉴 줄)는 전부 프리팹 Prefabs/UI/MainLobby.prefab 에 들어 있고,
// 이 스크립트는 선택 상태 계산과 입력만 맡는다 (SkillLoadoutView와 같은 구조). 줄 하나의 표시는 MainLobbyItemView 담당.
// 프리팹을 처음부터 다시 찍어내려면 에디터 메뉴 Tools ▸ FCC ▸ Build Main Lobby Prefab.
public class MainLobbyView : MonoBehaviour {
    #region 인스펙터 변수

    [Header("연결 — 프리팹이 채워 둔 값입니다")]
    // 메뉴 5줄. **배열 순서가 그대로 ↑↓ 이동 순서입니다.** 무엇을 하는 줄인지는 각 줄의 action이 정한다.
    public MainLobbyItemView[] items;
    public TMP_Text versionLabel; // 좌하단 버전 표기. Application.version 을 그대로 찍는다.

    [Header("연결 — 씬에서 직접 이어주세요")]
    // **씬의 설정 패널 오브젝트를 물려주세요.** 비어 있으면 [설정] 줄이 잠긴 채로 표시된다.
    public GameObject settingsPanel;
    // **씬의 기록 선택 화면(SaveSlotSelect 프리팹)을 물려주세요.** [새로 시작]·[이어하기]가 이 화면에서 자리를 고르게 된다.
    // 비워두면 예전처럼 [새로 시작]은 곧바로 첫 씬으로, [이어하기]는 마지막 기록으로 바로 넘어간다.
    public SaveSlotSelectView saveSlotPanel;

    [Header("씬 이름")]
    // **빌드 세팅(File ▸ Build Profiles)에 등록된 씬 이름을 정확히 적으세요.**
    public string newGameSceneName = "First"; // [새로 시작] — 0장 프롤로그부터.
    public string memoryRoomSceneName = ""; // [기억의 방] — 씬이 아직 없다. 비워두면 줄이 잠긴 채로 표시된다.

    [Header("문구")]
    public string versionFormat = "v{0}"; // {0} = Application.version.
    public string continueEmptyText = "저장된 기억 없음"; // 세이브가 없을 때 [이어하기] 오른쪽에 붙는 표기.
    // {0} = 저장된 씬 이름, {1} = 저장 시각. 챕터·일차 표기는 SaveData에 해당 필드가 생기면 여기만 바꾸면 된다.
    public string continueFormat = "{0} · {1}";

    #endregion
    #region 컴포넌트 변수

    InputAction escapeAction; // 설정창을 닫는 ESC. MainMenuController와 같은 방식으로 코드에서 만든다.

    SaveData continueTarget; // [이어하기]가 열 세이브(마지막으로 고른 자리). 없으면 null.
    bool hasAnySave; // 어느 자리든 기록이 하나라도 있는지. 없으면 [이어하기] 줄이 잠긴다.
    int selectedIndex = -1;
    bool isReady; // 프리팹 연결이 온전한지. 어긋난 채로 두면 NullReference가 쏟아지므로 Awake에서 한 번만 검사한다.
    bool isBusy; // 씬 전환을 시작한 뒤. 연타로 두 번 넘어가는 것을 막는다.

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        escapeAction = new InputAction(binding: "<Keyboard>/escape");

        isReady = ValidateReferences();
        if (!isReady) return;

        foreach (MainLobbyItemView item in items) {
            item.Bind(HandleItemHovered, HandleItemClicked);
        }

        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (saveSlotPanel != null) {
            saveSlotPanel.gameObject.SetActive(false);
            saveSlotPanel.OnNewGameRequested += HandleNewGameRequested;
            saveSlotPanel.OnLoadRequested += HandleLoadRequested;
            saveSlotPanel.OnClosed += HandleSaveSlotClosed;
        }
    }

    void OnDestroy() {
        if (saveSlotPanel == null) return;

        saveSlotPanel.OnNewGameRequested -= HandleNewGameRequested;
        saveSlotPanel.OnLoadRequested -= HandleLoadRequested;
        saveSlotPanel.OnClosed -= HandleSaveSlotClosed;
    }

    void OnEnable() { escapeAction.Enable(); }
    void OnDisable() { escapeAction.Disable(); }

    void Start() {
        if (!isReady) return;

        RefreshContinue();
        RefreshVersion();
        RefreshLocks();
        SelectFirstUnlocked();
    }

    void Update() {
        if (!isReady || isBusy) return;

        // 설정창이 떠 있는 동안에는 로비 커서가 움직이면 안 된다. ESC로 닫는 것만 받는다.
        if (settingsPanel != null && settingsPanel.activeSelf) {
            if (escapeAction.triggered) settingsPanel.SetActive(false);
            return;
        }

        // 기록 선택 화면은 ESC 까지 자기 입력을 스스로 받는다. 떠 있는 동안 로비는 손을 뗀다.
        if (IsSaveSlotOpen()) return;

        HandleInput();
    }

    #endregion
    #region 프리팹 연결 검사

    // 프리팹을 쓰지 않고 컴포넌트만 붙였거나, 프리팹을 손보다 참조를 끊었을 때 조용히 죽지 않도록
    // 무엇이 비었는지 이름으로 찍어준다. 하나라도 비면 화면을 아예 굴리지 않는다.
    bool ValidateReferences() {
        List<string> missing = new();

        if (items == null || items.Length == 0) {
            missing.Add($"{nameof(items)}(메뉴 줄이 하나도 없습니다)");
        }
        else {
            for (int i = 0; i < items.Length; i++) {
                if (items[i] == null) missing.Add($"{nameof(items)}[{i}]");
            }
        }

        if (missing.Count == 0) return true;

        Debug.LogError($"[MainLobbyView] 프리팹 연결이 비어 있어 로비를 열 수 없습니다 — {string.Join(", ", missing)}. " +
            "Prefabs/UI/MainLobby 프리팹을 씬에 놓으세요. 프리팹이 없으면 Tools ▸ FCC ▸ Build Main Lobby Prefab 으로 만들 수 있습니다.", this);
        return false;
    }

    #endregion
    #region 표시 갱신

    // 세이브 파일을 읽어 [이어하기] 줄에 어느 지점인지 적어둔다.
    // SaveManager는 씬을 넘어 살아남는 싱글턴이라 로비 씬에 없을 수도 있는데, 그때는 세이브가 없는 것으로 본다.
    void RefreshContinue() {
        SaveManager saves = SaveManager.Instance;

        continueTarget = saves != null ? saves.Read() : null;
        if (continueTarget != null && string.IsNullOrEmpty(continueTarget.sceneName)) continueTarget = null;

        // 기록 선택 화면이 있으면 마지막 자리가 비어 있어도 다른 자리의 기록을 골라 불러올 수 있다.
        hasAnySave = continueTarget != null || (saveSlotPanel != null && saves != null && saves.HasAnySlot());

        MainLobbyItemView item = FindItem(MainLobbyAction.Continue);
        if (item == null) return;

        if (continueTarget == null) {
            item.SetSuffix(hasAnySave ? "" : continueEmptyText);
            return;
        }

        // 씬 이름(CoreScene 등)이 그대로 보이지 않도록 기록 선택 화면의 이름 대응표를 함께 쓴다.
        string region = saveSlotPanel != null ? saveSlotPanel.DescribeRegion(continueTarget) : continueTarget.sceneName;
        item.SetSuffix(string.Format(continueFormat, region, continueTarget.savedAt));
    }

    void RefreshVersion() {
        if (versionLabel != null) versionLabel.text = string.Format(versionFormat, Application.version);
    }

    // 아직 열 수 없는 줄을 잠근다. 지우지 않고 흐리게 남기는 이유는 앞으로 무엇이 생길지 보여주기 위해서다.
    void RefreshLocks() {
        foreach (MainLobbyItemView item in items) {
            item.SetUnlocked(IsUnlocked(item.action));
        }
    }

    bool IsUnlocked(MainLobbyAction action) {
        return action switch {
            MainLobbyAction.Continue => hasAnySave,
            MainLobbyAction.MemoryRoom => !string.IsNullOrEmpty(memoryRoomSceneName), // 기억의 방 씬이 생기기 전까지는 잠김.
            MainLobbyAction.Settings => settingsPanel != null,
            _ => true,
        };
    }

    MainLobbyItemView FindItem(MainLobbyAction action) {
        foreach (MainLobbyItemView item in items) {
            if (item.action == action) return item;
        }
        return null;
    }

    #endregion
    #region 선택 이동

    void SelectFirstUnlocked() {
        for (int i = 0; i < items.Length; i++) {
            if (!items[i].IsUnlocked) continue;

            Select(i);
            return;
        }
    }

    void Select(int index) {
        selectedIndex = index;

        for (int i = 0; i < items.Length; i++) {
            items[i].SetSelected(i == selectedIndex);
        }
    }

    // 잠긴 줄은 건너뛰고, 끝에 닿으면 반대편으로 감싼다. 항목이 5개뿐이라 위아래로 자주 오간다.
    void MoveSelection(int delta) {
        if (items.Length == 0) return;

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

    // EventSystem의 내비게이션에 기대지 않고 키보드를 직접 읽는다. UI 모듈 설정이 씬마다 달라도
    // 똑같이 동작해야 하기 때문 (마우스는 MainLobbyItemView가 따로 받는다). SkillLoadoutView와 같은 방식.
    void HandleInput() {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) MoveSelection(-1);
        if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) MoveSelection(1);

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) {
            Activate();
        }
    }

    void HandleItemHovered(MainLobbyItemView item) {
        if (isBusy) return;
        if (settingsPanel != null && settingsPanel.activeSelf) return;
        if (IsSaveSlotOpen()) return;

        int index = System.Array.IndexOf(items, item);
        if (index >= 0) Select(index);
    }

    void HandleItemClicked(MainLobbyItemView item) {
        if (isBusy) return;
        if (settingsPanel != null && settingsPanel.activeSelf) return;
        if (IsSaveSlotOpen()) return;

        HandleItemHovered(item); // 클릭한 줄을 먼저 고른 뒤 실행한다. 키보드 흐름과 결과가 같도록.
        Activate();
    }

    #endregion
    #region 항목 실행

    void Activate() {
        if (selectedIndex < 0 || selectedIndex >= items.Length) return;

        MainLobbyItemView item = items[selectedIndex];
        if (!item.IsUnlocked) return;

        switch (item.action) {
            case MainLobbyAction.Continue: OpenContinue(); break;
            case MainLobbyAction.NewGame: OpenNewGame(); break;
            case MainLobbyAction.MemoryRoom: LoadScene(memoryRoomSceneName); break;
            case MainLobbyAction.Settings: OpenSettings(); break;
            case MainLobbyAction.Quit: Quit(); break;
        }
    }

    // 세이브에 적힌 씬을 연 뒤에 상태를 되돌린다.
    // SaveManager.LoadGame()은 같은 씬 안에서만 복원하므로, 씬이 다 뜬 다음에 불러야 한다.
    void Continue() {
        if (continueTarget == null) return;

        SceneManager.sceneLoaded += RestoreAfterLoad;
        LoadScene(continueTarget.sceneName);
    }

    // 씬 전환과 함께 이 컴포넌트는 사라지므로 인스턴스 메서드로는 복원 시점을 받을 수 없다. 그래서 static이다.
    static void RestoreAfterLoad(Scene scene, LoadSceneMode mode) {
        SceneManager.sceneLoaded -= RestoreAfterLoad; // 한 번만 받고 곧바로 뗀다. 안 떼면 이후 모든 씬 로드에서 되돌린다.

        if (SaveManager.Instance == null) {
            Debug.LogWarning("[MainLobbyView] SaveManager가 없어 이어하기 상태를 복원하지 못했습니다. " +
                "세이브를 쓰는 씬에는 SaveManager가 살아 있어야 합니다.");
            return;
        }

        SaveManager.Instance.LoadGame();
    }

    // 씬 로드를 직접 하지 않고 ScreenFader를 거친다 — 커튼이 닫히듯 화면이 덮인 뒤에 넘어가야 하기 때문.
    // 씬에 ScreenFader가 없으면 ScreenFader.LoadScene이 알아서 연출 없이 바로 넘긴다.
    void LoadScene(string sceneName) {
        if (string.IsNullOrEmpty(sceneName)) {
            Debug.LogWarning("[MainLobbyView] 씬 이름이 비어 있어 넘어가지 않았습니다. 인스펙터의 씬 이름을 채우세요.", this);
            return;
        }

        isBusy = true;
        ScreenFader.LoadScene(sceneName);
    }

    // 기록 선택 화면이 연결돼 있으면 자리를 먼저 고르게 한다. 비어 있으면 예전처럼 곧바로 넘어간다
    // (기록 선택 화면을 아직 올리지 않은 씬에서도 메뉴가 막히지 않게 하기 위함이다).
    void OpenNewGame() {
        if (saveSlotPanel != null) saveSlotPanel.Open(SaveSlotSelectView.Mode.NewGame);
        else LoadScene(newGameSceneName);
    }

    void OpenContinue() {
        if (saveSlotPanel != null) saveSlotPanel.Open(SaveSlotSelectView.Mode.Load);
        else Continue();
    }

    // 자리는 SaveSlotSelectView 가 이미 SaveManager 에 정해두고 넘어온다. 로비는 씬만 넘긴다.
    void HandleNewGameRequested(int slot) {
        LoadScene(newGameSceneName);
    }

    // 고른 기록을 [이어하기] 대상으로 삼아 기존 흐름(씬 열기 → 상태 되돌리기)을 그대로 탄다.
    void HandleLoadRequested(int slot, SaveData data) {
        continueTarget = data;
        Continue();
    }

    // 기록 선택 화면에서 기록을 지우고 나왔을 수 있다. [이어하기]의 잠금과 표기를 다시 맞춘다.
    void HandleSaveSlotClosed() {
        RefreshContinue();
        RefreshLocks();

        if (selectedIndex < 0 || !items[selectedIndex].IsUnlocked) SelectFirstUnlocked();
    }

    bool IsSaveSlotOpen() {
        return saveSlotPanel != null && saveSlotPanel.gameObject.activeSelf;
    }

    void OpenSettings() {
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    void Quit() {
        Debug.Log("게임 종료!");
        Application.Quit(); // 에디터에서는 동작하지 않고 빌드에서만 실제로 종료됨.
    }

    #endregion
}
