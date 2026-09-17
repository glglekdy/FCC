using System;
using System.Collections.Generic;
using UnityEngine;

// 던전 몬스터 풀의 한 항목. 방 스폰 엔트리에 몬스터를 비워두면 여기서 가중치로 뽑는다.
[Serializable]
public class DungeonMonsterOption {
    public GameObject prefab;
    [Min(0f)] public float weight = 1f;
}

// 레이아웃 한 구간(segment). "이 역할의 방을 minRooms~maxRooms 개 이어 붙이고, 각 방마다
// secretBranchChance 확률로 곁가지를 단다." 구간을 인스펙터에서 여러 개 쌓아 던전의 리듬을 짠다.
// 비워 두면 DefaultSequence() 가 대신 쓰인다(입구·기믹·전투를 번갈아 끼운 기본 리듬).
[Serializable]
public class DungeonSegment {
    public string label; // 인스펙터에서 구간을 알아보기 위한 메모. 로직에는 쓰이지 않는다.
    public DungeonRoom.RoomRole category = DungeonRoom.RoomRole.PlatformingHazard;
    [Min(1)] public int minRooms = 1;
    [Min(1)] public int maxRooms = 1;
    [Range(0f, 1f)] public float secretBranchChance;
}

// 역할별 방을 소켓 정렬로 이어 붙이는 던전 생성기.
// 항상 [입구] 로 시작해 [출구] 로 끝나며, 그 사이는 sequence(비면 DefaultSequence)에 적힌 구간을
// 순서대로 펼친다. 순서를 데이터로 빼 두는 이유는, 2D 플랫포머 특성상 어떤 조합이라도 지형이 항상
// 이어지도록 보장하면서(순수 알고리즘 타일 생성과 달리 클리어 불가능한 배치가 나올 수 없다)
// 챕터마다 다른 리듬(기믹을 더 길게 / 전투를 더 촘촘히)을 코드 수정 없이 줄 수 있기 때문이다.
//
// **상점 방은 한 판에 반드시 하나 나온다.** sequence 에 상점 구간을 직접 적으면 그 자리를 따르고,
// 적지 않았으면 가운데 방 목록의 한가운데에 끼워 넣는다. 구간마다 방 개수가 무작위라 "몇 번째 구간 뒤"로
// 정해 두면 판마다 앞쪽에 쏠리거나 출구 코앞에 붙기 때문에, 방 목록을 다 짠 뒤 개수로 자리를 잡는다.
//
// 방 프리팹은 "몬스터가 놓일 수 있는 자리 / 지형 / 리스폰" 을 정의하고, 이 생성기는 "그중 얼마를 실제로
// 쓸지(몬스터 배율)" 와 "던전 밖 경계 · 낙사 높이" 같은 던전 단위 값을 정한다.
public class DungeonGenerator : MonoBehaviour {
    #region 인스펙터 변수 — 방 프리팹 풀

    [Header("방 프리팹 풀 (카테고리별 2종 이상 등록 권장)")]
    public List<GameObject> entryRoomPrefabs = new();
    public List<GameObject> hazardRoomPrefabs = new();
    public List<GameObject> verticalRoomPrefabs = new();
    public List<GameObject> combatRoomPrefabs = new();
    public List<GameObject> secretRoomPrefabs = new();
    public List<GameObject> exitRoomPrefabs = new();
    public List<GameObject> shopRoomPrefabs = new(); // **비워 두면 상점이 빠진 채 생성되고 에러가 뜹니다.**

    #endregion
    #region 인스펙터 변수 — 레이아웃 구간

    [Header("레이아웃 구간 (비우면 기본 리듬 사용)")]
    [Tooltip("입구·출구를 뺀 가운데 구간만 적는다. 첫 구간이 입구가, 마지막 구간이 출구가 아니면 자동으로 앞뒤에 붙인다.")]
    public List<DungeonSegment> sequence = new();

    #endregion
    #region 인스펙터 변수 — 몬스터 양 (던전 단위)

