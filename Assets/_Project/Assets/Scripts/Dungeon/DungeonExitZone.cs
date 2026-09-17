using UnityEngine;

// 던전 입구방에서 뒤로 빠져나가는 이탈 트리거. 출구방은 닿기만 해도 나가지지 않도록 거울(DungeonExitMirror)로 바뀌었다.
// 밟으면 DungeonGenerator 에 알리고 던전을 해체한다 — 되돌리기·카메라 원복은 DungeonGate 가 맡는다.
//
// 전투방을 다 클리어하기 전에 입구방 뒷문으로 나가면 보상 없이 나가며, 다음 입장 시 완전히 새 레이아웃이
// 생성된다(중간 상태를 보존하지 않는다).
// **Is Trigger 콜라이더를 붙이세요.**
[RequireComponent(typeof(Collider2D))]
public class DungeonExitZone : MonoBehaviour {
    #region 인스펙터 변수

    [Header("감지")]
    public string playerTag = "Player";

    #endregion
    #region 런타임 변수

    // 프리팹은 씬 오브젝트를 참조할 수 없으므로 DungeonGenerator 가 생성 직후 주입한다.
    DungeonGenerator generator;

    #endregion
    #region 유니티 라이프 사이클

    void Reset() {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    public void Configure(DungeonGenerator owner) {
        generator = owner;
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (!other.CompareTag(playerTag)) return;
        if (generator != null) generator.NotifyExit();
    }

    #endregion
    #region 에디터 표시

#if UNITY_EDITOR
    void OnDrawGizmos() {
        if (!TryGetComponent<Collider2D>(out Collider2D col)) return;

        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.18f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.8f);
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
#endif

    #endregion
}
