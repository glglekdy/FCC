using System.Collections;
using UnityEngine;
using UnityEngine.Localization;

// 던전 출구방의 전신 거울. 상호작용하면 거울이 반응한 뒤 던전을 빠져나간다.
//
// 예전에는 출구방 끝의 트리거(DungeonExitZone)에 닿는 순간 나갔다. 들어올 때는 거울을 눌러 들어오는데
// 나갈 때는 벽에 닿기만 하면 튕겨 나가 입장과 퇴장의 무게가 맞지 않았고, 출구방을 둘러보다 끝에 닿으면
// 의도하지 않아도 나가졌다. 그래서 나가는 것도 플레이어가 고르는 행동으로 바꿨다.
//
// 나간 뒤의 처리(암전 · 해체 · 복귀 · 클리어 시 입구 거울 파괴)는 그대로 DungeonGate 가 맡는다. 여기서는
// DungeonExitZone 과 같은 창구(DungeonGenerator.NotifyExit)로 알리기만 한다 — 퇴장 경로가 둘로 갈라지면
// 한쪽만 고치는 일이 생긴다.
//
// **Is Trigger 콜라이더를 붙이세요.** (PlayerInteractor 가 트리거만 탐지합니다.)
[RequireComponent(typeof(Collider2D))]
public class DungeonExitMirror : MonoBehaviour, IInteractable {
    #region 인스펙터 변수

    [Header("프롬프트")]
    public LocalizedString label = new("Ui", "interact.exit"); // 플레이어에게 뜨는 문구.
    public Vector3 promptOffset = new(0f, 3.8f, 0f); // 거울 꼭대기 위에 프롬프트를 띄울 오프셋. 피벗은 발밑이다.

    [Header("연결")]
    // 빛이 훑고 지나가는 거울 연출. 비우면 자식에서 찾고, 그래도 없으면 연출 없이 바로 나간다.
    public DungeonGateMirror mirror;

    [Header("연출")]
    public float extraInvincibleTime = 0.3f; // 거울이 반응하는 시간에 더해 거는 무적. 게이트의 퇴장 무적이 이어받기 전의 틈을 메운다.

    #endregion
    #region 런타임 변수

    DungeonGenerator generator; // 프리팹은 씬 오브젝트를 참조할 수 없으므로 DungeonGenerator 가 생성 직후 주입한다.
    bool isExiting;             // 연출 도중 한 번 더 눌려 NotifyExit 가 두 번 가지 않도록 막는다.

    #endregion
    #region IInteractable

    public string InteractLabel => LocalizationText.Resolve(label, "나가기");
    public bool CanInteract => generator != null && !isExiting;
    public Vector3 PromptAnchor => transform.position + promptOffset;

    public void Interact(GameObject interactor) {
        if (!CanInteract || interactor == null) return;
        StartCoroutine(ExitRoutine(interactor));
    }

    #endregion
    #region 유니티 라이프 사이클

    void Reset() {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void Awake() {
        if (mirror == null) mirror = GetComponentInChildren<DungeonGateMirror>(true);
    }

    public void Configure(DungeonGenerator owner) {
        generator = owner;
    }

    #endregion
    #region 퇴장

    IEnumerator ExitRoutine(GameObject interactor) {
        isExiting = true;

        // PlayerInteractor 가 플레이어 루트가 아닌 자식에 붙어 있어도 동작하도록 루트를 찾아 쓴다 (DungeonGate 와 같은 이유).
        Health health = interactor.GetComponentInParent<Health>();
        GameObject player = health != null ? health.gameObject : interactor;
        Player_move move = player.GetComponentInChildren<Player_move>();

        // 거울이 반응하는 동안 조작을 뺏으므로 그 사이에 맞지 않도록 무적을 건다.
        float leadIn = mirror != null ? mirror.enterDuration : 0f;
        if (health != null) health.SetInvincible(leadIn + extraInvincibleTime);
        if (move != null) move.isMovementLocked = true;

        // 들어올 때와 같은 반응을 보여 준다. 같은 거울로 드나든다는 느낌을 주려는 것이다.
        if (mirror != null) yield return mirror.PlayEnter();

        // 잠금을 먼저 풀고 알린다. 게이트의 퇴장 연출이 같은 호출 안에서 곧바로 다시 잠그므로 틈은 생기지 않고,
        // 구독한 게이트가 없어 생성기가 그냥 해체하는 경우에도 플레이어가 잠긴 채 남지 않는다.
        if (move != null) move.isMovementLocked = false;
        generator.NotifyExit();
    }

    #endregion
    #region 에디터 표시

#if UNITY_EDITOR
    void OnDrawGizmos() {
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.8f);
        Gizmos.DrawWireSphere(PromptAnchor, 0.2f);
    }
#endif

    #endregion
}
