using UnityEngine;
using UnityEngine.InputSystem;

// T 키를 누르면 가장 가까운 뒷세계 입구(DungeonGate)가 어느 쪽에 있는지 화살표로 알려 준다.
// **플레이어(PlayerInput 이 붙은 오브젝트)에 붙이세요.**
//
// 길을 잃었을 때 방향만 짚어 주는 나침반이다. 거리·경로는 알려 주지 않는다 — 메트로배니아에서 길 찾기 자체가
// 플레이의 일부라, 자동으로 끌고 가면 맵을 읽는 재미가 사라진다. 그래서 잠깐 떴다 사라지고 쿨타임도 둔다.
//
// 화살표는 플레이어의 자식으로 두지 않는다. 플레이어는 좌우를 스케일 반전으로 표현해서(Player_move),
// 자식으로 달면 왼쪽을 볼 때마다 화살표가 뒤집힌다. 대신 매 프레임 플레이어 둘레로 따라다니게 한다.
public class Player_GateCompass : MonoBehaviour {
    #region 인스펙터 변수

    [Header("연결")]
    // **Prefabs/VFX/GateCompassArrow.prefab 을 연결하세요.** (Tools ▸ FCC ▸ Build Gate Compass Arrow)
    public SpriteRenderer arrowPrefab;

    [Header("표시")]
    public float radius = 1.8f;      // 플레이어 중심에서 화살표까지의 거리.
    public Vector2 centerOffset = new(0f, 0.6f); // 플레이어 발밑이 원점이라, 원을 그리는 중심을 몸통 높이로 올린다.
    public float holdSeconds = 5f;   // 다 보이는 채로 머무는 시간.
    public float fadeSeconds = 1.5f; // 그 뒤 천천히 사라지는 시간.

    [Header("쿨타임")]
    public float cooldown = 15f; // 누른 순간부터 다시 누를 수 있게 되기까지.

    #endregion
    #region 컴포넌트 변수

    Player_move move;
    SpriteRenderer arrow;
    Transform target;

    float readyTime;   // 이 시각(Time.time)이 지나야 다시 쓸 수 있다.
    float elapsed;     // 화살표가 떠 있은 시간.
    float baseAlpha;   // 프리팹에 칠해 둔 알파. 사라질 때 여기서부터 내린다.

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        move = GetComponent<Player_move>();

        if (arrowPrefab == null) {
            Debug.LogError($"[Player_GateCompass] '{name}' — arrowPrefab 이 연결되지 않아 방향 표시를 띄울 수 없습니다.", this);
        }
    }

    // 화살표는 플레이어의 자식이 아니라 따로 떠 있는 오브젝트라, 플레이어가 사라져도 남는다(HealthBar 와 같은 정리 방식).
    void OnDestroy() {
        if (arrow != null) Destroy(arrow.gameObject);
    }

    // 표시 중에만 돈다. 플레이어가 걸어도 화살표가 옆에 따라붙고, 방향도 계속 다시 잰다.
    void LateUpdate() {
        if (arrow == null || !arrow.gameObject.activeSelf) return;

        // 던전에 들어갔거나 씬이 바뀌어 대상이 사라지면 그대로 거둔다.
        if (target == null) {
            Hide();
            return;
        }

        Vector2 center = (Vector2)transform.position + centerOffset;
        Vector2 toTarget = (Vector2)target.position - center;
        if (toTarget.sqrMagnitude > 0.0001f) {
            Vector2 direction = toTarget.normalized;
            arrow.transform.position = center + direction * radius;
            arrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        // 연출은 unscaledDeltaTime 기준이다. 히트스톱에 걸려도 5초는 5초로 흘러야 한다(PlayerInteractor 프롬프트와 같은 이유).
        elapsed += Time.unscaledDeltaTime;
        if (elapsed <= holdSeconds) return;

        float fade = fadeSeconds <= 0f ? 1f : (elapsed - holdSeconds) / fadeSeconds;
        if (fade >= 1f) {
            Hide();
            return;
        }

        Color c = arrow.color;
        c.a = baseAlpha * (1f - fade);
        arrow.color = c;
    }

    #endregion
    #region 입력

    // PlayerInput 의 메시지 방식(BroadcastMessages)으로 들어온다. 액션 이름 `FindGate`(Client ▸ Player, 기본 T)와 맞춰야 한다.
    void OnFindGate(InputValue value) {
        if (!value.isPressed) return;
        if (Time.time < readyTime) return;

        // 대사·컷씬 중과 멈춘 화면(일시정지 · 정비 화면)에서는 받지 않는다. 상호작용(F)과 같은 규칙이다.
        if (move != null && move.isMovementLocked) return;
        if (Time.timeScale == 0f) return;

        // 뒷세계 안에서는 쓰지 않는다. 입구 거울은 바깥 세계에 그대로 서 있어서, 방향을 그려 봐야 벽 너머 엉뚱한 곳을 가리킨다.
        if (DungeonRespawnController.Instance != null) return;

        Transform found = FindNearestGate();
        if (found == null) return; // 이 씬에 입구가 없으면 쿨타임도 쓰지 않는다. 헛누름으로 15초를 잠그면 억울하다.

        target = found;
        Show();
        readyTime = Time.time + cooldown;
    }

    #endregion
    #region 대상 찾기

    // 아직 들어갈 수 있는 입구를 먼저 고른다. 이미 클리어한 입구는 눌러도 열리지 않아서(DungeonGate.CanInteract)
    // 그쪽으로 보내면 헛걸음이 된다. 다만 전부 닫혀 있으면 위치라도 알려 주려고 가장 가까운 곳을 그대로 쓴다.
    Transform FindNearestGate() {
        DungeonGate[] gates = FindObjectsByType<DungeonGate>(FindObjectsSortMode.None);

        Transform bestOpen = null, bestAny = null;
        float openDistance = float.MaxValue, anyDistance = float.MaxValue;
        Vector2 from = transform.position;

        foreach (DungeonGate gate in gates) {
            if (gate == null) continue;

            float distance = ((Vector2)gate.transform.position - from).sqrMagnitude;
            if (distance < anyDistance) {
                anyDistance = distance;
                bestAny = gate.transform;
            }
            if (gate.CanInteract && distance < openDistance) {
                openDistance = distance;
                bestOpen = gate.transform;
            }
        }

        return bestOpen != null ? bestOpen : bestAny;
    }

    #endregion
    #region 표시 · 숨김

    void Show() {
        if (arrow == null) {
            if (arrowPrefab == null) return;

            arrow = Instantiate(arrowPrefab);
            arrow.name = "GateCompassArrow";
            baseAlpha = arrow.color.a;
        }

        Color c = arrow.color;
        c.a = baseAlpha;
        arrow.color = c;

        elapsed = 0f;
        arrow.gameObject.SetActive(true);
        LateUpdate(); // 뜨는 첫 프레임부터 제자리를 잡게 한다. 안 그러면 한 프레임 동안 원점에 나타난다.
    }

    void Hide() {
        if (arrow != null) arrow.gameObject.SetActive(false);
        target = null;
    }

    #endregion
}
