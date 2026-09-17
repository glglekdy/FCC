using System;
using UnityEngine;

// 던전을 이루는 방 프리팹 하나의 허브. entryAnchor / exitAnchor 로 소켓을 규격화해 DungeonGenerator 가
// 방을 순서대로 이어 붙일 수 있게 한다. 정렬은 두 앵커의 월드 좌표를 맞추는 것뿐이라 좌우뿐 아니라
// 상하로도 그대로 동작한다(VerticalClimb 는 바닥→천장으로 앵커를 둔다).
//
// 플레이어가 이 방 트리거에 들어온 순간 리스폰 지점 갱신·전투 락인을 한 번에 처리한다.
// **방 전체를 덮는 Is Trigger 콜라이더를 이 오브젝트(루트)에 붙이세요.**
public class DungeonRoom : MonoBehaviour {
    public enum RoomRole {
        [InspectorName("입구")] Entry,
        [InspectorName("플랫포밍 기믹")] PlatformingHazard,
        [InspectorName("전투방")] CombatArena,
        [InspectorName("수직 갱도")] VerticalClimb,
        [InspectorName("곁가지 보너스")] SecretBranch,
        [InspectorName("출구")] Exit,
        // **맨 뒤에만 추가하세요.** 역할은 프리팹에 정수로 저장되어, 중간에 끼우면 기존 방의 역할이 한 칸씩 밀린다.
        [InspectorName("상점")] Shop
    }

    #region 인스펙터 변수

    [Header("역할")]
    public RoomRole role;

    [Header("소켓")]
    // **두 소켓은 방 트리거의 좌우 끝(타일 끝)에 두세요.** 안쪽에 두면 앞뒤 방이 그만큼 겹쳐 이음매 타일이 이중으로 그려진다.
    // 입구방의 entryAnchor 만은 이어 붙이는 데 쓰이지 않고 입장 지점(DungeonGate 가 플레이어를 옮기는 곳)이라 안쪽에 둔다.
    public Transform entryAnchor; // 이전 방의 exitAnchor 에 맞춰 이 방이 배치되는 기준점.
    public Transform exitAnchor;  // 다음 방이 이 지점에 맞춰 배치된다.
    public Transform branchAnchor; // 곁가지 방으로 들어가는 문이 서는 지점(문은 이 아래 바닥에 세워진다). 비우면 이 방은 분기를 지원하지 않는다.

    [Header("진입 감지")]
    public Collider2D roomTrigger; // 비우면 이 오브젝트의 Collider2D 를 쓴다.
    public string playerTag = "Player";

    [Header("리스폰")]
    // **낙사 시 돌아올 빈 오브젝트를 만들어 연결하세요.** 비우면 entryAnchor, 그것도 없으면 방 원점을 쓴다.
    public Transform respawnPoint;

    [Header("전투 (역할이 전투방일 때만)")]
    public DungeonRoomSpawner spawner;
    public Collider2D lockBarrier;  // 출구 쪽 벽. 전투가 시작되면 켜져(통행 차단) 전멸하면 꺼진다.
    public Collider2D entryBarrier; // 입구 쪽 벽. 플레이어가 lockInDepth 만큼 들어와야 켜지고, 전멸하면 꺼진다.

    // 입구 소켓에서 출구 쪽으로 이만큼(유닛) 들어와야 몬스터가 깨어나고 뒤가 잠긴다.
    // 문턱에서 바로 잠그면 벽이 플레이어 몸과 겹쳐 켜지거나, 발만 걸친 채 갇힌 것처럼 느껴진다.
    // **입구 소켓 ~ 입구 쪽 벽 거리 + 벽 두께 절반 + 플레이어 몸 절반보다 커야 합니다.** 그보다 작으면 벽이 플레이어를 밀어낸다.
    // 소켓은 방 끝, 벽은 그보다 1.5 안쪽이라 벽에서 3 떨어진 4.5 를 기본으로 둔다.
    public float lockInDepth = 4.5f;

