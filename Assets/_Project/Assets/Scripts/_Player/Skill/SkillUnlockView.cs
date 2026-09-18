using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 스토리에서 새 스킬을 되찾았을 때 화면 가운데 뜨는 전체화면 알림. 어떤 스킬을 얻었는지 보여주고
// Enter 를 눌러야 닫힌다 — AreaTitleView(지역 이름, 자동으로 슬라이드 인/아웃하며 조작을 막지 않는다)와
// 달리 플레이어가 직접 확인해야 넘어가는 화면이라 열려 있는 동안 이동을 잠근다(TutorialTriggerZone과 같은 이유).
//
// 화면 요소는 전부 프리팹 Prefabs/UI/SkillUnlockUI.prefab 에 들어 있고, 이 스크립트는 표시·입력 대기만 맡는다.
//
// **표시가 필요한 씬마다 SkillUnlockUI 프리팹을 하나씩 놓으세요.** 겹쳐도 Awake 에서 정리한다
// (AreaTitleView·ScreenFader와 같은 싱글턴 방식).
public class SkillUnlockView : MonoBehaviour {
    public static SkillUnlockView Instance;

    #region 인스펙터 변수

    [Header("연결 — 프리팹이 채워 둔 값입니다")]
    public GameObject windowRoot;  // 통째로 켜고 끄는 창.
    public TMP_Text subtitleLabel; // 스킬 이름.
    public Image iconImage;        // 스킬 아이콘. 없으면 monogramLabel 로 대신 그린다 (HudSkillSlotView와 같은 방식).
    public TMP_Text monogramLabel; // 아이콘이 없는 스킬의 머리글자.

    [Header("잠금")]
    public bool lockPlayerMovement = true; // 열려 있는 동안 이동을 잠근다.

    #endregion
    #region 런타임 변수

    bool waitingForClose;
    Player_move lockedMove; // 닫을 때 풀어줄 대상. 여는 시점에 넘겨받은 것을 그대로 기억해 둔다.

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        // 파괴된 뒤에도 C# 참조가 남을 수 있어 ?. 대신 != null 로 Unity의 == 오버로드를 탄다.
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        // DontDestroyOnLoad 는 루트 오브젝트에서만 동작한다. 정리용으로 다른 오브젝트 밑에 넣어뒀을
        // 수 있으므로 먼저 떼어낸다 (SaveManager와 같은 이유).
        transform.SetParent(null);
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!ValidateReferences()) return;
        windowRoot.SetActive(false);
    }

    void OnDestroy() {
        if (Instance == this) Instance = null;
    }

    void Update() {
        if (!waitingForClose) return;
        if (!WasEnterPressed()) return;

        Close();
    }

    #endregion
    #region 표시

    // 어디서든 부르는 진입점. 씬에 프리팹이 없어도 게임이 멈추지 않도록 조용히 지나간다 (AreaTitleView.Announce와 같은 방침).
    // playerMove 는 닫힐 때 이동 잠금을 풀어줄 대상이다 — 씬마다 플레이어가 새로 놓이므로 부르는 쪽(SkillManager)이
    // 이미 들고 있는 참조를 그대로 넘겨받는다.
    public static void Announce(SkillBase skill, Player_move playerMove) {
        if (skill == null) return;

        if (Instance != null) {
            Instance.Show(skill, playerMove);
            return;
        }

        Debug.LogWarning($"[SkillUnlockView] 씬에 SkillUnlockUI 프리팹이 없어 '{skill.DisplayName}' 해금 알림을 건너뜁니다. " +
            "Prefabs/UI/SkillUnlockUI 프리팹을 씬에 놓으세요.");
    }

    // 컷씬(SkillUnlockStep)이 화면이 닫히기를 기다렸다가 다음 단계로 넘어갈 때 쓴다.
    public static bool IsShowing => Instance != null && Instance.waitingForClose;

    public void Show(SkillBase skill, Player_move playerMove) {
        if (windowRoot == null || subtitleLabel == null) return;

        subtitleLabel.text = skill.DisplayName;

        bool hasIcon = skill.icon != null;
        if (iconImage != null) {
            iconImage.sprite = hasIcon ? skill.icon : null;
            iconImage.enabled = hasIcon;
        }
        if (monogramLabel != null) {
            monogramLabel.gameObject.SetActive(!hasIcon);
            if (!hasIcon) monogramLabel.text = Monogram(skill.DisplayName);
        }

        windowRoot.SetActive(true);
        waitingForClose = true;

        lockedMove = lockPlayerMovement ? playerMove : null;
        if (lockedMove != null) lockedMove.isMovementLocked = true;
    }

    void Close() {
        waitingForClose = false;
        windowRoot.SetActive(false);

        if (lockedMove != null) lockedMove.isMovementLocked = false;
        lockedMove = null;
    }

    static bool WasEnterPressed() {
        Keyboard kb = Keyboard.current;
        return kb != null && (kb[Key.Enter].wasPressedThisFrame || kb[Key.NumpadEnter].wasPressedThisFrame);
    }

    // 단어 머리글자 최대 두 자 (HudSkillSlotView.Monogram과 같은 로직).
    static string Monogram(string displayName) {
        if (string.IsNullOrWhiteSpace(displayName)) return "?";

        string[] words = displayName.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2) return (words[0].Substring(0, 1) + words[1].Substring(0, 1)).ToUpperInvariant();

        string word = words[0];
        return (word.Length >= 2 ? word.Substring(0, 2) : word).ToUpperInvariant();
    }

    #endregion
    #region 검사

    bool ValidateReferences() {
        List<string> missing = new();
        if (windowRoot == null) missing.Add(nameof(windowRoot));
        if (subtitleLabel == null) missing.Add(nameof(subtitleLabel));

        if (missing.Count == 0) return true;

        Debug.LogError($"[SkillUnlockView] 프리팹 연결이 비어 있어 스킬 해금 알림을 쓸 수 없습니다 — {string.Join(", ", missing)}. " +
            "Prefabs/UI/SkillUnlockUI 프리팹을 씬에 놓으세요.", this);
        return false;
    }

    #endregion
}
