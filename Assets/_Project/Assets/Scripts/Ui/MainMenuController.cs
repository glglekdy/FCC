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

    #endregion
    #region 컴포넌트 변수

    InputAction escapeAction;

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
