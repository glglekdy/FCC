using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

// 던전(뒷세계 등) 입구. 상호작용하면 던전을 새로 생성해 플레이어를 들여보내고, 전투방을 모두
// 클리어하면 기억 조각을 지급한다. 던전 안에서의 낙사·사망 처리는 DungeonRespawnController 에 맡긴다.
//
// 들고 나는 순간은 "① 거울이 반응한다 → ② 삼켜진다(암전) → ③ 저쪽에서 눈을 뜬다" 세 박자로 재생한다.
// 생성(Generate)과 순간이동은 반드시 ② 의 암전 뒤에서 처리한다 — 화면이 보이는 채로 방을 통째로
// Instantiate 하면 프레임이 튀는 것까지 그대로 보이고, 무엇보다 순간이동이 연출이 아니라 사고처럼 보인다.
//
// **콜라이더(Is Trigger 체크)를 함께 붙여야 PlayerInteractor 가 탐지합니다.**
public class DungeonGate : MonoBehaviour, IInteractable {
    #region 인스펙터 변수

    [Header("식별")]
    // 클리어 여부를 기록하는 고유 id. **씬 안에서 겹치지 않게 지으세요.** 비우면 오브젝트 이름을 쓴다.
    public string dungeonId;

    [Header("프롬프트")]
    public string label = "들어가기";
    public Vector3 promptOffset = new(0f, 1.6f, 0f);

    [Header("던전")]
    public DungeonGenerator generator;

    [Header("복귀")]
    // 던전에서 걸어 나오거나 죽었을 때 플레이어가 설 위치. **빈 오브젝트를 만들어 연결하세요.** 비우면 이 게이트 위치.
    public Transform outsideReturnPoint;

    [Header("목표")]
    // 비어 있지 않으면 던전을 완전히 클리어한 순간 이 목표를 완료 처리한다(SaveMirror·DialogueTriggerZone 과 같은 방식).
    public string objectiveId;

    [Header("보상 · 사망 처리")]
    public int memoryShardReward = 1;

    [Tooltip("던전 안에서 죽으면 자아 게이지를 최대치의 이 비율로 되돌린다. 1이면 전량 회복.")]
    [Range(0f, 1f)] public float deathHealthRestoreRatio = 1f;

    [Tooltip("낙사할 때마다 깎을 자아 게이지. 0이면 페널티 없음.")]
    public int fallDamage = 10;

    [Tooltip("낙사로 복귀한 직후 무적으로 버틸 시간(초). 복귀 지점 근처의 몬스터에게 연달아 맞는 것을 막는다.")]
    public float fallInvincibleTime = 3f;

    [Header("거울 연출")]
    // 진입할 때 빛나고, 클리어하고 나온 순간 부서지는 전신 거울. 비우면 이 오브젝트와 자식에서 찾는다.
    public DungeonGateMirror mirror;

    [Header("진입 연출")]
    // 던전에 들어선 순간 화면 위에서 내려오는 지역 이름. 비우면 표시하지 않는다.
    public string areaTitle = "뒷세계";
    public string areaSubtitle;

    [Tooltip("두 번째 진입부터는 거울 반응(①)을 건너뛰고 암전만 태운다. 클리어 전에 걸어 나왔다가 다시 들어오는 경우 같은 도입을 매번 보면 지겨워진다.")]
    public bool shortenOnReenter = true;

    public float enterFadeOut = 0.55f; // 화면이 검어지는 시간.
    public float blackHold = 0.35f;    // 검은 채로 머무는 시간. 이 사이에 던전을 만들고 플레이어를 옮긴다.
    public float enterFadeIn = 0.8f;   // 던전 쪽에서 화면이 밝아지는 시간.

    [Header("진입 연출 — 카메라 밀어 넣기")]
    // 거울이 반응하는 동안 살짝 다가가는 카메라. **CoreScene 의 Player_Camera 를 연결하세요.** 비우면 생략한다.
    public CinemachineCamera pushInCamera;
    [Tooltip("시야 크기(Ortho Size)에 곱할 배율. 1보다 작아야 다가가는 것으로 보인다. 절대값이 아니라 배율인 이유는 카메라 영역마다 기본 시야가 달라서다.")]
    [Range(0.5f, 1f)] public float pushInZoom = 0.9f;

