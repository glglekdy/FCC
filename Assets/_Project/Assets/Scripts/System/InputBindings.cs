using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;

// 키 재지정 줄이 어느 칸을 만지는지. 키보드와 마우스는 한 칸으로 본다 — PC 기본 조작이 둘을 섞어 쓰기 때문이다(공격 = 마우스 좌클릭).
public enum BindingDevice {
    Keyboard,
    Gamepad,
}

// 키 재지정의 동작 대응표 · 키 이름 · 실제 반영을 맡는다.
//
// 키를 바꾸는 방법은 Input System 의 바인딩 덮어쓰기(binding override) 하나뿐이다. Client.inputactions 원본은 그대로 두고
// "이 바인딩은 이 키로" 라는 덮어쓰기만 얹는다. 그래서 기본값 복원은 덮어쓰기를 지우는 것으로 끝나고, 저장도
// SaveBindingOverridesAsJson 이 만든 문자열 하나(SettingsData.bindingOverrides)로 끝난다.
//
// 설정창은 게임이 실제로 쓰는 에셋이 아니라 **편집용 사본**을 만진다. 이유는 둘이다.
//   1. 「적용」을 누르기 전까지는 게임의 키가 바뀌면 안 된다(「취소」가 의미를 잃는다).
//   2. PerformInteractiveRebinding 은 켜져 있는 액션에 걸 수 없다. 일시정지 메뉴(PauseMenuView)가 열려 있는 동안은
//      플레이어 입력을 꺼 두지만, 게임이 쓰는 에셋의 액션이 언제 켜져 있을지를 설정창이 일일이 따질 수는 없다.
//
// 게임 쪽 반영: GameSettings.Apply → ApplyOverrides → 등록된 에셋 전부에 덮어쓰기를 다시 건다.
// 에셋 등록은 씬이 열릴 때 PlayerInput.all 에서, 그리고 설정창 · SkillManager 가 가진 참조로 한다.
public static class InputBindings {
    #region 대응표

    // 컨트롤 탭의 한 줄.
    public class Entry {
        public readonly string label; // 화면에 보이는 동작 이름.
        public readonly string action; // "맵/액션". 비어 있으면 코드가 키를 직접 읽는 고정 조작이다.
        public readonly string part; // 합성 바인딩(Move)의 조각 이름. 일반 바인딩이면 null.
        public readonly string fixedKeyboard; // 고정 조작의 키 이름.
        public readonly string fixedGamepad;

        public Entry(string label, string action, string part = null, string fixedKeyboard = null, string fixedGamepad = null) {
            this.label = label;
            this.action = action;
            this.part = part;
            this.fixedKeyboard = fixedKeyboard;
            this.fixedGamepad = fixedGamepad;
        }

        public bool IsFixed => string.IsNullOrEmpty(action);

        // 키가 겹쳤는지 볼 범위. 맵이 다르면 서로 다른 상황에서만 쓰이므로 겹쳐도 된다(SwapConflicts 참고).
        public string MapName => IsFixed ? null : action.Substring(0, Mathf.Max(0, action.IndexOf('/')));
    }

    // 순서가 곧 SettingsKeybindRow.actionIndex 다. **중간에 끼우거나 지우면 SettingsPanel 프리팹 각 줄의 actionIndex 도 함께 당겨야 합니다.**
    // 새 동작은 맨 뒤에 추가하세요.
    public static readonly Entry[] Entries = {
        new Entry("왼쪽 이동", "Player/Move", "left"),
        new Entry("오른쪽 이동", "Player/Move", "right"),
        new Entry("점프", "Player/Jump"),
        new Entry("공격", "Player/Attack"),
        new Entry("상호작용", "Player/Interact"),
        new Entry("스킬 1", "Player/Skill1"),
        new Entry("스킬 2", "Player/Skill2"),
        new Entry("스킬 3", "Player/Skill3"),
        new Entry("대사 넘김", "Ui/NextDialogue"),
        // ESC 는 일시정지 · 창 닫기 · 재지정 취소를 한꺼번에 맡고 있어 바꿀 수 없게 둔다(PauseMenuView 가 직접 읽는다).
        new Entry("일시정지", null, fixedKeyboard: "ESC"),
    };

