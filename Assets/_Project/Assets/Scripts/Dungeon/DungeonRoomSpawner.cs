using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 방 프리팹 하나에 종속되는 스폰·전멸 감지 컴포넌트. dungeonId 나 던전 전체 진행 상황은 모르고,
// "이 방에 뿌리기로 한 몬스터를 전부 잡았는가" 만 판정한다.
//
// 구버전은 spawnPoints[i] ↔ monsterPrefabs[i] 두 리스트를 인덱스로 짝지어 한쪽만 늘리면 조용히
// 잘려나갔다. 여기서는 스폰 지점 하나 = 엔트리 하나로 묶어 짝이 어긋날 수 없게 한다.
public class DungeonRoomSpawner : MonoBehaviour {
    #region 스폰 엔트리 정의

    [Serializable]
    public class SpawnEntry {
        // **스폰 위치. 빈 자식 오브젝트를 만들어 연결하세요.**
        public Transform point;

        // 비워두면 던전 몬스터 풀(DungeonGenerator.dungeonMonsterPool)에서 가중치로 뽑는다.
        public GameObject monsterPrefab;

        [Min(0f)]
        public float spawnDelay; // 전투 시작(방 입장) 후 지연 스폰. 0 이면 앞 방에서부터 미리 보인다.

        public bool countTowardClear = true; // 끄면 전멸 판정에서 빠진다(분위기용 잡몹).

        public bool alwaysSpawn; // 던전 배율로 개수가 줄어도 이 엔트리는 항상 스폰된다.
    }

    #endregion
    #region 인스펙터 변수

    [Header("스폰 목록")]
    public List<SpawnEntry> entries = new();

    [Header("이 방에서 실제로 쓸 스폰 지점 개수")]
    // 둘 다 -1 이면 entries 를 전부 쓴다. 값을 주면 [min, max] 사이에서 무작위로 고른다(alwaysSpawn 은 별도로 항상 포함).
    public int minSpawnCount = -1;
    public int maxSpawnCount = -1;

    #endregion
    #region 던전 주입 값

    // 프리팹은 씬·던전 오브젝트를 참조할 수 없으므로 DungeonGenerator 가 생성 직후 채워 넣는다.
    [NonSerialized] public List<DungeonMonsterOption> injectedPool;
    [NonSerialized] public float countMultiplier = 1f; // 고른 지점 개수에 곱한다.
    [NonSerialized] public int perRoomCap = 99;         // 배율을 곱해도 이 수를 넘지 않는다.
    [NonSerialized] public Transform monsterParent;     // 스폰된 몬스터를 여기 자식으로 둔다(던전 해체 시 함께 정리).

    #endregion
    #region 이벤트

    public event Action OnAllMonstersDefeated;

    // 몬스터가 깨어나 전투가 시작된 순간. 방에 들어온 경우와, 앞 방에서 대기 중인 몬스터를 먼저 때린 경우 모두 여기로 온다.
    public event Action OnWoken;

    #endregion
    #region 조회

    public bool IsCleared => cleared;

    #endregion
    #region 런타임 변수

    bool spawnStarted;
    bool awake;            // WakeAll 이 불렸는지. 꺼져 있는 동안 나온 몬스터는 제자리에서 기다린다.
    bool cleared;          // 전멸 이벤트를 한 번만 쏘기 위한 가드.
    int expectedCounted;   // countTowardClear 로 스폰하기로 한 총 마릿수.
    int spawnedCounted;    // 그중 실제로 스폰된 수(지연 스폰 포함).
    int aliveCounted;      // 그중 아직 살아 있는 수.
    bool poolWarned;

    readonly List<SpawnEntry> delayedEntries = new(); // 대기 중에 뽑혔지만 spawnDelay 가 있어 깨어난 뒤에 나올 엔트리.
    readonly List<IDormant> dormantBrains = new();     // 깨울 때 풀어 줄 몬스터 AI.

    #endregion
    #region 스폰 · 깨우기