    [Header("퇴장 연출")]
    public float exitFadeOut = 0.45f;
    public float exitFadeIn = 0.8f;

    #endregion
    #region 런타임 변수

    GameObject cachedPlayer;
    Player_move cachedPlayerMove;
    int combatRoomsRemaining; // 던전 한 판에 전투방이 여러 개 나올 수 있어 전부 클리어해야 보상을 준다.
    bool inDungeon;
    bool isTransitioning;     // 들고 나는 연출이 도는 중. 이 사이의 상호작용·재진입을 전부 막는다.
    bool hasEntered;          // 한 번이라도 들어간 적이 있는지. 재진입 때 도입을 줄이는 데 쓴다.

    Coroutine pushRoutine;
    float pushBaseSize;       // 밀어 넣기 전의 시야 크기. 되돌릴 때 쓴다.
    bool pushedIn;

    #endregion
    #region IInteractable

    public string InteractLabel => label;

    public bool CanInteract {
        get {
            if (inDungeon || isTransitioning) return false;
            return !(DungeonManager.Instance != null && DungeonManager.Instance.IsCleared(dungeonId));
        }
    }

    public Vector3 PromptAnchor => transform.position + promptOffset;

    public void Interact(GameObject interactor) {
        if (inDungeon || isTransitioning) return;
        if (generator == null) {
            Debug.LogError($"[DungeonGate] '{name}' — generator 가 연결되지 않아 던전을 생성할 수 없습니다.", this);
            return;
        }

        // PlayerInteractor 가 플레이어 루트가 아닌 자식에 붙어 있어도 동작하도록 루트를 찾아 쓴다 (SaveMirror 와 같은 이유).
        Health health = interactor.GetComponentInParent<Health>();
        cachedPlayer = health != null ? health.gameObject : interactor;
        cachedPlayerMove = cachedPlayer != null ? cachedPlayer.GetComponentInChildren<Player_move>() : null;

        StartCoroutine(EnterRoutine());
    }

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        if (string.IsNullOrEmpty(dungeonId)) dungeonId = name;
        if (mirror == null) mirror = GetComponentInChildren<DungeonGateMirror>(true);
    }

    // 이미 클리어한 던전이면 거울이 처음부터 부서진 상태여야 한다. Awake 가 아니라 Start 인 이유는
    // DungeonManager 가 세이브에서 클리어 목록을 되돌리는 시점보다 뒤여야 하기 때문이다.
    void Start() {
        if (mirror == null) return;
        if (DungeonManager.Instance != null && DungeonManager.Instance.IsCleared(dungeonId)) mirror.SetBrokenImmediate();
    }

    void OnDestroy() {
        if (generator != null) generator.OnDungeonExited -= HandleWalkOut;
    }

    #endregion
    #region 진입

    IEnumerator EnterRoutine() {
        isTransitioning = true;

        // 연출이 도는 동안 조작을 뺏으므로, 그 사이에 오버월드 몬스터에게 무방비로 맞지 않도록 무적을 걸어 둔다.
        // 던전 쪽으로 남는 시간은 그대로 진입 직후의 유예가 되므로 따로 줄이지 않는다.
        float leadIn = ShouldPlayLeadIn() && mirror != null ? mirror.enterDuration : 0f;
        GrantInvincibility(leadIn + enterFadeOut + blackHold + enterFadeIn + 0.5f);
        LockPlayer(true);

        // ① 거울이 반응한다. 카메라는 같은 시간 동안 함께 밀려 들어간다.
        if (ShouldPlayLeadIn()) {
            StartPushIn();
            yield return mirror.PlayEnter();
        }

        // ② 삼킨다.
        yield return Cover(1f, enterFadeOut);

        // 카메라는 화면이 검은 동안 되돌린다. 도착한 방의 카메라 영역이 값을 정한 뒤에 되돌리면 그 값을 덮어쓴다.
        StopPushIn();

        List<DungeonRoom> rooms = generator.Generate(); // 매번 새 레이아웃. 무거우므로 반드시 암전 뒤에서 돈다.
        if (rooms.Count == 0) {
            Debug.LogError($"[DungeonGate] '{name}' — 생성된 방이 없습니다. 방 프리팹 풀을 확인하세요.", this);

            // 검은 화면에 갇히지 않도록 막을 걷고 끝낸다 (ScreenFader 가 씬 로드 실패를 처리하는 방식과 같다).
            yield return Cover(0f, enterFadeIn);
            LockPlayer(false);
            isTransitioning = false;
            yield break;
        }

        inDungeon = true;
        hasEntered = true;

        // 전투방 클리어 카운트.
        combatRoomsRemaining = 0;
        foreach (DungeonRoom room in rooms) {
            if (room.role != DungeonRoom.RoomRole.CombatArena || room.spawner == null) continue;
            combatRoomsRemaining++;
            room.spawner.OnAllMonstersDefeated += HandleCombatRoomCleared;
        }

        // 걸어 나가기 처리. 중복 구독 방지.
        generator.OnDungeonExited -= HandleWalkOut;
        generator.OnDungeonExited += HandleWalkOut;

        // 던전 안 낙사·사망 처리 시작.
        DungeonRespawnController.Begin(cachedPlayer, generator.fallYThreshold, fallDamage, fallInvincibleTime, HandlePlayerDeath);

        // 입구방으로 이동 + 첫 방 리스폰.
        DungeonRoom entry = rooms[0];
        WarpTo(cachedPlayer, entry.entryAnchor != null ? entry.entryAnchor.position : entry.transform.position);

        if (DungeonRespawnController.Instance != null) DungeonRespawnController.Instance.SetCurrentRoom(entry);

        if (blackHold > 0f) yield return new WaitForSecondsRealtime(blackHold);

        // ③ 눈을 뜬다. 지역 이름을 막이 걷히기 직전에 띄워 두면 화면이 밝아지면서 글자가 함께 떠오른다.
        if (!string.IsNullOrEmpty(areaTitle)) AreaTitleView.Announce(areaTitle, areaSubtitle);
        yield return Cover(0f, enterFadeIn);

        LockPlayer(false);
        isTransitioning = false;
    }

    bool ShouldPlayLeadIn() {
        return mirror != null && !(shortenOnReenter && hasEntered);
    }

    #endregion
    #region 클리어 판정 · 보상

    void HandleCombatRoomCleared() {
        combatRoomsRemaining = Mathf.Max(0, combatRoomsRemaining - 1);
        if (combatRoomsRemaining > 0) return; // 아직 안 끝난 전투방이 남아 있으면 보류.

        if (DungeonManager.Instance != null) DungeonManager.Instance.MarkCleared(dungeonId);

        if (cachedPlayer != null && cachedPlayer.TryGetComponent(out Player_MemoryShardInventory shards)) {
            shards.Add(memoryShardReward);
        }

        if (!string.IsNullOrEmpty(objectiveId) && ObjectiveManager.Instance != null) {
            ObjectiveManager.Instance.CompleteObjective(objectiveId);
        }
    }

    #endregion
    #region 이탈 처리

    // 자아 게이지 0 → 던전 밖으로 되돌리고 체력을 회복시킨다.
    void HandlePlayerDeath() {
        ExitDungeon(restoreHealth: true);
    }

    // 클리어 전에 입구/출구로 걸어 나감 → 그대로 밖으로. 체력은 건드리지 않는다.
    void HandleWalkOut() {
        ExitDungeon(restoreHealth: false);
    }

    void ExitDungeon(bool restoreHealth) {
        if (!inDungeon) return;
        inDungeon = false; // 나가는 동안 출구 트리거가 다시 밟혀도 두 번 돌지 않도록 먼저 내린다.

        StartCoroutine(ExitRoutine(restoreHealth));
    }

    IEnumerator ExitRoutine(bool restoreHealth) {
        isTransitioning = true;

        // 사망으로 나갈 때 플레이어는 체력 1로 되살아난 상태다(DungeonRespawnController.HandleLethal).
        // 막이 덮이는 동안 옆에 붙은 몬스터에게 한 대 더 맞으면, 이번엔 가로채는 쪽이 없어 진짜로 파괴된다.
        GrantInvincibility(exitFadeOut + blackHold + exitFadeIn + 0.5f);
        LockPlayer(true);

        yield return Cover(1f, exitFadeOut);

        // 해체도 암전 뒤에서 한다. 눈앞에서 방이 사라지면 던전이 무너진 것처럼 보인다.
        if (generator != null) generator.Teardown();
        if (DungeonRespawnController.Instance != null) DungeonRespawnController.Instance.Dispose();

        Vector3 pos = outsideReturnPoint != null ? outsideReturnPoint.position : transform.position;
        WarpTo(cachedPlayer, pos);

        if (restoreHealth && cachedPlayer != null && cachedPlayer.TryGetComponent(out Health h)) {
            int target = Mathf.Max(1, Mathf.RoundToInt(h.MaxHealth * deathHealthRestoreRatio));
            h.SetHealth(target);
        }

        if (blackHold > 0f) yield return new WaitForSecondsRealtime(blackHold);
        yield return Cover(0f, exitFadeIn);

        LockPlayer(false);
        isTransitioning = false;

        // 클리어하고 나왔을 때만 거울을 부순다. 클리어 전에 걸어 나온 것은 다시 들어갈 수 있어야 하므로
        // 입구가 그대로 남아 있어야 한다(CanInteract 도 같은 기준으로 열고 닫힌다).
        // 막이 완전히 걷힌 뒤에 부르는 이유는, 암전 중에 터지면 플레이어가 그 장면을 통째로 놓치기 때문이다.
        if (mirror != null && DungeonManager.Instance != null && DungeonManager.Instance.IsCleared(dungeonId)) {
            mirror.PlayBreak();
        }
    }

    #endregion
    #region 연출 도구

    // 페이더가 씬에 없으면 연출 없이 지나간다 — 호출부마다 분기하지 않도록 여기로 모았다
    // (ScreenFader.LoadScene 과 같은 방침).
    IEnumerator Cover(float target, float duration) {
        if (ScreenFader.Instance == null) yield break;
        yield return ScreenFader.Instance.FadeCover(target, duration);
    }

    void LockPlayer(bool locked) {
        if (cachedPlayerMove != null) cachedPlayerMove.isMovementLocked = locked;
    }

    void GrantInvincibility(float duration) {
        if (cachedPlayer == null) return;
        if (cachedPlayer.TryGetComponent(out Health health)) health.SetInvincible(duration);
    }

    void StartPushIn() {
        if (pushInCamera == null || pushInZoom >= 1f) return;

        if (pushRoutine != null) StopCoroutine(pushRoutine);
        pushRoutine = StartCoroutine(PushInRoutine());
    }

    // 거울이 반응하는 동안 시야를 좁혀 거울 쪽으로 다가간 것처럼 보이게 한다.
    // 되돌릴 크기는 Awake 가 아니라 밀기 시작하는 순간에 기억한다 — 카메라 영역(Player_AreaCameraController)이
    // 구역마다 시야 크기를 바꾸므로, 미리 기억해 두면 엉뚱한 크기로 되돌린다.
    IEnumerator PushInRoutine() {
        pushBaseSize = pushInCamera.Lens.OrthographicSize;
        pushedIn = true;

        float target = pushBaseSize * pushInZoom;
        float duration = mirror != null ? Mathf.Max(0.01f, mirror.enterDuration) : 0.6f;
        float elapsed = 0f;

        while (elapsed < duration) {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            SetCameraSize(Mathf.Lerp(pushBaseSize, target, t));
            yield return null;
        }

        pushRoutine = null;
    }

    void StopPushIn() {
        if (pushRoutine != null) {
            StopCoroutine(pushRoutine);
            pushRoutine = null;
        }

        if (!pushedIn) return;

        pushedIn = false;
        SetCameraSize(pushBaseSize); // 암전 중이라 튀는 것이 보이지 않으므로 서서히가 아니라 한 번에 되돌린다.
    }

    void SetCameraSize(float size) {
        if (pushInCamera == null) return;

        LensSettings lens = pushInCamera.Lens;
        lens.OrthographicSize = size;
        pushInCamera.Lens = lens;
    }

    void WarpTo(GameObject go, Vector3 pos) {
        if (go == null) return;

        go.transform.position = pos;
        if (go.TryGetComponent(out Rigidbody2D rb)) rb.linearVelocity = Vector2.zero;
    }

    #endregion
    #region 에디터 표시

#if UNITY_EDITOR
    void OnDrawGizmos() {
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.8f);
        Gizmos.DrawWireSphere(PromptAnchor, 0.2f);

        Vector3 ret = outsideReturnPoint != null ? outsideReturnPoint.position : transform.position;
        Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireSphere(ret, 0.3f);
        Gizmos.DrawLine(transform.position, ret);
    }
#endif

    #endregion
}