    // 화면에 적는 키 이름. 등록되지 않은 키는 Input System 이 주는 이름(F, Up Arrow 등)을 그대로 쓴다.
    // 키는 ControlKey() 형식 — 기기 계열 + 컨트롤 경로, 소문자.
    static readonly Dictionary<string, string> Names = new() {
        { "keyboard/escape", "ESC" },
        { "keyboard/space", "Space" },
        { "keyboard/enter", "Enter" },
        { "keyboard/tab", "Tab" },
        { "keyboard/backspace", "Backspace" },
        { "keyboard/leftshift", "L Shift" },
        { "keyboard/rightshift", "R Shift" },
        { "keyboard/leftctrl", "L Ctrl" },
        { "keyboard/rightctrl", "R Ctrl" },
        { "keyboard/leftalt", "L Alt" },
        { "keyboard/rightalt", "R Alt" },
        { "mouse/leftbutton", "마우스 좌클릭" },
        { "mouse/rightbutton", "마우스 우클릭" },
        { "mouse/middlebutton", "마우스 휠 클릭" },
        { "mouse/forwardbutton", "마우스 앞 버튼" },
        { "mouse/backbutton", "마우스 뒤 버튼" },
        { "gamepad/buttonsouth", "A 버튼" },
        { "gamepad/buttoneast", "B 버튼" },
        { "gamepad/buttonwest", "X 버튼" },
        { "gamepad/buttonnorth", "Y 버튼" },
        { "gamepad/leftshoulder", "LB" },
        { "gamepad/rightshoulder", "RB" },
        { "gamepad/lefttrigger", "LT" },
        { "gamepad/righttrigger", "RT" },
        { "gamepad/start", "Start" },
        { "gamepad/select", "Back" },
        { "gamepad/leftstickpress", "L스틱 누름" },
        { "gamepad/rightstickpress", "R스틱 누름" },
        { "gamepad/dpad/up", "십자키 위" },
        { "gamepad/dpad/down", "십자키 아래" },
        { "gamepad/dpad/left", "십자키 왼쪽" },
        { "gamepad/dpad/right", "십자키 오른쪽" },
        { "gamepad/leftstick/up", "L스틱 위" },
        { "gamepad/leftstick/down", "L스틱 아래" },
        { "gamepad/leftstick/left", "L스틱 왼쪽" },
        { "gamepad/leftstick/right", "L스틱 오른쪽" },
    };

    // Names 중 한국어 단어인 것만 Ui 테이블 키로 다시 잇는다. ESC · Space · LB · A 버튼처럼 이미 영문/기호인
    // 항목은 번역할 말이 없어 Names 에만 남긴다.
    static readonly Dictionary<string, LocalizedString> LocalizedKeyNames = new() {
        { "mouse/leftbutton", new LocalizedString("Ui", "keybind.key.mouse_left") },
        { "mouse/rightbutton", new LocalizedString("Ui", "keybind.key.mouse_right") },
        { "mouse/middlebutton", new LocalizedString("Ui", "keybind.key.mouse_middle") },
        { "mouse/forwardbutton", new LocalizedString("Ui", "keybind.key.mouse_forward") },
        { "mouse/backbutton", new LocalizedString("Ui", "keybind.key.mouse_back") },
        { "gamepad/buttonsouth", new LocalizedString("Ui", "keybind.key.gamepad_south") },
        { "gamepad/buttoneast", new LocalizedString("Ui", "keybind.key.gamepad_east") },
        { "gamepad/buttonwest", new LocalizedString("Ui", "keybind.key.gamepad_west") },
        { "gamepad/buttonnorth", new LocalizedString("Ui", "keybind.key.gamepad_north") },
        { "gamepad/leftstickpress", new LocalizedString("Ui", "keybind.key.left_stick_press") },
        { "gamepad/rightstickpress", new LocalizedString("Ui", "keybind.key.right_stick_press") },
        { "gamepad/dpad/up", new LocalizedString("Ui", "keybind.key.dpad_up") },
        { "gamepad/dpad/down", new LocalizedString("Ui", "keybind.key.dpad_down") },
        { "gamepad/dpad/left", new LocalizedString("Ui", "keybind.key.dpad_left") },
        { "gamepad/dpad/right", new LocalizedString("Ui", "keybind.key.dpad_right") },
        { "gamepad/leftstick/up", new LocalizedString("Ui", "keybind.key.left_stick_up") },
        { "gamepad/leftstick/down", new LocalizedString("Ui", "keybind.key.left_stick_down") },
        { "gamepad/leftstick/left", new LocalizedString("Ui", "keybind.key.left_stick_left") },
        { "gamepad/leftstick/right", new LocalizedString("Ui", "keybind.key.left_stick_right") },
    };

