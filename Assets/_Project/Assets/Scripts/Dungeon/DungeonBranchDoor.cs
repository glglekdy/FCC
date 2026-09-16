using System.Collections;
using UnityEngine;

// 곁가지(비밀방)로 드나드는 문. 한 쌍으로 쓴다 — 부모 방에 놓이는 "입구 문" 과 곁가지 방 안의 "귀환 문".
//
// 곁가지 방을 부모 방에 소켓으로 바로 붙이지 않고 문으로 잇는 이유: 방은 전부 벽·천장으로 닫힌 상자라
// 옆에 붙여도 드나들 구멍이 없고, 소켓 지점(발판 위)에 맞춰 붙이면 곁가지 상자가 부모 방 한가운데에 통째로
// 겹친다. 실제로 수직 갱도에서는 곁가지의 벽·바닥이 오르는 길을 가로막았다. 그래서 곁가지 방은 던전 위쪽
// 빈 공간에 따로 만들고, 두 문 사이를 순간이동으로 잇는다.
//
// 두 문 모두 곁가지 방 프리팹 안에 들어 있다. 입구 문은 DungeonGenerator 가 부모 방의 branchAnchor 로
// 옮겨 놓는다 — 부모 방 프리팹마다 문을 심어 두면 곁가지가 안 뽑힌 판에도 문이 남기 때문이다.
// **Is Trigger 콜라이더를 붙이세요.** (PlayerInteractor 가 트리거만 탐지합니다.)
[RequireComponent(typeof(Collider2D))]
public class DungeonBranchDoor : MonoBehaviour, IInteractable {
    #region 인스펙터 변수

    [Header("프롬프트")]
    public string label = "들어가기"; // 플레이어에게 뜨는 문구.
    public Vector3 promptOffset = new(0f, 3.2f, 0f); // 문 윗부분에 프롬프트를 띄울 오프셋. 문 피벗은 발밑이다.

    [Header("도착")]
    public float standHeight = 1.1f; // 도착한 문 발밑에서 플레이어 피벗까지의 높이. 플레이어 피벗이 몸통 한가운데라 바닥에 파묻히지 않게 띄운다.

    [Header("연출")]
    public float fadeOut = 0.2f; // 화면이 검어지는 시간. 방 하나 건너가는 것이라 던전 입장보다 짧게 둔다.
    public float blackHold = 0.1f; // 검은 채로 머무는 시간. 카메라가 새 위치로 따라붙을 틈이다.
    public float fadeIn = 0.3f;
    public float arrivalInvincibleTime = 0.6f; // 막이 걷히는 동안 맞지 않도록 거는 무적.

    #endregion
    #region 런타임 변수

    DungeonBranchDoor destination; // 건너편 문. DungeonGenerator 가 연결한다.
    DungeonRoom destinationRoom;   // 건너편 문이 속한 방. 도착 즉시 리스폰 기준을 바꾸는 데 쓴다.
    bool isTraveling;

    #endregion
    #region IInteractable

    public string InteractLabel => label;
    public bool CanInteract => destination != null && !isTraveling;
    public Vector3 PromptAnchor => transform.position + promptOffset;

    public void Interact(GameObject interactor) {
        if (!CanInteract || interactor == null) return;
        StartCoroutine(Travel(interactor));
    }

    #endregion
    #region 연결

    // 양방향으로 한 번에 잇는다. 한쪽만 이어 두면 들어간 곁가지에서 나올 수 없게 된다.
    public static void Link(DungeonBranchDoor a, DungeonRoom roomA, DungeonBranchDoor b, DungeonRoom roomB) {
        if (a == null || b == null) return;

        a.destination = b;
        a.destinationRoom = roomB;
        b.destination = a;
        b.destinationRoom = roomA;
    }

    #endregion
    #region 이동

    IEnumerator Travel(GameObject interactor) {
        isTraveling = true;

        // PlayerInteractor 가 플레이어 자식에 붙어 있어도 찾아지도록 부모 쪽으로 찾는다.
        Player_move move = interactor.GetComponentInParent<Player_move>();
        GameObject player = move != null ? move.gameObject : interactor;
        Health health = player.GetComponent<Health>();

        if (move != null) move.isMovementLocked = true;
        if (health != null) health.SetInvincible(fadeOut + blackHold + fadeIn + arrivalInvincibleTime);

        yield return Cover(1f, fadeOut);

        player.transform.position = destination.transform.position + Vector3.up * standHeight;
        if (player.TryGetComponent(out Rigidbody2D body)) body.linearVelocity = Vector2.zero;

        // 방 트리거의 진입 판정은 다음 물리 스텝에야 돈다. 그 사이 낙사 판정이 먼저 돌면 떠나온 방의
        // 리스폰 지점으로 끌려가므로, 리스폰 기준은 여기서 바로 바꿔 둔다.
        if (destinationRoom != null && DungeonRespawnController.Instance != null) {
            DungeonRespawnController.Instance.SetCurrentRoom(destinationRoom);
        }

        if (blackHold > 0f) yield return new WaitForSecondsRealtime(blackHold);
        yield return Cover(0f, fadeIn);

        if (move != null) move.isMovementLocked = false;
        isTraveling = false;
    }

    // 페이더가 씬에 없으면 연출 없이 지나간다 (DungeonGate 와 같은 방침).
    IEnumerator Cover(float target, float duration) {
        if (ScreenFader.Instance == null) yield break;
        yield return ScreenFader.Instance.FadeCover(target, duration);
    }

    #endregion
    #region 에디터 표시

#if UNITY_EDITOR
    void OnDrawGizmos() {
        Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 1.3f, new Vector3(1.6f, 2.6f, 0f));
        if (destination != null) Gizmos.DrawLine(transform.position, destination.transform.position);
    }
#endif

    #endregion
}
