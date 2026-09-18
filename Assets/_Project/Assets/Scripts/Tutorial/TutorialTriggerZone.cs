using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 튜토리얼 한 묶음을 재생하는 컴포넌트. 플레이어가 이 오브젝트의 Collider2D(Is Trigger) 안에 들어오면
// 재생된다 (DialogueTriggerZone의 영역 진입형과 같은 구조). 재생 루프 자체는 TutorialPlayer를 쓴다.
//
// 튜토리얼은 이 컴포넌트의 인스펙터에서 직접 편집한다 — 칸마다 이름(부제) · 멘트 · 이미지.
// 이름·멘트는 String Table 'Tutorial'의 키를 가리키는 LocalizedString이다.
public class TutorialTriggerZone : MonoBehaviour {
    #region 인스펙터 변수

    [Header("연결")]
    public TutorialView view; // 튜토리얼을 띄울 창. **InGameUi 프리팹의 TutorialUI를 연결하세요.**
    public InputActionReference advanceAction; // 다음 입력 (Client ▸ Ui ▸ NextDialogue 재사용).

    [Header("재생 중 가릴 화면")]
    // 재생 중 꺼둘 다른 UI 뿌리들. **InGameUi 밑의 PlayerHud · ObjectiveChecklist 등을 연결하세요.**
    // 비워두면 아무것도 끄지 않는다. 재생이 끝나면 껐던 것만 다시 켠다(이미 꺼져 있던 화면까지
    // 억지로 켜면 목표가 없어 숨어 있던 체크리스트 같은 것이 튜토리얼 뒤에 잘못 튀어나온다).
    public GameObject[] hideDuringTutorial;

    [Header("튜토리얼")]
    public string tutorialId; // 세이브에 기록할 고유 id. **비어 있으면 매번 다시 재생됩니다.**
    public List<TutorialEntry> entries = new();

    [Header("진입 조건")]
    public string playerTag = "Player";

    [Header("진행 중 처리")]
    public bool lockPlayerMovement = true; // 튜토리얼 중 이동 잠금 (Player_move.isMovementLocked 재사용).

    #endregion
    #region 런타임 변수

    static bool isPlaying; // 튜토리얼 두 묶음이 겹쳐 재생되지 않도록 씬 전체에서 하나만 돌게 한다.

    // 프로젝트가 Enter Play Mode Options에서 Domain Reload를 꺼두고 있어, static 필드가 Play를
    // 멈춰도 초기화되지 않는다 (DialogueTriggerZone.ResetStaticStateOnPlay와 같은 이유).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetStaticStateOnPlay() {
        isPlaying = false;
    }

    #endregion
    #region 유니티 라이프 사이클

    void OnTriggerEnter2D(Collider2D other) {
        if (!other.CompareTag(playerTag)) return;
        if (isPlaying) return;
        if (!string.IsNullOrEmpty(tutorialId) && TutorialManager.Instance != null && TutorialManager.Instance.HasShown(tutorialId)) return;

        StartCoroutine(RunTutorial(other.GetComponent<Player_move>()));
    }

    #endregion
    #region 재생

    IEnumerator RunTutorial(Player_move playerMove) {
        if (view == null) {
            Debug.LogError($"[TutorialTriggerZone] '{name}' 의 View 칸이 비어 있어 튜토리얼을 재생할 수 없습니다.", this);
            yield break;
        }
        if (entries.Count == 0) {
            Debug.LogWarning($"[TutorialTriggerZone] '{name}' 의 Entries 리스트가 비어 있습니다.", this);
            yield break;
        }
        if (isPlaying) yield break; // 같은 프레임에 다른 존과 겹치는 경우를 막는다.

        isPlaying = true;

        if (!lockPlayerMovement) playerMove = null;
        if (playerMove != null) playerMove.isMovementLocked = true;

        bool[] wasActive = HideOtherUi();

        yield return TutorialPlayer.Play(view, entries, advanceAction);

        RestoreOtherUi(wasActive);

        if (playerMove != null) playerMove.isMovementLocked = false;

        isPlaying = false;

        if (!string.IsNullOrEmpty(tutorialId) && TutorialManager.Instance != null) {
            TutorialManager.Instance.MarkShown(tutorialId);
        }
    }

    // 끄기 직전 상태를 기록해 둔다 — 되돌릴 때 이미 꺼져 있던 화면까지 켜지 않기 위함이다.
    bool[] HideOtherUi() {
        if (hideDuringTutorial == null || hideDuringTutorial.Length == 0) return null;

        bool[] wasActive = new bool[hideDuringTutorial.Length];
        for (int i = 0; i < hideDuringTutorial.Length; i++) {
            if (hideDuringTutorial[i] == null) continue;
            wasActive[i] = hideDuringTutorial[i].activeSelf;
            hideDuringTutorial[i].SetActive(false);
        }
        return wasActive;
    }

    void RestoreOtherUi(bool[] wasActive) {
        if (wasActive == null || hideDuringTutorial == null) return;

        for (int i = 0; i < hideDuringTutorial.Length && i < wasActive.Length; i++) {
            if (hideDuringTutorial[i] == null) continue;
            hideDuringTutorial[i].SetActive(wasActive[i]);
        }
    }

    #endregion
    #region 에디터 표시

#if UNITY_EDITOR
    void OnDrawGizmos() {
        if (!TryGetComponent<Collider2D>(out var col)) return;

        // 대사(DialogueTriggerZone)의 주황색과 구분되도록 하늘색을 쓴다.
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.3f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.8f);
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
#endif

    #endregion
}