    // HUD 스킬 칸처럼 한두 글자 자리에 적는 짧은 이름. 없으면 Names 의 이름을 쓴다.
    static readonly Dictionary<string, string> ShortNames = new() {
        { "keyboard/space", "SPC" },
        { "keyboard/enter", "ENT" },
        { "keyboard/tab", "TAB" },
        { "keyboard/backspace", "BS" },
        { "keyboard/leftshift", "SHF" },
        { "keyboard/rightshift", "SHF" },
        { "keyboard/leftctrl", "CTL" },
        { "keyboard/rightctrl", "CTL" },
        { "keyboard/leftalt", "ALT" },
        { "keyboard/rightalt", "ALT" },
        { "mouse/leftbutton", "M1" },
        { "mouse/rightbutton", "M2" },
        { "mouse/middlebutton", "M3" },
        { "mouse/forwardbutton", "M4" },
        { "mouse/backbutton", "M5" },
    };

    #endregion
    #region 상태

    // 게임이 실제로 쓰는 에셋들. 보통은 Client.inputactions 하나지만, PlayerInput 이 사본을 만드는 경우(플레이어가 둘 이상)까지
    // 맞추려고 목록으로 둔다. static 으로 붙잡고 있는 것 자체가 씬 전환 때 에셋이 내려가 덮어쓰기를 잃는 것도 막아 준다.
    static readonly List<InputActionAsset> liveAssets = new();
    static string appliedOverrides; // 마지막으로 확정된 덮어쓰기. 나중에 등록되는 에셋에도 같은 값을 건다.
    static bool hasApplied;

    static InputActionAsset editCopy; // 설정창 전용 사본.
    static string editCopyOverrides; // 사본에 지금 걸려 있는 덮어쓰기. 넘겨받은 값과 다를 때만 다시 건다.

    static InputActionRebindingExtensions.RebindingOperation rebind;

    // 설정창이 키 입력을 기다리는 중인지. 그동안 화면 조작(탭 전환 · 줄 이동)은 쉬어야 한다.
    public static bool IsRebinding => rebind != null;

    // 확정된 키가 게임에 반영된 뒤. 키 이름을 적어 둔 화면(HUD 스킬 칸 · 상호작용 안내)이 다시 그린다.
    // **static 이벤트이므로 OnDestroy 에서 반드시 구독을 해제하세요.**
    public static event Action OnApplied;

    #endregion
    #region 초기화

