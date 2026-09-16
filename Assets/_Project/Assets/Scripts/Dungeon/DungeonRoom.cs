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
        [InspectorName("출구")] Exit
    }

    #region 인스펙터 변수

    [Header("역할")]
    public RoomRole role;

    [Header("소켓")]
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
    public Collider2D lockBarrier; // 입장 시 켜져(통행 차단) 전멸하면 꺼진다.

    [Header("곁가지 문 (역할이 곁가지 보너스일 때만)")]
    // **곁가지 방 안에 둘 다 넣어 두세요.** 입구 문은 생성기가 부모 방의 branchAnchor 로 옮긴다.
    public DungeonBranchDoor entranceDoor; // 부모 방에 놓여 이 방으로 들어오는 문.
    public DungeonBranchDoor returnDoor;   // 이 방 안에서 부모 방으로 돌아가는 문.

    #endregion
    #region 조회

    public Transform RespawnAnchor =>
        respawnPoint != null ? respawnPoint : (entryAnchor != null ? entryAnchor : transform);

    #endregion
    #region 런타임 변수

    bool combatStarted; // 같은 방에 다시 들어와도 몬스터가 중복 스폰되지 않도록 막는 가드.

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

        // 2. 전투방이면 락인.
        if (role == RoomRole.CombatArena) StartCombatLockIn();
    }

    #endregion
    #region 전투방 락인

    // 플레이어가 실제로 이 방에 걸어 들어온 순간 몬스터를 스폰하고 진행 방향 바리어를 잠근다.
    void StartCombatLockIn() {
        if (combatStarted || spawner == null) return;
        combatStarted = true;

        if (lockBarrier != null) lockBarrier.enabled = true;
        spawner.OnAllMonstersDefeated += HandleCombatCleared;
        spawner.SpawnAll();
    }

    void HandleCombatCleared() {
        if (lockBarrier != null) lockBarrier.enabled = false;
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
