using UnityEngine;
using UnityEngine.InputSystem;

public class MainMenuController : MonoBehaviour {
    #region 인스펙터 변수

    [Header("UI Panels")]
    public GameObject settingsPanel; // 메인 메뉴에서 열리는 설정창.

    [Header("씬 이름")]
    // [Game Start] 로 시작하는 씬. 프롤로그 컷씬(Chapter0/CutScenePage/First)부터 시작한다.
    // **빌드 세팅(File ▸ Build Profiles)에 등록된 씬 이름을 정확히 적으세요.**
    public string startSceneName = "First";

    [Header("단축키")]
    // 메인 메뉴에서 이 키를 누르면 CoreScene으로 바로 진입한다. 빌드 종류와 무관하게 동작한다.
    public Key debugSkipToCoreSceneKey = Key.P;
    public string debugCoreSceneName = "CoreScene";

    // 메인 메뉴에서 이 키를 두 번 연달아 누르면 저장된 기억(세이브 파일)을 자리마다 전부 지운다.
    // 되돌릴 수 없는 동작이라 한 번에 지우지 않는다 — 첫 입력은 예고만 하고, 아래 시간 안에 한 번 더 눌러야 실행된다.
    // **쓰지 않을 때는 Key.None 으로 두세요.** 시연·개발용 단축키라 확인 창을 띄우지 않는다.
    public Key resetAllSavesKey = Key.O;
    public float resetAllSavesWindow = 1.5f; // 두 번째 입력을 기다리는 시간(초).

    #endregion
    #region 컴포넌트 변수

    InputAction escapeAction;

    float lastResetKeyTime = float.NegativeInfinity; // 초기화 단축키를 마지막으로 누른 시각(unscaled).

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        escapeAction = new InputAction(binding: "<Keyboard>/escape");
    }

    void OnEnable() { escapeAction.Enable(); }
    void OnDisable() { escapeAction.Disable(); }

    void Update() {
        // 메인 메뉴에서 ESC를 누르면 열려있는 설정창을 닫아준다.
        if (escapeAction.triggered && settingsPanel != null && settingsPanel.activeSelf) {
            CloseSettings();
        }

        if (debugSkipToCoreSceneKey != Key.None && Keyboard.current != null
            && Keyboard.current[debugSkipToCoreSceneKey].wasPressedThisFrame) {
            ScreenFader.LoadScene(debugCoreSceneName);
        }

        HandleResetAllSaves();
    }

    #endregion
    #region 세이브 초기화 단축키

    // 실수로 기록을 날리지 않도록 첫 입력은 예고만 하고 두 번째 입력에서 지운다.
    void HandleResetAllSaves() {
        if (resetAllSavesKey == Key.None || Keyboard.current == null) return;
        if (!Keyboard.current[resetAllSavesKey].wasPressedThisFrame) return;

        // 설정창·기록 선택 화면이 떠서 시간이 멈춰 있어도 같은 간격으로 재야 하므로 unscaledTime 을 쓴다.
        if (Time.unscaledTime - lastResetKeyTime > resetAllSavesWindow) {
            lastResetKeyTime = Time.unscaledTime;
            Debug.Log($"[MainMenuController] 저장된 기억을 전부 지우려면 {resetAllSavesWindow}초 안에 [{resetAllSavesKey}] 키를 한 번 더 누르세요.", this);
            return;
        }

        lastResetKeyTime = float.NegativeInfinity; // 세 번째 입력이 곧바로 또 지우지 않도록 대기 상태를 비운다.
        ResetAllSaves();
    }

    void ResetAllSaves() {
        if (SaveManager.Instance == null) {
            Debug.LogWarning("[MainMenuController] SaveManager 가 없어 기억을 초기화하지 못했습니다. " +
                "메인 메뉴 씬에 SaveManager 가 있어야 합니다.", this);
            return;
        }

        int deleted = SaveManager.Instance.DeleteAllSlots();
        Debug.Log($"[MainMenuController] 저장된 기억 {deleted}개를 지웠습니다. ({Application.persistentDataPath})", this);

        // 화면이 옛 기록을 그대로 들고 있으면 이미 지워진 자리를 골라 빈 파일을 여는 꼴이 된다.
        // 로비와 기록 선택 화면은 씬에 하나씩만 있으므로 찾아서 다시 읽게 한다(꺼져 있는 기록 선택 화면도 찾아야 해서 Include).
        MainLobbyView lobby = FindFirstObjectByType<MainLobbyView>(FindObjectsInactive.Include);
        if (lobby != null) lobby.RefreshSaveState();

        SaveSlotSelectView slots = FindFirstObjectByType<SaveSlotSelectView>(FindObjectsInactive.Include);
        if (slots != null) slots.Refresh();
    }

    #endregion
    #region 설정창 관련 함수

    // [Setting] 버튼 클릭 시 호출.
    public void OpenSettings() {
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    void CloseSettings() {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    #endregion
    #region 씬 전환 관련 함수

    // [Game Start] 버튼 클릭 시 호출.
    // 씬 로드를 직접 하지 않고 ScreenFader를 거친다 — 화면이 검게 덮인 뒤에 프롤로그가 시작되어야 하기 때문.
    // 씬에 ScreenFader가 없으면 ScreenFader.LoadScene이 알아서 연출 없이 바로 넘긴다.
    public void StartGame() {
        ScreenFader.LoadScene(startSceneName);
    }

    // [Exit] 버튼 클릭 시 호출.
    public void ExitGame() {
        Debug.Log("게임 종료!");
        Application.Quit(); // 에디터에서는 동작하지 않고 빌드에서만 실제로 종료됨.
    }

    #endregion
}