    [Header("몬스터 양")]
    [Tooltip("방에 적힌 스폰 지점 개수에 곱한다. 1이면 방 그대로, 0.5면 절반, 2면 두 배. 같은 방을 챕터마다 다른 밀도로 재사용할 수 있다.")]
    [Range(0f, 3f)] public float monsterCountMultiplier = 1f;

    [Tooltip("배율을 올려도 한 방에 이 수를 넘겨 스폰하지 않는다.")]
    public int maxMonstersPerRoom = 12;

    [Tooltip("방 스폰 엔트리에 몬스터를 비워두면 여기서 가중치로 뽑는다.")]
    public List<DungeonMonsterOption> dungeonMonsterPool = new();

    #endregion
    #region 인스펙터 변수 — 위치 · 경계 · 낙사

    [Header("생성 위치 · 경계")]
    // **오버월드와 겹치지 않는 좌표에 빈 오브젝트를 만들어 연결하세요.**
    public Transform dungeonOrigin;

    // 던전을 나갈 때 카메라를 되돌릴 오버월드 경계. **CoreScene 의 오버월드 Confiner 콜라이더를 연결하세요.**
    public Collider2D outsideBounds;

    [Header("곁가지 배치")]
    [Tooltip("곁가지 방을 던전 맨 위보다 이만큼 더 위에 따로 만든다. 본 동선의 방과 겹치지 않게 띄우는 간격이다.")]
    public float branchRowGap = 12f;

    [Tooltip("곁가지 방끼리 가로로 띄우는 간격.")]
    public float branchSpacing = 4f;

    [Tooltip("입구 문을 branchAnchor 아래 바닥에 세울 때 찾는 지형 레이어. 비우면 'ground' 레이어를 쓴다.")]
    public LayerMask branchDoorGroundMask;

    [Header("낙사")]
    [Tooltip("플레이어가 이 월드 Y 아래로 떨어지면 현재 방 리스폰 지점으로 되돌린다. 방마다 바닥 높이가 달라도 하나로 처리하려고 던전 단위로 둔다.")]
    public float fallYThreshold = -50f;

    #endregion
    #region 이벤트

    // 플레이어가 입구/출구 이탈 트리거를 밟았을 때. DungeonGate 가 구독해 밖으로 되돌린다.
    public event Action OnDungeonExited;

    // 레이아웃이 완성됐을 때(방 목록, 전투방 수). 진행 표시 UI 등이 구독한다.
    public event Action<IReadOnlyList<DungeonRoom>, int> OnDungeonGenerated;

    #endregion
    #region 런타임 변수

    Transform generatedRoot;

    // 풀마다 아직 안 뽑힌 방 뭉치. 한 판 동안 유지해, 구간이 바뀌어도 풀을 다 쓰기 전에는 같은 방이 다시 나오지 않게 한다.
    // 구간마다 새로 섞으면 기믹 구간이 네 번 나오는 긴 판에서 같은 방을 금세 또 만난다.
    readonly Dictionary<List<GameObject>, List<GameObject>> pickBuckets = new();

    // 방을 실제로 놓기 전에 짜 두는 계획 한 줄.
    readonly struct PlannedRoom {
        public readonly GameObject Prefab;
        public readonly DungeonRoom.RoomRole Role;
        public readonly float SecretBranchChance;

        public PlannedRoom(GameObject prefab, DungeonRoom.RoomRole role, float secretBranchChance) {
            Prefab = prefab;
            Role = role;
            SecretBranchChance = secretBranchChance;
        }
    }

    #endregion
    #region 기본 리듬

