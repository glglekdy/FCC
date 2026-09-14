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

    #endregion
    #region 기본 리듬

    // 인스펙터 sequence 가 비었을 때 쓰는 기본 구간 배열.
    // 기믹과 전투를 번갈아 끼워 "기믹 다 하고 전투 다 하는" 밋밋한 직선 구조를 피한다.
    static List<DungeonSegment> DefaultSequence() {
        return new List<DungeonSegment> {
            new() { label = "도입 기믹",   category = DungeonRoom.RoomRole.PlatformingHazard, minRooms = 1, maxRooms = 1, secretBranchChance = 0.35f },
            new() { label = "첫 교전",     category = DungeonRoom.RoomRole.CombatArena,      minRooms = 1, maxRooms = 1 },
            new() { label = "중반 기믹",   category = DungeonRoom.RoomRole.PlatformingHazard, minRooms = 1, maxRooms = 2, secretBranchChance = 0.5f },
            new() { label = "중반 교전",   category = DungeonRoom.RoomRole.CombatArena,      minRooms = 1, maxRooms = 2 },
            new() { label = "수직 갱도",   category = DungeonRoom.RoomRole.VerticalClimb,    minRooms = 1, maxRooms = 1, secretBranchChance = 0.6f },
            new() { label = "마무리 기믹", category = DungeonRoom.RoomRole.PlatformingHazard, minRooms = 1, maxRooms = 1, secretBranchChance = 0.35f },
        };
    }

    #endregion
    #region 생성 · 해체

    public List<DungeonRoom> Generate() {
        Teardown(); // 이전 잔여 인스턴스가 있으면 먼저 정리한다.

        generatedRoot = new GameObject("GeneratedDungeon").transform;
        generatedRoot.position = dungeonOrigin != null ? dungeonOrigin.position : Vector3.zero;

        var rooms = new List<DungeonRoom>();

        // 1. 입구 — 항상 맨 앞.
        DungeonRoom current = SpawnRoom(PickPrefab(entryRoomPrefabs), null);
        if (current != null) rooms.Add(current);

        // 2. 가운데 구간 펼치기.
        List<DungeonSegment> steps = (sequence != null && sequence.Count > 0) ? sequence : DefaultSequence();
        foreach (DungeonSegment step in steps) {
            if (step == null) continue;
            if (step.category == DungeonRoom.RoomRole.Entry || step.category == DungeonRoom.RoomRole.Exit) continue; // 입·출구는 여기서 만들지 않는다.

            int lo = Mathf.Max(1, Mathf.Min(step.minRooms, step.maxRooms));
            int hi = Mathf.Max(lo, Mathf.Max(step.minRooms, step.maxRooms));
            int count = UnityEngine.Random.Range(lo, hi + 1);

            foreach (GameObject prefab in PickPrefabs(PoolFor(step.category), count)) {
                current = SpawnRoom(prefab, current);
                if (current == null) continue;
                rooms.Add(current);
                TrySpawnSecretBranch(current, step.secretBranchChance);
            }
        }

        // 3. 출구 — 항상 맨 뒤.
        current = SpawnRoom(PickPrefab(exitRoomPrefabs), current);
        if (current != null) rooms.Add(current);

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

    // 입구/출구 이탈 트리거가 호출한다.
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
            _ => hazardRoomPrefabs,
        };
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

    void TrySpawnSecretBranch(DungeonRoom parent, float chance) {
        if (parent == null || parent.branchAnchor == null) return;
        if (chance <= 0f || UnityEngine.Random.value >= chance) return;

        GameObject prefab = PickPrefab(secretRoomPrefabs);
        if (prefab == null) return;

        GameObject instance = Instantiate(prefab, generatedRoot);
        DungeonRoom room = instance.GetComponent<DungeonRoom>();

        if (room != null && room.entryAnchor != null) {
            instance.transform.position += parent.branchAnchor.position - room.entryAnchor.position;
        }
        else {
            Debug.LogWarning($"[DungeonGenerator] 곁가지 '{prefab.name}' 에 entryAnchor 가 없어 정렬하지 못했습니다.", this);
        }
    }

    #endregion
    #region 랜덤 선택

    GameObject PickPrefab(List<GameObject> pool) {
        if (pool == null || pool.Count == 0) return null;
        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    // count 만큼 뽑는다. 셔플 뭉치 방식이라 풀을 다 쓰기 전까지는 같은 방이 연속으로 나오지 않는다.
    List<GameObject> PickPrefabs(List<GameObject> pool, int count) {
        var result = new List<GameObject>(Mathf.Max(0, count));
        if (pool == null || pool.Count == 0 || count <= 0) return result;

        var bucket = new List<GameObject>();
        while (result.Count < count) {
            if (bucket.Count == 0) {
                bucket.AddRange(pool);
                Shuffle(bucket);
            }
            int last = bucket.Count - 1;
            result.Add(bucket[last]);
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