    [Header("곁가지 문 (역할이 곁가지 보너스일 때만)")]
    // **곁가지 방 안에 둘 다 넣어 두세요.** 입구 문은 생성기가 부모 방의 branchAnchor 로 옮긴다.
    public DungeonBranchDoor entranceDoor; // 부모 방에 놓여 이 방으로 들어오는 문.
    public DungeonBranchDoor returnDoor;   // 이 방 안에서 부모 방으로 돌아가는 문.

    #endregion
    #region 이벤트

    // 플레이어가 이 방 트리거에 들어온 순간. 다시 들어와도 매번 발행하므로 "처음"인지는 구독자가 가른다.
    // DungeonGate 가 방을 지나왔는지 판정해 뒷세계 코인을 지급하는 데 쓴다.
    public event Action<DungeonRoom> OnPlayerEntered;

    #endregion
    #region 조회

    public Transform RespawnAnchor =>
        respawnPoint != null ? respawnPoint : (entryAnchor != null ? entryAnchor : transform);

    #endregion
    #region 런타임 변수

    bool combatPrepared; // 앞 방에 다시 들어와도 몬스터가 중복 스폰되지 않도록 막는 가드.
    bool lockedIn;       // 플레이어가 잠금선을 넘어 입구 쪽 벽까지 잠겼는지.
    Transform trackedPlayer; // 이 전투방 트리거에 들어온 플레이어. 잠금선을 넘었는지 매 프레임 본다.

    #endregion
    #region 던전 주입 값

    // 본 동선에서 바로 다음 방. 프리팹은 옆 방을 알 수 없으므로 DungeonGenerator 가 방을 다 이은 뒤 채워 넣는다.
    // 곁가지 방과 출구는 비어 있다.
    [NonSerialized] public DungeonRoom nextRoom;

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        if (roomTrigger == null) roomTrigger = GetComponent<Collider2D>();
        if (spawner == null) spawner = GetComponent<DungeonRoomSpawner>();
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (!other.CompareTag(playerTag)) return;

        // 1. 리스폰: 현재 방을 이 방으로.
        if (DungeonRespawnController.Instance != null) DungeonRespawnController.Instance.SetCurrentRoom(this);

        // 2. 다음 방이 전투방이면 몬스터를 미리 세워 둔다. 전투방이 연달아 나오면 싸우는 동안 다음 방 몬스터도 보인다.
        if (nextRoom != null && nextRoom.role == RoomRole.CombatArena) nextRoom.PrepareCombat();

        // 3. 전투방이면 잠금선을 넘는지 지켜본다. 트리거에 닿은 순간이 아니라 일정 깊이까지 들어와야 싸움이 시작된다.
        //    방은 끝끼리 맞닿아 있어서, 트리거에 닿은 순간은 몸 절반이 아직 앞 방 끝자락에 걸쳐 있는 셈이기도 하다.
        if (role == RoomRole.CombatArena) {
            PrepareCombat(); // 앞 방을 거치지 않고 들어온 경우에도 몬스터는 있어야 한다. 이미 세워 뒀으면 아무것도 하지 않는다.
            trackedPlayer = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;
        }