    // 인스펙터 sequence 가 비었을 때 쓰는 기본 구간 배열. 가운데 방 11~14개 + 입구 · 상점 · 출구 = 한 판 14~17개.
    // 기믹과 전투를 번갈아 끼워 "기믹 다 하고 전투 다 하는" 밋밋한 직선 구조를 피하고, 수직 갱도를 앞뒤 반에
    // 하나씩 나눠 둔다. 상점은 여기 적지 않는다 — Generate 가 목록 한가운데에 끼운다(맨 위 설명 참고).
    // 전투방은 3~4개다. 전부 클리어해야 보상이 나오므로 더 늘리면 한 판이 전투에 끌려다니게 된다.
    static List<DungeonSegment> DefaultSequence() {
        return new List<DungeonSegment> {
            new() { label = "도입 기믹",     category = DungeonRoom.RoomRole.PlatformingHazard, minRooms = 1, maxRooms = 1, secretBranchChance = 0.35f },
            new() { label = "첫 교전",       category = DungeonRoom.RoomRole.CombatArena,      minRooms = 1, maxRooms = 1 },
            new() { label = "초반 기믹",     category = DungeonRoom.RoomRole.PlatformingHazard, minRooms = 2, maxRooms = 2, secretBranchChance = 0.4f },
            new() { label = "첫 수직 갱도",  category = DungeonRoom.RoomRole.VerticalClimb,    minRooms = 1, maxRooms = 1, secretBranchChance = 0.6f },
            new() { label = "중반 기믹",     category = DungeonRoom.RoomRole.PlatformingHazard, minRooms = 1, maxRooms = 2, secretBranchChance = 0.5f },
            new() { label = "중반 교전",     category = DungeonRoom.RoomRole.CombatArena,      minRooms = 1, maxRooms = 2 },
            new() { label = "후반 기믹",     category = DungeonRoom.RoomRole.PlatformingHazard, minRooms = 1, maxRooms = 2, secretBranchChance = 0.5f },
            new() { label = "둘째 수직 갱도", category = DungeonRoom.RoomRole.VerticalClimb,    minRooms = 1, maxRooms = 1, secretBranchChance = 0.6f },
            new() { label = "마무리 기믹",   category = DungeonRoom.RoomRole.PlatformingHazard, minRooms = 1, maxRooms = 1, secretBranchChance = 0.35f },
            new() { label = "마지막 교전",   category = DungeonRoom.RoomRole.CombatArena,      minRooms = 1, maxRooms = 1 },
        };
    }

    #endregion
    #region 생성 · 해체

    public List<DungeonRoom> Generate() {
        Teardown(); // 이전 잔여 인스턴스가 있으면 먼저 정리한다.
        pickBuckets.Clear();

        generatedRoot = new GameObject("GeneratedDungeon").transform;
        generatedRoot.position = dungeonOrigin != null ? dungeonOrigin.position : Vector3.zero;

        var rooms = new List<DungeonRoom>();
        var branchParents = new List<DungeonRoom>(); // 곁가지가 뽑힌 방. 본 동선이 다 놓인 뒤에 한꺼번에 배치한다.

        // 1. 가운데 방 목록을 먼저 짠다. 상점 자리를 전체 개수로 정해야 해서, 놓으면서 정할 수가 없다.
        List<PlannedRoom> plan = PlanMiddleRooms();
        EnsureShop(plan);

        // 2. 입구 — 항상 맨 앞.
        DungeonRoom current = SpawnRoom(PickPrefab(entryRoomPrefabs), null);
        if (current != null) rooms.Add(current);

        // 3. 가운데 방을 순서대로 잇는다. 하나를 못 만들어도 직전 방에 다음 방을 이어야 하므로,
        //    실패한 결과로 current 를 덮어쓰지 않는다(덮어쓰면 다음 방이 던전 원점에 겹쳐 놓인다).
        foreach (PlannedRoom planned in plan) {
            DungeonRoom room = SpawnRoom(planned.Prefab, current);
            if (room == null) continue;

            current = room;
            rooms.Add(room);
            if (RollSecretBranch(room, planned.SecretBranchChance)) branchParents.Add(room);
        }

        // 4. 출구 — 항상 맨 뒤.
        current = SpawnRoom(PickPrefab(exitRoomPrefabs), current);
        if (current != null) rooms.Add(current);

        // 본 동선의 앞뒤를 잇는다. 방은 다음 방이 전투방이면 들어서는 순간 그 방 몬스터를 미리 세워 둔다(DungeonRoom.PrepareCombat).
        // 곁가지는 rooms 에 들지 않으므로 끼어들지 않는다 — 곁가지에서 돌아오면 부모 방 트리거를 다시 밟아 똑같이 처리된다.
        for (int i = 0; i + 1 < rooms.Count; i++) rooms[i].nextRoom = rooms[i + 1];

        // 5. 곁가지 — 본 동선의 높이가 전부 정해져야 그 위의 빈 줄을 잡을 수 있다(수직 갱도가 뒤에 오면 던전 꼭대기가 높아진다).
        SpawnSecretBranches(rooms, branchParents);

        InjectRoomConfig();

        int combatRooms = 0;
        foreach (DungeonRoom r in rooms) {
            if (r.role == DungeonRoom.RoomRole.CombatArena && r.spawner != null) combatRooms++;
        }
        OnDungeonGenerated?.Invoke(rooms, combatRooms);

        return rooms;
    }