    // 전투방 앞 방에 들어섰을 때 부른다. 어떤 몬스터가 기다리는지 미리 보이도록 세워 두되, WakeAll 전까지는 제자리에서 기다리게 한다.
    // 중복 호출돼도 한 번만 스폰한다(방 재진입·이벤트 중복 대비).
    //
    // spawnDelay 가 있는 엔트리는 여기서 내지 않는다. 지연 스폰은 들어온 뒤에 뒤늦게 튀어나오는 증원이라
    // 미리 보이면 의미가 없으므로, 깨어난 순간부터 지연을 센다.
    public void SpawnDormant() {
        if (spawnStarted) return;
        spawnStarted = true;

        List<SpawnEntry> chosen = SelectEntries();

        expectedCounted = 0;
        foreach (SpawnEntry e in chosen) {
            if (e == null || e.point == null) continue;
            if (e.countTowardClear) expectedCounted++;
        }

        // 셀 몬스터가 하나도 없으면(빈 방 / 분위기 몹만) 즉시 클리어 처리.
        if (expectedCounted == 0) {
            MarkCleared();
            return;
        }

        foreach (SpawnEntry e in chosen) {
            if (e == null || e.point == null) continue;

            if (!awake && e.spawnDelay > 0f) {
                delayedEntries.Add(e);
                continue;
            }
            StartCoroutine(SpawnRoutine(e));
        }
    }

    // 플레이어가 전투방에 실제로 들어섰을 때 부른다. 앞 방을 거치지 않아 아직 스폰 전이면 스폰부터 한다.
    // 중복 호출돼도 한 번만 깨운다.
    public void WakeAll() {
        if (awake) return;
        awake = true;

        if (!spawnStarted) {
            SpawnDormant(); // awake 가 켜진 뒤라 대기 없이 바로 싸우는 몬스터로 나온다.
        }
        else {
            foreach (IDormant brain in dormantBrains) {
                // 대기 중에 죽은 몬스터는 파괴된 뒤라 C# 참조만 남아 있다. Unity 의 == 로 걸러야 한다.
                if (brain is MonoBehaviour behaviour && behaviour != null) brain.SetDormant(false);
            }

            foreach (SpawnEntry e in delayedEntries) StartCoroutine(SpawnRoutine(e));
        }

        dormantBrains.Clear();
        delayedEntries.Clear();

        OnWoken?.Invoke();
    }

    IEnumerator SpawnRoutine(SpawnEntry e) {
        if (e.spawnDelay > 0f) yield return new WaitForSeconds(e.spawnDelay);

        GameObject prefab = e.monsterPrefab != null ? e.monsterPrefab : PickFromPool();
        if (prefab == null) yield break;

        GameObject monster = Instantiate(prefab, e.point.position, e.point.rotation,
            monsterParent != null ? monsterParent : transform);

        // 분위기 몹도 미리 보이는 동안에는 함께 기다려야 하므로 전멸 셈에서 빠지기 전에 재운다.
        if (!awake) HoldDormant(monster);

        if (!e.countTowardClear) yield break;

        spawnedCounted++;

        if (monster.TryGetComponent(out Health health)) {
            aliveCounted++;
            health.OnDeath += _ => HandleMonsterDeath();
        }
        else {
            // Health 가 없으면 죽는 시점을 알 수 없다. 전멸 판정이 영영 안 끝나는 것을 막기 위해 셈에서 뺀다.
            Debug.LogWarning($"[DungeonRoomSpawner] '{prefab.name}' 에 Health 가 없어 전멸 판정에서 제외합니다.", this);
            expectedCounted = Mathf.Max(0, expectedCounted - 1);
        }

        CheckCleared();
    }