        OnPlayerEntered?.Invoke(this);
    }

    void Update() {
        if (trackedPlayer == null || lockedIn) return;

        // 잠그기 전에 이미 끝난 방(셀 몬스터가 없는 방 · 앞 방에서 다 잡은 방)은 가둘 이유가 없다.
        if (spawner == null || spawner.IsCleared) {
            trackedPlayer = null;
            return;
        }

        if (IsPastLockLine(trackedPlayer.position)) LockIn();
    }

    #endregion
    #region 전투방 락인

    // 바로 앞 방에 들어선 순간 부른다. 어떤 몬스터가 기다리는지 미리 보이도록 세워만 두고, 싸움은 이 방에 들어와야 시작한다.
    // 들어가기 전에 무엇과 싸울지 보고 준비할 수 있게 하려는 것이다.
    public void PrepareCombat() {
        if (combatPrepared || spawner == null) return;
        combatPrepared = true;

        spawner.OnAllMonstersDefeated += HandleCombatCleared;
        spawner.OnWoken += HandleCombatStarted;
        spawner.SpawnDormant();
    }

    // 플레이어가 잠금선을 넘은 순간 몬스터를 깨우고 입구 쪽 벽을 잠근다. 출구 쪽 벽은 깨어날 때(HandleCombatStarted) 잠긴다.
    void LockIn() {
        lockedIn = true;
        trackedPlayer = null;

        spawner.WakeAll();
        if (entryBarrier != null && !spawner.IsCleared) entryBarrier.enabled = true;
    }

    // 출구 쪽 벽은 들어온 쪽이 아니라 깨어난 쪽에서 잠근다. 앞 방에서 대기 중인 몬스터를 먼저 때려 깨워도 같은 전투이기 때문이다.
    // 입구 쪽 벽은 여기서 잠그지 않는다 — 앞 방에서 깨운 경우 플레이어가 아직 바깥에 있어, 잠그면 방 밖에 갇힌다.
    void HandleCombatStarted() {
        // 깨어나기 전에 이미 끝난 방(셀 몬스터가 없는 방)은 잠그지 않는다. 잠그면 풀어 줄 전멸 이벤트가 다시 오지 않는다.
        if (lockBarrier != null && !spawner.IsCleared) lockBarrier.enabled = true;
    }

    void HandleCombatCleared() {
        if (lockBarrier != null) lockBarrier.enabled = false;
        if (entryBarrier != null) entryBarrier.enabled = false;
    }

    // 입구 소켓 → 출구 소켓 방향으로 잰 깊이가 lockInDepth 를 넘었는지. 방 트리거 밖이면 넘지 않은 것으로 본다 —
    // 곁가지 방은 본 동선 위쪽 줄에 따로 놓여 가로 좌표만 보면 이 방 안쪽과 겹칠 수 있다.
    bool IsPastLockLine(Vector2 position) {
        if (roomTrigger != null && !roomTrigger.OverlapPoint(position)) return false;

        // 소켓이 비어 있으면 깊이를 잴 수 없다. 방에 들어온 것만으로 잠가 예전 동작으로 되돌린다.
        if (entryAnchor == null || exitAnchor == null) return true;

        Vector2 entry = entryAnchor.position;
        Vector2 forward = ((Vector2)exitAnchor.position - entry).normalized;
        return Vector2.Dot(position - entry, forward) >= lockInDepth;
    }

    #endregion
    #region 에디터 표시

#if UNITY_EDITOR
    void OnDrawGizmos() {
        DrawAnchor(entryAnchor, new Color(0.4f, 1f, 0.6f, 0.9f));
        DrawAnchor(exitAnchor, new Color(1f, 0.85f, 0.3f, 0.9f));
        DrawAnchor(branchAnchor, new Color(1f, 0.55f, 0.2f, 0.9f));

        // 리스폰 지점은 SaveMirror 와 같은 창백한 하늘색으로 통일.
        Transform r = RespawnAnchor;
        if (r != null) {
            Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(r.position, 0.3f);
            Gizmos.DrawLine(transform.position, r.position);
        }

        DrawLockLine();
    }

    // 전투방 잠금선. 이 선을 넘으면 몬스터가 깨어나고 입구 쪽 벽이 잠긴다. 선 길이는 방 트리거 높이에 맞춘다.
    void DrawLockLine() {
        if (role != RoomRole.CombatArena || entryAnchor == null || exitAnchor == null) return;

        Vector2 entry = entryAnchor.position;
        Vector2 forward = ((Vector2)exitAnchor.position - entry).normalized;
        Vector2 across = new(-forward.y, forward.x);
        Collider2D area = roomTrigger != null ? roomTrigger : GetComponent<Collider2D>();
        float halfLength = area != null ? Mathf.Max(area.bounds.extents.x, area.bounds.extents.y) : 4f;

        Vector2 point = entry + forward * lockInDepth;
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
        Gizmos.DrawLine(point - across * halfLength, point + across * halfLength);
    }

    void DrawAnchor(Transform t, Color color) {
        if (t == null) return;
        Gizmos.color = color;
        Gizmos.DrawWireSphere(t.position, 0.3f);
        Gizmos.DrawLine(transform.position, t.position);
    }
#endif

    #endregion
}