    // 생성된 방을 통째로 치운다. generatedRoot 하나만 Destroy 하면 그 아래 방·몬스터가 전부 함께 사라진다.
    public void Teardown() {
        if (generatedRoot == null) return;

        Destroy(generatedRoot.gameObject);
        generatedRoot = null;
    }

    // 입구방 이탈 트리거(DungeonExitZone)와 출구방 거울(DungeonExitMirror)이 호출한다.
    //
    // 해체를 여기서 하지 않고 게이트에 넘기는 이유: 게이트는 퇴장 연출(암전)이 끝난 뒤에 Teardown 을 부른다.
    // 여기서 바로 치워 버리면 플레이어 눈앞에서 방이 통째로 사라진 다음에야 화면이 어두워진다.
    // 다만 아무도 구독하지 않았다면 치워 줄 사람이 없으므로 그때는 직접 해체한다.
    public void NotifyExit() {
        if (OnDungeonExited == null) {
            Teardown();
            return;
        }

        OnDungeonExited.Invoke();
    }

    #endregion
    #region 던전 값 주입

    // 프리팹은 씬·던전 값을 참조할 수 없으므로, 생성 직후 모든 스폰어·이탈 트리거에 던전 값을 넣어 준다.
    void InjectRoomConfig() {
        if (generatedRoot == null) return;

        foreach (DungeonRoomSpawner spawner in generatedRoot.GetComponentsInChildren<DungeonRoomSpawner>(true)) {
            spawner.injectedPool = dungeonMonsterPool;
            spawner.countMultiplier = monsterCountMultiplier;
            spawner.perRoomCap = maxMonstersPerRoom;
            spawner.monsterParent = generatedRoot;
        }

        foreach (DungeonExitZone zone in generatedRoot.GetComponentsInChildren<DungeonExitZone>(true)) {
            zone.Configure(this);
        }

        foreach (DungeonExitMirror exitMirror in generatedRoot.GetComponentsInChildren<DungeonExitMirror>(true)) {
            exitMirror.Configure(this);
        }
    }

    #endregion
    #region 방 배치

    List<GameObject> PoolFor(DungeonRoom.RoomRole role) {
        return role switch {
            DungeonRoom.RoomRole.Entry => entryRoomPrefabs,
            DungeonRoom.RoomRole.PlatformingHazard => hazardRoomPrefabs,
            DungeonRoom.RoomRole.CombatArena => combatRoomPrefabs,
            DungeonRoom.RoomRole.VerticalClimb => verticalRoomPrefabs,
            DungeonRoom.RoomRole.SecretBranch => secretRoomPrefabs,
            DungeonRoom.RoomRole.Exit => exitRoomPrefabs,
            DungeonRoom.RoomRole.Shop => shopRoomPrefabs,
            _ => hazardRoomPrefabs,
        };
    }

