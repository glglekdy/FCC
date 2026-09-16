using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class GroundMoveSystem : MonoBehaviour {
    #region 인스펙터 변수

    [Header("무브먼트 설정")]
    public float speed = 4f; // 몬스터 이동 스피드.
    public float maxSpeed = 3f; // 몬스터가 이동할 수 있는 최대 속도.

    [Header("플레이어 감지")]
    public float detectionRange = 5f; // 플레이어를 감지하는 반경.
    public LayerMask playerLayer; // 플레이어 레이어.
    public float stopDistanceToPlayer = 0.9f; // 추격 중 플레이어와 x축 거리가 이 값 안으로 들어오면 정지. 도착 판정이 없으면 여러 마리가 플레이어의 같은 좌표를 목표로 삼아 서로 겹친다.

    [Header("동족 간격")]
    public LayerMask allyLayer; // 다른 몬스터 레이어. **비워두면 자기 레이어를 자동으로 쓴다.**
    public float separationDistance = 1.1f; // 이 거리 안에 동족이 있으면 그 방향으로는 밀어붙이지 않는다. 몸통 콜라이더 폭보다 약간 크게 잡을 것.
    public float overlapEscapeDistance = 0.6f; // 이만큼 파고든 동족이 있으면 이동을 접고 반대쪽으로 빠져나온다.
    public float separationSpeed = 1.5f; // 겹침에서 빠져나올 때의 속도.

    [Header("Ground Check")]
    public Transform groundCheck; // **몬스터 발밑에 빈 오브젝트를 만드세요.**
    public Vector2 groundCheckSize = new(0.5f, 0.1f);
    public LayerMask groundLayer; // Ground 레이어 설정 필요

    [Header("Wall Check")]
    public Transform wallCheck; // **몬스터 진행 방향 앞쪽에 빈 오브젝트를 만드세요.**
    public float wallCheckDistance = 0.2f;

    [Header("Ledge Check")]
    public Transform ledgeCheck; // **몬스터 발밑 앞쪽에 빈 오브젝트를 만드세요.**
    public float ledgeCheckDistance = 0.5f;

    [Header("애니메이션")]
    // 걷기·정지 모션 전환용. **비워두면 같은 오브젝트에서 찾습니다.**
    // 넘기는 값은 Speed(float) 하나뿐이고, 몇부터 걷기로 볼지는 Animator Controller 의 전이 조건이 정한다.
    public Animator animator;

    #endregion
    #region 외부 제어용 변수

    [HideInInspector]
    public bool isMovementLocked; // Attack 등 외부 시스템이 공격 중 이동을 멈출 때 사용.

    #endregion
    #region 컴포넌트 변수

    Rigidbody2D rigid;
    float knockbackTimer; // 0보다 크면 넉백 중 - 일반 이동 로직을 건너뛰어 물리 힘이 그대로 유지되게 한다.
    MoveState state;
    int facingDirection = 1; // 1: 오른쪽, -1: 왼쪽

    bool isGrounded;
    bool isFacingWall;
    bool isFacingLedge;
    bool isChasing;
    bool isPlayerInStopDistance; // 추격 중 플레이어에 충분히 붙었는지.

    bool isAllyOnRight;
    bool isAllyOnLeft;
    int allyEscapeDirection; // 0이 아니면 그 방향으로 겹침 탈출 중.

    ContactFilter2D allyFilter;
    readonly List<Collider2D> allyBuffer = new(); // 매 프레임 새로 할당하지 않도록 재사용한다.

    Transform playerTransform;

    // Animator 에 Speed 파라미터가 있는 컨트롤러에서만 값을 넘긴다. 없는 컨트롤러에 SetFloat 를 하면
    // 매 프레임 경고가 쌓여 콘솔이 묻히기 때문에 Awake 에서 한 번만 확인해 둔다.
    bool hasSpeedParameter;

    #endregion

    enum MoveState { Patrol, Chase }

    static readonly int SpeedParameter = Animator.StringToHash("Speed"); // Animator Controller 의 파라미터와 이름을 맞춰야 한다.

    #region 유니티 라이프 사이클

    void Awake() {
        rigid = GetComponent<Rigidbody2D>();
        rigid.interpolation = RigidbodyInterpolation2D.Interpolate;

        // 인스펙터에서 비워두면 자기 레이어를 동족으로 본다. 씬 세팅을 깜빡했을 때 간격 유지가 조용히 죽는 걸 막는다.
        if (allyLayer.value == 0) allyLayer = 1 << gameObject.layer;

        allyFilter.useTriggers = false; // Renderer 에 달린 트리거 캡슐까지 잡히면 노이즈라 몸통 콜라이더만 본다.
        allyFilter.SetLayerMask(allyLayer);

        if (animator == null) animator = GetComponent<Animator>();
        hasSpeedParameter = HasParameter(SpeedParameter);
    }

    void Update() {
        CheckGrounded();
        CheckWall();
        CheckLedge();
        CheckAlly();
        DetectPlayer();

        // 앞을 막은 동족도 벽·낭떠러지와 같은 방향 전환 사유로 본다.
        // 단 양옆이 다 막혔으면 매 프레임 뒤집혀 떨리므로 그냥 멈추게 둔다.
        if (state == MoveState.Patrol && (isFacingWall || isFacingLedge || (IsAllyAhead() && !IsAllyBehind()))) {
            Flip();
        }

        UpdateAnimator();
    }

    void FixedUpdate() {
        Move();
    }

    #endregion

    #region 몬스터 움직임 관련 함수

    void Move() {
        if (knockbackTimer > 0f) {
            knockbackTimer -= Time.fixedDeltaTime;
            return;
        }

        if (isMovementLocked) {
            rigid.linearVelocity = new Vector2(0f, rigid.linearVelocityY);
            return;
        }

        if (!isGrounded) {
            return;
        }

        // 이미 파고든 동족이 있으면 이동보다 탈출이 먼저다. (에디터에서 같은 자리에 복제해 둔 경우 등)
        if (allyEscapeDirection != 0) {
            rigid.linearVelocity = new Vector2(allyEscapeDirection * separationSpeed, rigid.linearVelocityY);
            return;
        }

        // 앞을 막은 동족을 계속 밀면 간격 없이 뭉쳐 겹쳐 보인다. 밀지 말고 뒤에서 기다리게 한다.
        if (IsAllyAhead()) {
            rigid.linearVelocity = new Vector2(0f, rigid.linearVelocityY);
            return;
        }

        // 추격 중 낭떠러지 앞에서는 정지 (추락 방지)
        if (state == MoveState.Chase && isFacingLedge) {
            return;
        }

        // 플레이어에 붙었으면 더 파고들지 않는다. 이게 없으면 감지 범위 안의 몬스터 전부가 플레이어의 같은 좌표로 몰린다.
        if (state == MoveState.Chase && isPlayerInStopDistance) {
            rigid.linearVelocity = new Vector2(0f, rigid.linearVelocityY);
            return;
        }

        rigid.AddForce(new Vector2(facingDirection * speed, 0f), ForceMode2D.Impulse);

        // 최고 속도 관리. velocity 를 통째로 대입하면 낙하·점프 중인 y축 속도까지 같이 덮어쓰게 되므로 x축만 손댄다.
        if (Mathf.Abs(rigid.linearVelocityX) > maxSpeed) {
            rigid.linearVelocityX = Mathf.Sign(rigid.linearVelocityX) * maxSpeed;
        }
    }

    // 실제로 깔린 거리만큼만 걷기 모션을 돌린다. 벽·동족·플레이어 앞에서 멈췄을 때 제자리걸음이 나오지 않게
    // 의도한 방향(facingDirection)이 아니라 리지드바디의 실제 속도를 본다.
    void UpdateAnimator() {
        if (!hasSpeedParameter) return;

        animator.SetFloat(SpeedParameter, Mathf.Abs(rigid.linearVelocityX));
    }

    bool HasParameter(int nameHash) {
        if (animator == null || animator.runtimeAnimatorController == null) return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++) {
            if (parameters[i].nameHash == nameHash) return true;
        }
        return false;
    }

    public void ApplyKnockback(Vector2 force, float duration = 0.15f) {
        knockbackTimer = duration;
        rigid.linearVelocity = Vector2.zero;
        rigid.AddForce(force, ForceMode2D.Impulse);
    }

    void CheckGrounded() {
        if (groundCheck == null) return;
        isGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);
    }

    void CheckWall() {
        if (wallCheck == null) return;
        isFacingWall = Physics2D.Raycast(wallCheck.position, Vector2.right * facingDirection, wallCheckDistance, groundLayer);
    }

    void CheckLedge() {
        if (ledgeCheck == null) {
            isFacingLedge = false;
            return;
        }
        isFacingLedge = !Physics2D.Raycast(ledgeCheck.position, Vector2.down, ledgeCheckDistance, groundLayer);
    }

    // 주변 동족의 좌우 위치를 파악한다.
    // 콜라이더끼리는 물리 엔진이 막아주지만, 서로를 인식하지 못하면 목표 지점(플레이어 좌표·진행 방향)이 같은 몬스터들이
    // 어깨를 맞댄 덩어리로 뭉쳐 버린다. 간격이 0이라 보기에는 겹친 것과 다름없으니 애초에 밀어붙이지 않게 막는다.
    void CheckAlly() {
        isAllyOnRight = false;
        isAllyOnLeft = false;
        allyEscapeDirection = 0;

        if (separationDistance <= 0f) return;

        int count = Physics2D.OverlapCircle(rigid.position, separationDistance, allyFilter, allyBuffer);
        float nearestOverlap = float.MaxValue;

        for (int i = 0; i < count; i++) {
            Rigidbody2D otherBody = allyBuffer[i].attachedRigidbody;
            if (otherBody == null || otherBody == rigid) continue; // 자기 자신(자식 콜라이더 포함)은 건너뛴다.

            float deltaX = otherBody.position.x - rigid.position.x;
            if (deltaX >= 0f) isAllyOnRight = true;
            else isAllyOnLeft = true;

            // 가장 깊게 파고든 상대를 기준으로 탈출 방향을 정한다.
            float distance = Mathf.Abs(deltaX);
            if (distance >= overlapEscapeDistance || distance >= nearestOverlap) continue;

            nearestOverlap = distance;
            if (distance > 0.01f) {
                allyEscapeDirection = deltaX > 0f ? -1 : 1;
            }
            else {
                // 에디터에서 Ctrl+D 로 복제하면 좌표가 완전히 같아 방향을 정할 수 없다. 오브젝트 고유 id 로 편을 갈라 서로 반대로 흩어지게 한다.
                allyEscapeDirection = GetEntityId() < otherBody.GetEntityId() ? -1 : 1;
            }
        }
    }

    // 좌우 감지 결과를 진행 방향에 맞춰 그때그때 읽는다. Flip() 이나 추격으로 facingDirection 이 바뀐 뒤에도 값이 맞도록.
    bool IsAllyAhead() {
        return facingDirection > 0 ? isAllyOnRight : isAllyOnLeft;
    }

    bool IsAllyBehind() {
        return facingDirection > 0 ? isAllyOnLeft : isAllyOnRight;
    }

    void DetectPlayer() {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRange, playerLayer);
        isChasing = hit != null;

        if (isChasing) {
            playerTransform = hit.transform;
            state = MoveState.Chase;
            int directionToPlayer = playerTransform.position.x >= transform.position.x ? 1 : -1;
            SetFacingDirection(directionToPlayer);
            isPlayerInStopDistance = Mathf.Abs(playerTransform.position.x - transform.position.x) <= stopDistanceToPlayer;
        }
        else {
            state = MoveState.Patrol;
            isPlayerInStopDistance = false;
        }
    }

    void Flip() {
        SetFacingDirection(facingDirection * -1);
    }

    // 스케일 x값의 부호를 뒤집어 좌우를 바라보게 한다. 추격 중 플레이어를 바라볼 때도 사용.
    void SetFacingDirection(int direction) {
        if (facingDirection == direction) return;

        facingDirection = direction;
        Vector3 newScale = transform.localScale;
        newScale.x = Mathf.Abs(newScale.x) * facingDirection;
        transform.localScale = newScale;
    }

    // 감지 범위 표시
    private void OnDrawGizmos() {
        if (groundCheck != null) {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }

        if (wallCheck != null) {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(wallCheck.position, wallCheck.position + facingDirection * wallCheckDistance * Vector3.right);
        }

        if (ledgeCheck != null) {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(ledgeCheck.position, ledgeCheck.position + Vector3.down * ledgeCheckDistance);
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        if (separationDistance > 0f) {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, separationDistance);
        }
    }

    #endregion
}