    // 이 프로젝트는 플레이 진입 시 도메인 리로드를 끄고 있어(Enter Play Mode Options) static 값이 직전 판에서 그대로 넘어온다.
    // 파괴된 에셋 · 끊긴 이벤트 구독이 남지 않도록 매 판 시작마다 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        rebind = null;
        liveAssets.Clear();
        appliedOverrides = null;
        hasApplied = false;
        editCopy = null;
        editCopyOverrides = null;
        OnApplied = null;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Application.quitting -= HandleQuitting;
        Application.quitting += HandleQuitting;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) {
        RegisterPlayers();
    }

    // 에디터에서는 플레이를 끝내도 메모리의 에셋이 덮어쓰기를 들고 남는다. 다음 판이 시작할 때 다시 걸긴 하지만,
    // 그 사이 입력 에셋 창에서 바뀐 키가 보이면 원본이 바뀐 것으로 착각하기 쉬워 여기서 걷어 둔다.
    static void HandleQuitting() {
        foreach (InputActionAsset asset in liveAssets) {
            if (asset != null) asset.RemoveAllBindingOverrides();
        }
    }

    static void RegisterPlayers() {
        foreach (PlayerInput player in PlayerInput.all) {
            if (player != null) Register(player.actions);
        }
    }

    // 게임이 쓰는 에셋을 알린다. 여러 번 불러도 안전하다.
    public static void Register(InputActionAsset asset) {
        if (asset == null) return;

        liveAssets.RemoveAll(a => a == null); // PlayerInput 이 만든 사본은 플레이어와 함께 파괴된다. Unity 의 == 로 걸러낸다.
        if (liveAssets.Contains(asset)) return;

        liveAssets.Add(asset);
        if (hasApplied) Load(asset, appliedOverrides);
    }

    #endregion
    #region 게임에 반영

    // GameSettings 가 확정된 값을 넘긴다(부팅 · 「적용」).
    public static void ApplyOverrides(string overrides) {
        appliedOverrides = overrides ?? string.Empty;
        hasApplied = true;

        RegisterPlayers();
        liveAssets.RemoveAll(a => a == null);
        foreach (InputActionAsset asset in liveAssets) Load(asset, appliedOverrides);

        if (OnApplied != null) OnApplied();
    }

    static void Load(InputActionAsset asset, string overrides) {
        // 덮어쓰기를 전부 지운 뒤 건다. 지우지 않으면 기본값으로 되돌린 바인딩에 옛 키가 남는다.
        asset.RemoveAllBindingOverrides();
        if (string.IsNullOrEmpty(overrides)) return;

        try {
            asset.LoadBindingOverridesFromJson(overrides, false);
        } catch (Exception e) {
            // 저장값이 깨졌다고 조작이 통째로 막히면 안 된다. 기본 키로 두고 알리기만 한다.
            asset.RemoveAllBindingOverrides();
            Debug.LogWarning("[InputBindings] 저장된 키 설정을 읽지 못해 기본 키를 씁니다 — " + e.Message);
        }
    }

    #endregion
    #region 설정창 — 표시

    // 설정창 줄이 보여줄 키 이름. 해제했거나 바인딩이 없으면 빈 문자열.
    public static string DisplayName(int entryIndex, BindingDevice device, string overrides) {
        Entry entry = EntryAt(entryIndex);
        if (entry == null) return string.Empty;
        if (entry.IsFixed) return FixedName(entry, device);

        if (!TryFind(EditCopy(overrides), entryIndex, device, out InputAction action, out int index)) return string.Empty;
        return Describe(action.bindings[index].effectivePath);
    }

    // 다시 지정할 수 있는 칸인지. 고정 조작 · 입력 에셋에 바인딩이 없는 칸(이동의 패드 칸 등)은 막는다.
    public static bool CanRebind(int entryIndex, BindingDevice device, string overrides) {
        return TryFind(EditCopy(overrides), entryIndex, device, out _, out _);
    }

    // 덮어쓰기를 무시한 입력 에셋 원래의 키 이름. 에디터의 프리팹 빌더가 칸에 자리 표시로 적는다.
    public static string DefaultName(InputActionAsset asset, int entryIndex, BindingDevice device) {
        Entry entry = EntryAt(entryIndex);
        if (entry == null) return string.Empty;
        if (entry.IsFixed) return FixedName(entry, device);

        if (!TryFind(asset, entryIndex, device, out InputAction action, out int index)) return string.Empty;
        return Describe(action.bindings[index].path);
    }

    // 설정창이 닫힐 때. 사본은 다음에 열 때 확정값에서 다시 만든다.
    public static void ReleaseEditCopy() {
        CancelRebind();

        if (editCopy != null) UnityEngine.Object.Destroy(editCopy);
        editCopy = null;
        editCopyOverrides = null;
    }

    static InputActionAsset EditCopy(string overrides) {
        if (editCopy == null) {
            InputActionAsset source = FirstLiveAsset();
            if (source == null) return null;

            // Instantiate 는 바인딩 id 를 그대로 복사한다. 사본에서 만든 덮어쓰기를 원본에 그대로 걸 수 있는 이유다.
            editCopy = UnityEngine.Object.Instantiate(source);
            editCopy.name = source.name + " (설정 편집용)";
            editCopyOverrides = null;
        }

        overrides ??= string.Empty;
        if (editCopyOverrides != overrides) {
            Load(editCopy, overrides);
            editCopyOverrides = overrides;
        }
        return editCopy;
    }

    static InputActionAsset FirstLiveAsset() {
        liveAssets.RemoveAll(a => a == null);
        return liveAssets.Count > 0 ? liveAssets[0] : null;
    }

    #endregion
    #region 설정창 — 다시 지정

    // 키 입력을 기다린다. 새 키를 누르면 바뀐 덮어쓰기 문자열을 onDone 에, ESC 로 빠져나오면 onCancel 을 부른다.
    // 한 번에 한 칸만 기다린다 — 다른 칸이 기다리던 중이면 그쪽은 취소된다.
    public static bool StartRebind(int entryIndex, BindingDevice device, string overrides, Action<string> onDone, Action onCancel) {
        CancelRebind();

        InputActionAsset copy = EditCopy(overrides);
        if (!TryFind(copy, entryIndex, device, out InputAction action, out int index)) return false;

        string before = action.bindings[index].effectivePath;

        InputActionRebindingExtensions.RebindingOperation operation = action.PerformInteractiveRebinding(index)
            .WithExpectedControlType("Button")
            // ESC 는 일시정지 · 창 닫기에 묶여 있어 어느 동작에도 줄 수 없다. 대기를 빠져나오는 키로만 쓴다.
            .WithCancelingThrough("<Keyboard>/escape")
            // anyKey 는 아무 키나 누르면 함께 눌리는 가상 버튼이다. 여기에 잡히면 모든 키가 그 동작이 된다.
            .WithControlsExcluding("<Keyboard>/anyKey")
            // 휠은 한 칸 굴릴 때마다 눌렸다 떼어지는 값이라, 누르고 있는 동안 동작하는 버튼 조작에 맞지 않는다.
            .WithControlsExcluding("<Mouse>/scroll");

        if (device == BindingDevice.Keyboard) {
            operation.WithControlsHavingToMatchPath("<Keyboard>").WithControlsHavingToMatchPath("<Mouse>");
        }
        else {
            operation.WithControlsHavingToMatchPath("<Gamepad>");
        }

        // 콜백 안에서 Dispose 한다(Input System 샘플과 같은 방식). 취소는 반드시 Cancel() 을 거쳐야 한다 —
        // Dispose 만 하면 대기 중에 바꿔 둔 "입력 삼키기" 설정이 복구되지 않아 이후 입력이 이상해진다.
        operation
            .OnComplete(op => {
                rebind = null;
                op.Dispose();
                string result = FinishRebind(copy, entryIndex, device, action, index, before);
                if (onDone != null) onDone(result);
            })
            .OnCancel(op => {
                rebind = null;
                op.Dispose();
                if (onCancel != null) onCancel();
            });

        rebind = operation;
        operation.Start();
        return true;
    }

    public static void CancelRebind() {
        if (rebind != null) rebind.Cancel(); // OnCancel 콜백이 rebind 를 비우고 정리한다.
    }

    // 키보드 칸의 할당을 비운다(Backspace). 빈 경로로 덮어쓰면 그 바인딩은 어떤 키에도 반응하지 않는다.
    public static string ClearBinding(int entryIndex, BindingDevice device, string overrides) {
        InputActionAsset copy = EditCopy(overrides);
        if (!TryFind(copy, entryIndex, device, out InputAction action, out int index)) return overrides;

        action.ApplyBindingOverride(index, string.Empty);
        editCopyOverrides = copy.SaveBindingOverridesAsJson();
        return editCopyOverrides;
    }

    static string FinishRebind(InputActionAsset copy, int entryIndex, BindingDevice device, InputAction action, int index, string before) {
        string after = action.bindings[index].effectivePath;

        // 원래 키로 되돌린 경우 덮어쓰기를 남기지 않는다. 남겨 두면 나중에 입력 에셋의 기본 키를 바꿔도 이 PC 에는 반영되지 않는다.
        if (SameControl(after, action.bindings[index].path)) action.RemoveBindingOverride(index);

        if (!string.IsNullOrEmpty(after) && !SameControl(after, before)) SwapConflicts(copy, entryIndex, device, after, before);

        editCopyOverrides = copy.SaveBindingOverridesAsJson();
        return editCopyOverrides;
    }

    // 같은 맵 안에서 새 키를 이미 쓰던 동작이 있으면, 그 동작에 방금 비운 키를 넘겨준다(서로 맞바꾼다).
    // 막기만 하면 "Q 와 E 를 맞바꾸기" 가 해제를 두 번 거쳐야 해서 번거롭고, 그냥 두면 키 하나가 두 동작을 한꺼번에 일으킨다.
    // 맵이 다르면 겹쳐도 둔다 — 대사 넘김(Ui)과 점프(Player)가 둘 다 Space 인 것처럼, 서로 다른 상황에서만 쓰이기 때문이다.
    static void SwapConflicts(InputActionAsset asset, int changedEntry, BindingDevice device, string takenPath, string freedPath) {
        string map = Entries[changedEntry].MapName;

        for (int i = 0; i < Entries.Length; i++) {
            if (i == changedEntry || Entries[i].IsFixed || Entries[i].MapName != map) continue;
            if (!TryFind(asset, i, device, out InputAction other, out int otherIndex)) continue;

            InputBinding binding = other.bindings[otherIndex];
            if (!SameControl(binding.effectivePath, takenPath)) continue;

            if (SameControl(freedPath, binding.path)) other.RemoveBindingOverride(otherIndex);
            else other.ApplyBindingOverride(otherIndex, freedPath ?? string.Empty);
        }
    }

    #endregion
    #region 게임 화면용 키 이름

    // 게임이 실제로 쓰는 액션의 키 이름. HUD 스킬 칸 · 상호작용 안내처럼 게임 화면에 키를 적는 곳이 쓴다.
    // compact 이면 좁은 칸용 짧은 이름(Space → SPC)을 쓴다. 바인딩이 없거나 해제했으면 빈 문자열.
    public static string ActionKeyName(InputAction action, BindingDevice device = BindingDevice.Keyboard, bool compact = false) {
        if (action == null) return string.Empty;

        int index = FindBinding(action, null, device);
        return index < 0 ? string.Empty : Describe(action.bindings[index].effectivePath, compact);
    }

    #endregion
    #region 바인딩 찾기

    static Entry EntryAt(int entryIndex) {
        return entryIndex >= 0 && entryIndex < Entries.Length ? Entries[entryIndex] : null;
    }

    static string FixedName(Entry entry, BindingDevice device) {
        return (device == BindingDevice.Keyboard ? entry.fixedKeyboard : entry.fixedGamepad) ?? string.Empty;
    }

    static bool TryFind(InputActionAsset asset, int entryIndex, BindingDevice device, out InputAction action, out int bindingIndex) {
        action = null;
        bindingIndex = -1;

        Entry entry = EntryAt(entryIndex);
        if (asset == null || entry == null || entry.IsFixed) return false;

        action = asset.FindAction(entry.action);
        if (action == null) return false;

        bindingIndex = FindBinding(action, entry.part, device);
        return bindingIndex >= 0;
    }

    // 액션에서 그 기기의 첫 바인딩을 찾는다. 이동의 방향키 조합처럼 두 번째 바인딩은 보조로 남겨 두고 건드리지 않는다.
    //
    // 바인딩 id 가 아니라 "조각 이름 + 기기" 로 찾는 이유: 입력 에셋에서 바인딩을 지우고 다시 만들면 id 가 바뀌는데,
    // 그때 대응표가 조용히 끊기지 않게 하기 위해서다.
    static int FindBinding(InputAction action, string part, BindingDevice device) {
        bool wantPart = !string.IsNullOrEmpty(part);
        var bindings = action.bindings;

        for (int i = 0; i < bindings.Count; i++) {
            InputBinding binding = bindings[i];
            if (binding.isComposite || binding.isPartOfComposite != wantPart) continue;
            if (wantPart && !string.Equals(binding.name, part, StringComparison.OrdinalIgnoreCase)) continue;

            // 원래 경로(path)로 기기를 가린다. 덮어쓴 경로(effectivePath)로 보면 해제한 바인딩("")이 어느 칸이었는지 잃는다.
            if (DeviceOf(binding.path) != device) continue;
            return i;
        }
        return -1;
    }

    static BindingDevice? DeviceOf(string path) {
        switch (FamilyOf(path)) {
            case "keyboard":
            case "mouse":
                return BindingDevice.Keyboard;
            case "gamepad":
                return BindingDevice.Gamepad;
            default:
                return null;
        }
    }

    // 경로의 기기 계열. <XInputController>/buttonSouth 와 <Gamepad>/buttonSouth 를 같은 버튼으로 보려고 계열로 묶는다.
    static string FamilyOf(string path) {
        if (string.IsNullOrEmpty(path)) return null;

        string layout = InputControlPath.TryGetDeviceLayout(path);
        if (string.IsNullOrEmpty(layout)) return null;

        if (IsBasedOn(layout, "Keyboard")) return "keyboard";
        if (IsBasedOn(layout, "Mouse")) return "mouse";
        // XInputController 는 Windows 에서만 레이아웃이 등록된다. 다른 OS 의 에디터에서도 패드 칸을 찾도록 이름으로도 본다.
        if (IsBasedOn(layout, "Gamepad") || layout.StartsWith("XInput", StringComparison.OrdinalIgnoreCase)) return "gamepad";
        return layout.ToLowerInvariant();
    }

    static bool IsBasedOn(string layout, string baseLayout) {
        return string.Equals(layout, baseLayout, StringComparison.OrdinalIgnoreCase)
            || InputSystem.IsFirstLayoutBasedOnSecond(layout, baseLayout);
    }

    // "keyboard/f" · "gamepad/buttonsouth" 처럼 기기 계열 + 컨트롤 경로. 겹침 검사와 이름표 조회에 쓴다.
    static string ControlKey(string path) {
        if (string.IsNullOrEmpty(path)) return null;

        int slash = path.IndexOf('/');
        if (slash < 0) return null;

        return FamilyOf(path) + path.Substring(slash).ToLowerInvariant();
    }

    static bool SameControl(string a, string b) {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b);
        return string.Equals(ControlKey(a) ?? a, ControlKey(b) ?? b, StringComparison.OrdinalIgnoreCase);
    }

    static string Describe(string path, bool compact = false) {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        string key = ControlKey(path);
        if (key != null) {
            if (LocalizedKeyNames.TryGetValue(key, out LocalizedString localized)) {
                string resolved = LocalizationText.Resolve(localized, null);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
            }
            if (compact && ShortNames.TryGetValue(key, out string shortName)) return shortName;
            if (Names.TryGetValue(key, out string name)) return name;
        }
        return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
    }

    #endregion
}
