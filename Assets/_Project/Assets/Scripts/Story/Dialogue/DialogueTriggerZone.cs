using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 대사 한 묶음을 재생하는 컴포넌트. 시작 방식은 두 가지다.
//   영역 진입형 — 플레이어가 이 오브젝트의 Collider2D(Is Trigger) 안에 들어오면 재생된다.
//   강제 시작형 — autoStart를 켜면 씬이 시작될 때 조작과 무관하게 알아서 재생된다 (오프닝 등).
//
// 재생 루프 자체는 DialoguePlayer를 쓴다 — 컷씬형(DialogueStep)과 진행 규칙(타자기 스킵 / AUTO 대기)이
// 어긋나지 않도록 하기 위함이다.
//
// 대사는 이 컴포넌트의 인스펙터에서 직접 편집한다 — 칸마다 화자 / 초상화 / 좌·우 / 본문 / 효과음.
// 화자·본문은 String Table 'Dialogue' 의 키를 가리키는 LocalizedString이다.
public class DialogueTriggerZone : MonoBehaviour {
    #region 인스펙터 변수

    [Header("연결")]
    public DialogueView view; // 대사를 띄울 대화창. **InGameUi 프리팹의 DialogueUI 를 연결하세요.**
    public InputActionReference advanceAction; // 다음/스킵 입력 (Client ▸ Ui ▸ NextDialogue).

    [Header("대사")]
    public List<DialogueEntry> entries = new();
    public bool hideOnFinish = true; // 끝나면 대화창을 끈다.

    [Header("시작 방식")]
    // 켜면 영역 진입을 기다리지 않고 씬이 시작될 때 강제로 재생한다. **켜면 Collider 트리거는 무시됩니다.**
    public bool autoStart;
    // 깨어나기 연출(ScreenWakeUp)이 끝난 뒤에 시작한다. 끄면 씬이 시작되자마자 바로 재생한다.
    // 같은 씬에 ScreenWakeUp이 없으면 기다리지 않는다. autoStart가 꺼져 있으면 의미가 없다.
    public bool waitForWakeUp = true;
    public float autoStartDelay = 0.4f; // 시작 조건이 갖춰진 뒤 한 박자 쉬는 시간(초). 곧바로 말이 튀어나오지 않게 한다.

    [Header("진입 조건")]
    public bool playOnce = true;
    public string playerTag = "Player";

    [Header("진행 중 처리")]
    public bool lockPlayerMovement = true; // 대사 중 이동 잠금 (Player_move.isMovementLocked 재사용).
    public string objectiveId; // 비어있지 않으면 대사가 끝날 때 해당 목표를 완료 처리.
    public SkillBase unlockSkill; // 비어있지 않으면 대사가 끝날 때 이 스킬을 되찾는다(해금 알림 · 빈 슬롯 자동 장착).
    public Player_Ability unlockAbility; // None 이 아니면 대사가 끝날 때 이 이동 패시브를 배운다(곡예사의 가르침).

    [Header("끝난 뒤")]
    // 대사가 전부 끝나면 검은 막으로 화면을 덮는다. ScreenFader 와 같은 함정이라
    // **막을 걷는 쪽(다음 컷씬 · 씬 전환 · DialogueView.ClearBackground)이 반드시 있어야 합니다.**
    // 없으면 검은 화면에서 그대로 멈춥니다.
    public bool blackoutOnFinish;
    public float blackoutDuration = 1f; // 막이 내려오는 데 걸리는 시간(초).

    #endregion
    #region 이벤트

    public static event Action OnDialogueStart;
    public static event Action OnDialogueEnd;

    #endregion
    #region 런타임 변수

    bool played;
    static bool isPlaying; // 대사 두 묶음이 겹쳐 재생되지 않도록 씬 전체에서 하나만 돌게 한다.

