using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;

// F키로 반복해서 말을 걸 수 있는 NPC. 대화를 건 횟수(NpcDialogueManager가 세이브까지 기억한다)에 따라
// dialogueSets 중 해당 순번의 대사를 재생하고, 목록보다 많이 말을 걸면 마지막 세트를 반복한다.
// 재생 루프는 DialogueTriggerZone과 같은 DialoguePlayer를 쓴다.
//
// **콜라이더(Is Trigger 체크)를 함께 붙여야 PlayerInteractor가 탐지합니다.**
public class NpcDialogue : MonoBehaviour, IInteractable {
    #region 인스펙터 변수

    [Header("식별")]
    // 세이브 파일에 기록되는 NPC 고유 id. **씬 안에서 겹치지 않게 지으세요.** 비워두면 오브젝트 이름을 대신 쓴다.
    public string npcId;

    [Header("연결")]
    public DialogueView view; // 대사를 띄울 대화창. **InGameUi 프리팹의 DialogueUI 를 연결하세요.**
    public InputActionReference advanceAction; // 다음/스킵 입력 (Client ▸ Ui ▸ NextDialogue).

    [Header("대사")]
    // 대화 횟수(0=1번째, 1=2번째, 2=3번째...)에 따라 재생할 대사 세트.
    // 목록보다 대화를 많이 걸면 마지막 세트를 반복합니다.
    public List<NpcDialogueSet> dialogueSets = new();
    public bool hideOnFinish = true; // 끝나면 대화창을 끈다.

    [Header("프롬프트")]
    public LocalizedString label = new("Ui", "interact.talk"); // 플레이어에게 뜨는 문구.
    public Vector3 promptOffset = new(0f, 1.6f, 0f); // NPC 머리 위쪽에 프롬프트를 띄울 오프셋.

    [Header("진행 중 처리")]
    public bool lockPlayerMovement = true; // 대화 중 이동 잠금 (Player_move.isMovementLocked 재사용).
    public string objectiveId; // 비어있지 않으면 대화가 끝날 때마다 해당 목표를 완료 처리.

    #endregion
    #region 런타임 변수

    bool isTalking; // 대사가 도는 동안 상호작용을 잠그는 가드 (SaveMirror.isSaving과 같은 이유).

    #endregion
    #region IInteractable

    public string InteractLabel => LocalizationText.Resolve(label, "대화하기");
    public bool CanInteract => !isTalking;
    public Vector3 PromptAnchor => transform.position + promptOffset;

    public void Interact(GameObject interactor) {
        if (isTalking) return;
        StartCoroutine(RunDialogue(interactor));
    }

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        // id를 깜빡해도 최소한 오브젝트 이름으로는 구분되게 한다.
        if (string.IsNullOrEmpty(npcId)) npcId = name;
    }

    #endregion
    #region 재생

    IEnumerator RunDialogue(GameObject interactor) {
        if (view == null) {
            Debug.LogError($"[NpcDialogue] '{name}' 의 View 칸이 비어 있어 대화를 재생할 수 없습니다.", this);
            yield break;
        }
        if (dialogueSets.Count == 0) {
            Debug.LogWarning($"[NpcDialogue] '{name}' 의 Dialogue Sets 목록이 비어 있습니다.", this);
            yield break;
        }

        isTalking = true;

        // PlayerInteractor가 플레이어 루트가 아닌 자식에 붙어 있어도 찾아지도록 GetComponentInParent를 쓴다
        // (SaveMirror와 같은 이유).
        Player_move playerMove = lockPlayerMovement && interactor != null
            ? interactor.GetComponentInParent<Player_move>()
            : null;
        if (playerMove != null) playerMove.isMovementLocked = true;

        int talkCount = NpcDialogueManager.Instance != null ? NpcDialogueManager.Instance.GetTalkCount(npcId) : 0;
        int index = Mathf.Min(talkCount, dialogueSets.Count - 1);
        NpcDialogueSet set = dialogueSets[index];

        yield return DialoguePlayer.Play(view, set.entries, advanceAction, hideOnFinish);

        if (NpcDialogueManager.Instance != null) {
            NpcDialogueManager.Instance.IncrementTalkCount(npcId);
        }
        else {
            Debug.LogWarning($"[NpcDialogue] '{name}' — 씬에 NpcDialogueManager가 없어 대화 횟수를 기억하지 못합니다.", this);
        }

        if (playerMove != null) playerMove.isMovementLocked = false;
        isTalking = false;

        // 마지막 세트를 반복해서 들어도 이미 배운 기술이면 UnlockAbility 가 조용히 무시한다.
        if (set.unlockAbility != Player_Ability.None) Player_AbilityUnlocker.Unlock(set.unlockAbility);

        // 파괴된 뒤에도 C# 참조가 남을 수 있어 ?. 대신 != null 로 Unity의 == 오버로드를 탄다.
        if (!string.IsNullOrEmpty(objectiveId) && ObjectiveManager.Instance != null) {
            ObjectiveManager.Instance.CompleteObjective(objectiveId);
        }
    }

    #endregion
    #region 에디터 표시

#if UNITY_EDITOR
    void OnDrawGizmos() {
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + promptOffset, 0.2f);
    }
#endif

    #endregion
}