    // 구간을 펼쳐 가운데 방 목록을 짠다. 입구 · 출구 구간은 Generate 가 따로 붙이므로 건너뛴다.
    List<PlannedRoom> PlanMiddleRooms() {
        var plan = new List<PlannedRoom>();

        List<DungeonSegment> steps = (sequence != null && sequence.Count > 0) ? sequence : DefaultSequence();
        foreach (DungeonSegment step in steps) {
            if (step == null) continue;
            if (step.category == DungeonRoom.RoomRole.Entry || step.category == DungeonRoom.RoomRole.Exit) continue;

            int lo = Mathf.Max(1, Mathf.Min(step.minRooms, step.maxRooms));
            int hi = Mathf.Max(lo, Mathf.Max(step.minRooms, step.maxRooms));
            int count = UnityEngine.Random.Range(lo, hi + 1);

            foreach (GameObject prefab in PickPrefabs(PoolFor(step.category), count)) {
                plan.Add(new PlannedRoom(prefab, step.category, step.secretBranchChance));
            }
        }

        return plan;
    }

    // 상점이 계획에 없으면 가운데에 하나 끼운다. 구간에 상점을 직접 적었다면 그 자리를 존중하고 더 끼우지 않는다.
    // 짝수 개일 때 뒤쪽 가운데로 들어가는 이유: 앞쪽에 서면 코인이 덜 모인 채로 상점을 만나 살 것이 없다.
    void EnsureShop(List<PlannedRoom> plan) {
        foreach (PlannedRoom planned in plan) {
            if (planned.Role == DungeonRoom.RoomRole.Shop) return;
        }

        GameObject shop = PickPrefab(shopRoomPrefabs);
        if (shop == null) {
            Debug.LogError("[DungeonGenerator] shopRoomPrefabs 가 비어 있어 상점 방 없이 생성합니다. " +
                           "Tools ▸ FCC ▸ Dungeon ▸ Place Dungeon Rig In Scene 으로 풀을 다시 채우세요.", this);
            return;
        }

        plan.Insert((plan.Count + 1) / 2, new PlannedRoom(shop, DungeonRoom.RoomRole.Shop, 0f));
    }

    // previous 가 있으면 새 방의 entryAnchor 를 previous 의 exitAnchor 위치에 맞춰 통째로 옮긴다.
    DungeonRoom SpawnRoom(GameObject prefab, DungeonRoom previous) {
        if (prefab == null) {
            Debug.LogWarning("[DungeonGenerator] 방 프리팹 풀 중 하나가 비어 있어 그 단계를 건너뜁니다.", this);
            return null;
        }

        GameObject instance = Instantiate(prefab, generatedRoot);
        DungeonRoom room = instance.GetComponent<DungeonRoom>();
        if (room == null) {
            Debug.LogError($"[DungeonGenerator] '{prefab.name}' 에 DungeonRoom 이 없습니다.", this);
            Destroy(instance);
            return null;
        }

        if (previous == null) {
            instance.transform.position = generatedRoot.position;
        }
        else if (room.entryAnchor != null && previous.exitAnchor != null) {
            instance.transform.position += previous.exitAnchor.position - room.entryAnchor.position;
        }
        else {
            Debug.LogWarning($"[DungeonGenerator] '{prefab.name}' 또는 이전 방에 소켓(entryAnchor/exitAnchor)이 비어 있어 정렬하지 못했습니다.", this);
        }

        return room;
    }

    bool RollSecretBranch(DungeonRoom parent, float chance) {
        if (parent == null || parent.branchAnchor == null) return false;
        return chance > 0f && UnityEngine.Random.value < chance;
    }

