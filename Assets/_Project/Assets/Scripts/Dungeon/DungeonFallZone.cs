using System.Collections.Generic;
using UnityEngine;

// 구덩이·가시밭 아래에 깔아 두는 낙사 트리거. 닿으면 현재 방 리스폰 지점으로 되돌린다.
// DungeonGenerator.fallYThreshold 로도 같은 처리가 되지만, 방마다 바닥 높이가 다를 때
// 지점별로 정확히 막고 싶으면 이 트리거를 쓴다.
// **Is Trigger 콜라이더를 붙이세요.**
//
// 낙사 트리거는 구덩이 밑, 즉 자기 방 영역보다 아래에 매달려 있다. 그래서 방을 이어 붙이면 이 트리거가
// 이웃 방의 공간으로 삐져나갈 수 있다. 실제로 수직 갱도 꼭대기에 기믹 방이 붙었을 때, 기믹 방의 낙사
// 트리거가 갱도 한가운데 걸려 발판을 딛고 오르던 플레이어가 영문도 모른 채 방 처음으로 되돌려졌다.
// 그래서 플레이어가 "다른 방의 영역 안" 에 있으면 그 자리는 이웃 방의 땅으로 보고 반응하지 않는다.
[RequireComponent(typeof(Collider2D))]
public class DungeonFallZone : MonoBehaviour {
    #region 인스펙터 변수

    [Header("감지")]
    public string playerTag = "Player";

    #endregion
    #region 컴포넌트 변수

    DungeonRoom ownerRoom; // 이 트리거가 속한 방. 자기 방 영역과 겹치는 것은 막을 이유가 없어 제외한다.
    ContactFilter2D roomFilter;
    readonly List<Collider2D> overlapBuffer = new(); // 판정마다 새로 할당하지 않도록 재사용한다.

    #endregion
    #region 유니티 라이프 사이클

    void Reset() {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void Awake() {
        ownerRoom = GetComponentInParent<DungeonRoom>();
        roomFilter = new ContactFilter2D {
            useTriggers = true, // 방 영역은 트리거 콜라이더라 이걸 켜야 걸린다.
        };
    }

    void OnTriggerEnter2D(Collider2D other) {
        TryRespawn(other);
    }

    // 진입 순간만 보면, 이웃 방 영역 안에서 들어와 무시된 뒤 그대로 영역 밖으로 걸어 나왔을 때 놓친다.
    // 되돌린 직후에는 무적이 걸려 같은 스텝에 한 번 더 불려도 피해가 겹치지 않는다.
    void OnTriggerStay2D(Collider2D other) {
        TryRespawn(other);
    }

    #endregion
    #region 낙사 판정

    void TryRespawn(Collider2D other) {
        if (!other.CompareTag(playerTag)) return;
        if (DungeonRespawnController.Instance == null) return;
        if (IsInsideOtherRoom(other.bounds.center)) return;

        DungeonRespawnController.Instance.RespawnAtCurrentRoom();
    }

    bool IsInsideOtherRoom(Vector2 point) {
        overlapBuffer.Clear();
        Physics2D.OverlapPoint(point, roomFilter, overlapBuffer);

        foreach (Collider2D col in overlapBuffer) {
            if (!col.TryGetComponent(out DungeonRoom room)) continue;
            if (room == ownerRoom) continue;
            if (room.roomTrigger != col) continue; // 방 루트에 영역 말고 다른 콜라이더가 붙어 있어도 영역으로 오인하지 않는다.
            return true;
        }
        return false;
    }

    #endregion
    #region 에디터 표시

#if UNITY_EDITOR
    void OnDrawGizmos() {
        if (!TryGetComponent<Collider2D>(out Collider2D col)) return;

        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.18f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
#endif

    #endregion
}