    // 몬스터 AI 를 재우고, 맞으면 방 전체를 깨운다. 앞 방에서 원거리로 먼저 때렸는데 제자리에 서서 맞기만 하면
    // 고장 난 것처럼 보이고, 반격 없이 하나씩 잡을 수도 있기 때문이다.
    void HoldDormant(GameObject monster) {
        foreach (IDormant brain in monster.GetComponentsInChildren<IDormant>()) {
            brain.SetDormant(true);
            dormantBrains.Add(brain);
        }

        // 몬스터가 먼저 파괴되면 이 구독도 함께 사라지므로 따로 해제하지 않는다.
        if (monster.TryGetComponent(out Health health)) {
            health.OnDamaged += (_, _) => WakeAll();
        }
    }

    void HandleMonsterDeath() {
        aliveCounted = Mathf.Max(0, aliveCounted - 1);
        CheckCleared();
    }

    // 스폰이 모두 끝났고(지연 포함) 살아 있는 개체가 없을 때만 전멸.
    void CheckCleared() {
        if (spawnedCounted >= expectedCounted && aliveCounted == 0) MarkCleared();
    }

    void MarkCleared() {
        if (cleared) return;
        cleared = true;

        OnAllMonstersDefeated?.Invoke();
    }

    #endregion
    #region 엔트리 선택

    List<SpawnEntry> SelectEntries() {
        var always = new List<SpawnEntry>();
        var optional = new List<SpawnEntry>();
        foreach (SpawnEntry e in entries) {
            if (e == null || e.point == null) continue;
            (e.alwaysSpawn ? always : optional).Add(e);
        }

        int total = always.Count + optional.Count;

        // 기준 개수: 범위를 안 줬으면 전부, 줬으면 [min, max] 무작위.
        int baseCount;
        if (minSpawnCount < 0 && maxSpawnCount < 0) {
            baseCount = total;
        }
        else {
            int lo = Mathf.Clamp(minSpawnCount < 0 ? 0 : minSpawnCount, 0, total);
            int hi = Mathf.Clamp(maxSpawnCount < 0 ? total : maxSpawnCount, lo, total);
            baseCount = UnityEngine.Random.Range(lo, hi + 1);
        }

        // 던전 배율 적용 후 상한·항상 스폰 수로 클램프.
        int target = Mathf.RoundToInt(baseCount * Mathf.Max(0f, countMultiplier));
        target = Mathf.Clamp(target, always.Count, Mathf.Min(total, perRoomCap));

        Shuffle(optional);
        int fill = Mathf.Clamp(target - always.Count, 0, optional.Count);

        var result = new List<SpawnEntry>(always);
        for (int i = 0; i < fill; i++) result.Add(optional[i]);
        return result;
    }

    GameObject PickFromPool() {
        if (injectedPool == null || injectedPool.Count == 0) {
            if (!poolWarned) {
                Debug.LogWarning($"[DungeonRoomSpawner] '{name}' 엔트리에 몬스터가 비어 있는데 던전 몬스터 풀도 비어 있어 스폰을 건너뜁니다.", this);
                poolWarned = true;
            }
            return null;
        }

        float totalWeight = 0f;
        foreach (DungeonMonsterOption o in injectedPool) {
            if (o != null && o.prefab != null) totalWeight += Mathf.Max(0f, o.weight);
        }
        if (totalWeight <= 0f) return null;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        foreach (DungeonMonsterOption o in injectedPool) {
            if (o == null || o.prefab == null) continue;
            roll -= Mathf.Max(0f, o.weight);
            if (roll <= 0f) return o.prefab;
        }
        return null;
    }

    void Shuffle(List<SpawnEntry> list) {
        for (int i = list.Count - 1; i > 0; i--) {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    #endregion
    #region 에디터 표시

#if UNITY_EDITOR
    void OnDrawGizmos() {
        Gizmos.color = new Color(1f, 0.35f, 0.3f, 0.85f);
        foreach (SpawnEntry e in entries) {
            if (e == null || e.point == null) continue;
            Gizmos.DrawWireSphere(e.point.position, 0.35f);
            Gizmos.DrawLine(transform.position, e.point.position);
        }
    }
#endif

    #endregion
}