    // 곁가지 방을 본 동선 전체의 꼭대기보다 위, 한 줄에 왼쪽부터 늘어놓고 부모 방과 문으로 잇는다.
    //
    // 예전에는 곁가지 방의 entryAnchor 를 부모 방의 branchAnchor 에 바로 맞췄다. branchAnchor 는 부모 방 안쪽
    // 발판 위라 곁가지 상자(16×12)가 부모 방 한가운데에 통째로 겹쳤고, 수직 갱도에서는 곁가지의 벽·바닥이
    // 오르는 길을 막았다. 본 동선과 절대 겹치지 않는 줄에 따로 두면 방 조합이 어떻게 나와도 안전하다.
    void SpawnSecretBranches(List<DungeonRoom> mainRooms, List<DungeonRoom> parents) {
        if (parents.Count == 0) return;

        Physics2D.SyncTransforms(); // 방을 옮긴 직후라 콜라이더 경계가 아직 옮기기 전 자리를 가리킨다.

        float layoutTop = float.MinValue;
        foreach (DungeonRoom r in mainRooms) {
            if (TryGetRoomRect(r, out Rect rect)) layoutTop = Mathf.Max(layoutTop, rect.yMax);
        }
        if (layoutTop == float.MinValue) layoutTop = generatedRoot.position.y;

        float rowBottom = layoutTop + branchRowGap;
        float cursorRight = float.MinValue; // 직전 곁가지 방의 오른쪽 끝. 곁가지끼리 겹치지 않게 민다.

        foreach (DungeonRoom parent in parents) {
            GameObject prefab = PickPrefab(secretRoomPrefabs);
            if (prefab == null) return;

            GameObject instance = Instantiate(prefab, generatedRoot);
            DungeonRoom room = instance.GetComponent<DungeonRoom>();
            if (room == null || room.entranceDoor == null || room.returnDoor == null) {
                // 문이 없으면 들어갈 길이 없는 방이 허공에 뜰 뿐이라 만들지 않는다.
                Debug.LogWarning($"[DungeonGenerator] 곁가지 '{prefab.name}' 에 DungeonRoom 의 entranceDoor/returnDoor 가 비어 있어 건너뜁니다.", this);
                Destroy(instance);
                continue;
            }

            Physics2D.SyncTransforms();
            if (!TryGetRoomRect(room, out Rect secretRect)) {
                secretRect = new Rect(instance.transform.position, Vector2.zero);
            }

            // 부모 방 바로 위쪽에 두려고 하되, 앞선 곁가지와 겹치면 오른쪽으로 민다.
            float left = parent.branchAnchor.position.x - secretRect.width / 2f;
            if (cursorRight != float.MinValue) left = Mathf.Max(left, cursorRight + branchSpacing);

            instance.transform.position += new Vector3(left - secretRect.xMin, rowBottom - secretRect.yMin, 0f);
            cursorRight = left + secretRect.width;

            // 입구 문은 방을 옮긴 뒤에 꺼내야 한다. 먼저 꺼내면 방을 옮길 때 함께 딸려 간다.
            room.entranceDoor.transform.position = GroundBelow(parent);
            DungeonBranchDoor.Link(room.entranceDoor, parent, room.returnDoor, room);
        }
    }

    // 방 루트의 트리거 콜라이더가 곧 방의 영역이다(DungeonRoom 진입 판정과 같은 기준).
    bool TryGetRoomRect(DungeonRoom room, out Rect rect) {
        rect = default;
        Collider2D col = room != null ? (room.roomTrigger != null ? room.roomTrigger : room.GetComponent<Collider2D>()) : null;
        if (col == null) return false;

        Bounds b = col.bounds;
        rect = Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
        return true;
    }

    // branchAnchor 는 방마다 발판 위 몸통 높이이거나 허공에 떠 있기도 하다. 문은 땅에 서 있어야 하므로 아래 바닥을 찾아 세운다.
    // 지형을 타일맵으로 옮기면서 소켓 밑 발판이 사라진 방도 있어(Room_Vertical_B 의 벽감), 가까운 곳만 보지 않고
    // 부모 방 바닥까지 내려가며 찾는다. 이웃 방 지형이 부모 방 영역으로 삐져나와 있을 수 있으므로 부모 방 것만 인정한다.
    // 끝내 못 찾으면 소켓 높이 규칙(바닥 + 1.1)을 거꾸로 적용해 둔다.
    readonly RaycastHit2D[] groundHits = new RaycastHit2D[16];