    // 프로젝트가 Enter Play Mode Options에서 Domain Reload를 꺼두고 있어, static 필드가 Play를
    // 멈춰도 초기화되지 않는다. 대사가 재생되던 도중 Play를 멈추면 isPlaying이 true로 박제되어
    // 이후 어떤 트리거도 조용히 무시되므로, Play 진입 시점마다 강제로 초기화한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetStaticStateOnPlay() {
        isPlaying = false;
    }

    #endregion
    #region 유니티 라이프 사이클

    // 강제 시작형일 때만 도는 코루틴. Awake가 아니라 Start인 이유는 같은 씬의 ScreenWakeUp보다
    // 먼저 Awake가 돌 수 있어(실행 순서가 정해져 있지 않음) 연출을 찾지 못할 수 있기 때문이다.
    IEnumerator Start() {
        if (!autoStart) yield break;

        // 첫 칸의 배경을 깨어나기 · 시작 대기보다 먼저 깔아둔다. 재생이 시작될 때 깔면 그 전까지
        // 배경 없는 게임 카메라 화면이 드러나, 눈을 뜨는 순간 엉뚱한 장면이 보인다.
        // 씬 시작 시점엔 걷어낼 앞 배경이 없어 페이드 없이 첫 프레임부터 바로 깔린다.
        if (view != null) DialoguePlayer.PrepareBackground(view, entries);

        if (waitForWakeUp) {
            ScreenWakeUp wakeUp = FindAnyObjectByType<ScreenWakeUp>();

            // 파괴된 뒤에도 C# 참조가 남을 수 있어 ?. 대신 != null 로 Unity의 == 오버로드를 탄다.
            if (wakeUp != null) {
                yield return wakeUp.WaitUntilFinished();
            }
            else {
                Debug.LogWarning($"[DialogueTriggerZone] '{name}' — 같은 씬에 ScreenWakeUp이 없어 " +
                    "깨어나기를 기다리지 않고 바로 시작합니다.", this);
            }
        }

        // 히트스톱이나 일시정지 배속에 끌려가지 않도록 unscaled 기준으로 센다 (ScreenWakeUp과 같은 이유).
        if (autoStartDelay > 0f) yield return new WaitForSecondsRealtime(autoStartDelay);

        yield return RunDialogue(FindAnyObjectByType<Player_move>());
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (autoStart) return; // 강제 시작형은 영역 진입을 무시한다. Start에서 이미 재생을 맡고 있다.
        if (!other.CompareTag(playerTag)) return;
        if (playOnce && played) return;
        if (isPlaying) return;

        StartCoroutine(RunDialogue(other.GetComponent<Player_move>()));
    }

    #endregion
    #region 재생

    // 대사를 처음부터 재생한다. playerMove가 null이면 이동 잠금만 건너뛴다 (플레이어가 없는 연출 전용 씬 등).
    IEnumerator RunDialogue(Player_move playerMove) {
        if (view == null) {
            Debug.LogError($"[DialogueTriggerZone] '{name}' 의 View 칸이 비어 있어 대화를 재생할 수 없습니다.", this);
            yield break;
        }
        if (entries.Count == 0) {
            Debug.LogWarning($"[DialogueTriggerZone] '{name}' 의 Entries 리스트가 비어 있습니다.", this);
            yield break;
        }
        if (isPlaying) yield break; // 강제 시작과 영역 진입이 겹치는 등, 두 번 들어오는 경우를 막는다.

        isPlaying = true;
        played = true;
        OnDialogueStart?.Invoke();

        if (!lockPlayerMovement) playerMove = null;
        if (playerMove != null) playerMove.isMovementLocked = true;

        yield return DialoguePlayer.Play(view, entries, advanceAction, hideOnFinish);

        // 암전을 DialoguePlayer 가 아니라 여기서 부르는 이유: 이것은 "이 대사 묶음"의 연출이지
        // 재생 루프의 규칙이 아니다. 루프에 넣으면 같은 루프를 쓰는 NpcDialogue · DialogueStep 까지
        // 대화가 끝날 때마다 화면이 까매진다.
        if (blackoutOnFinish) view.BlackoutBackground(blackoutDuration);

        if (playerMove != null) playerMove.isMovementLocked = false;

        isPlaying = false;
        OnDialogueEnd?.Invoke();

        // 파괴된 뒤에도 C# 참조가 남을 수 있어 ?. 대신 != null 로 Unity의 == 오버로드를 탄다.
        if (!string.IsNullOrEmpty(objectiveId) && ObjectiveManager.Instance != null) {
            ObjectiveManager.Instance.CompleteObjective(objectiveId);
        }

        if (unlockSkill != null) SkillUnlocker.Unlock(unlockSkill);
        if (unlockAbility != Player_Ability.None) Player_AbilityUnlocker.Unlock(unlockAbility);
    }

    #endregion
    #region 에디터 표시

#if UNITY_EDITOR
    void OnDrawGizmos() {
        // 강제 시작형은 영역이 의미가 없으므로 그리지 않는다. 씬 뷰에서 둘을 눈으로 구분하기 위함.
        if (autoStart) return;
        if (!TryGetComponent<Collider2D>(out var col)) return;

        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.3f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
#endif

    #endregion
}