    Vector3 GroundBelow(DungeonRoom parent) {
        Vector3 anchor = parent.branchAnchor.position;
        float depth = TryGetRoomRect(parent, out Rect rect) ? Mathf.Max(0f, anchor.y - rect.yMin) : 8f;

        var filter = new ContactFilter2D {
            useLayerMask = true,
            layerMask = branchDoorGroundMask.value != 0 ? branchDoorGroundMask : (LayerMask)LayerMask.GetMask("ground"),
            useTriggers = false, // 가시밭·낙사 영역 같은 트리거 위에 문을 세우지 않는다.
        };

        int count = Physics2D.Raycast(anchor, Vector2.down, filter, groundHits, depth);
        RaycastHit2D best = default;
        for (int i = 0; i < count; i++) {
            RaycastHit2D hit = groundHits[i];
            if (hit.distance <= 0f) continue;          // 콜라이더 안에서 시작한 판정은 바닥이 아니다.
            if (hit.normal.y < 0.5f) continue;         // 벽 옆면에 걸린 것은 설 자리가 아니다.
            if (!hit.collider.transform.IsChildOf(parent.transform)) continue;
            if (best.collider == null || hit.distance < best.distance) best = hit;
        }

        if (best.collider != null) return new Vector3(anchor.x, best.point.y, anchor.z);
        return anchor + Vector3.down * 1.1f;
    }

    #endregion
    #region 랜덤 선택

    GameObject PickPrefab(List<GameObject> pool) {
        if (pool == null || pool.Count == 0) return null;
        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    // count 만큼 뽑는다. 셔플 뭉치 방식이라 풀을 다 쓰기 전까지는 같은 방이 다시 나오지 않는다.
    // 뭉치는 풀마다 한 판 동안 이어 쓴다(pickBuckets). 구간이 달라도 같은 풀에서 뽑으면 앞 구간에서 나온 방은 빠져 있다.
    List<GameObject> PickPrefabs(List<GameObject> pool, int count) {
        var result = new List<GameObject>(Mathf.Max(0, count));
        if (pool == null || pool.Count == 0 || count <= 0) return result;

        if (!pickBuckets.TryGetValue(pool, out List<GameObject> bucket)) {
            bucket = new List<GameObject>();
            pickBuckets[pool] = bucket;
        }

        GameObject previous = null;
        while (result.Count < count) {
            if (bucket.Count == 0) {
                bucket.AddRange(pool);
                Shuffle(bucket);

                // 뭉치를 새로 섞은 첫 방이 방금 뽑은 방과 같으면 같은 방이 연달아 붙는다. 한 칸 뒤와 바꿔 피한다.
                int top = bucket.Count - 1;
                if (top > 0 && bucket[top] == previous) (bucket[top], bucket[top - 1]) = (bucket[top - 1], bucket[top]);
            }
            int last = bucket.Count - 1;
            previous = bucket[last];
            result.Add(previous);
            bucket.RemoveAt(last);
        }
        return result;
    }

    void Shuffle(List<GameObject> list) {
        for (int i = list.Count - 1; i > 0; i--) {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    #endregion
    #region 에디터 표시

#if UNITY_EDITOR
    void OnDrawGizmos() {
        if (dungeonOrigin != null) {
            Gizmos.color = new Color(1f, 0.4f, 0.9f, 0.9f);
            Gizmos.DrawWireCube(dungeonOrigin.position, new Vector3(3f, 3f, 0f));
        }

        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
        Vector3 left = new(-1000f, fallYThreshold, 0f);
        Vector3 right = new(1000f, fallYThreshold, 0f);
        Gizmos.DrawLine(left, right); // 낙사 기준선.
    }
#endif

    #endregion
}
