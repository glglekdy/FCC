using UnityEngine;

// 위아래로 순찰하다가 플레이어를 발견하면 고속으로 돌진해 박치기하는 비행 몬스터.
// 놓치면 급정지하지 않고 관성으로 미끄러지며 선회했다가 다시 달려든다.
//
// 지상형(GroundMoveSystem + Attack)과 달리 이동과 공격을 한 파일에 둔다.
// 돌진 자체가 공격이라 분리하면 상태·조준 스냅샷·스윕 시작 위치를 매 프레임 두 컴포넌트가 주고받아야 한다.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class FlyMoveSystem : MonoBehaviour, IRestrainable {
    #region 인스펙터 변수

    [Header("연결")]
    public Transform visual; // 회전·반전시킬 스프라이트 자식. **몬스터 밑의 Renderer 오브젝트를 연결하세요.**
    public Animator animator; // 상태별 프레임 애니메이션. **visual과 같은 Renderer 오브젝트의 Animator를 연결하세요.** 스테이트 이름은 Idle · Aim · Charge · Drift · Hurt 여야 한다.
    public SpriteStateAnimator spriteAnimator; // (구) 상태별 정지 이미지 한 장. animator를 연결하면 쓰이지 않는다.

    [Header("레이어")]
    public LayerMask playerLayer; // 플레이어 레이어. **Player(3) 를 지정하세요.**
    public LayerMask groundLayer; // 지형 레이어. **ground(31, 소문자) 를 지정하세요.**

    [Header("순찰")]
    public float patrolRange = 2f; // 시작 위치를 중심으로 위아래 몇 유닛까지 오갈지.
    public float patrolSpeed = 1.8f; // 순찰 속도. 돌진 속도와 차이가 커야 순찰이 느긋하게 읽힌다.
    public float patrolAccel = 4f; // 순찰 목표 속도에 붙는 빠르기.
    public float patrolFlipDistance = 0.15f; // 레인 끝점에 이만큼 붙으면 반대로 돌아선다.
    public float clearance = 0.8f; // 천장·바닥을 미리 감지하는 거리. 몸 반지름보다 넉넉하게.

    [Header("플레이어 감지")]
    public float detectionRange = 7f; // 플레이어를 처음 발견하는 반경.
    public float loseRange = 10f; // 추격을 포기하는 반경. detectionRange 보다 커야 경계에서 추격이 깜빡이지 않는다.
    public bool requireLineOfSight = true; // 벽 너머의 플레이어는 못 본 것으로 친다.
    public float loseSightTime = 1.2f; // 시야가 이만큼 계속 끊겨야 타깃을 놓는다. 점프로 잠깐 가려지는 것에 흔들리지 않게.
    public float losStartOffset = 0.55f; // 시야 검사를 몸 밖에서 시작하는 거리. **몸 콜라이더 반지름보다 크게 잡으세요.**

    [Header("돌진 예고")]
    public float aimDuration = 0.35f; // 돌진 전 멈춰서 예고하는 시간. 가속·활주까지 더하면 체감 경고는 약 1초다.
    public float aimStagger = 0.15f; // 여기에 0~이 값만큼 랜덤을 더한다. 여러 마리가 한 몸처럼 동시에 튀어나오는 걸 깬다.
    public float aimLiftHeight = 0.8f; // 예고 중 위로 떠오르는 높이. 이만큼 뜬 자리가 돌진의 새 출발점이 되어 아래로 내리찍는 궤적이 나온다.
    public float aimLiftBurstSpeed = 9f; // 예고가 시작되는 순간 위로 쏘아 올리는 초기 속도. 여기서 "확 뜨는" 느낌이 나온다.
    public float aimLiftSpeed = 3f; // 초기 임펄스 이후 목표 높이에 다가가는 속도.
    public float aimLiftAccel = 6f; // 떠오르는 속도에 붙는 가속률.
    public Color aimIndicatorColor = new(1f, 0.35f, 0.1f, 0.9f); // 예고 링 색상.
    public float aimIndicatorStartScale = 2.5f; // 예고 링이 이 배율에서 1배로 좁혀 들어온다. 남은 시간이 크기로 읽힌다.
    public float aimIndicatorVisualScale = 0.6f; // 링의 겉보기 크기 배율. 1보다 작게 하면 실제 판정 범위(ramRadius)는 그대로 두고 시각적으로만 작게 보인다.

    [Header("돌진")]
    public float chargeSpeed = 14f; // 돌진 최고 속도. 플레이어가 달려서는 못 피하고 점프·타이밍으로 피해야 하는 값.
    public float chargeAccel = 8f; // 돌진 가속률.
    public float chargeMinDistance = 2f; // 조준점이 코앞이어도 최소 이만큼은 달린다. 없으면 출발하자마자 오버슛 판정이 뜬다.
    public float overshootMargin = 1.2f; // 조준점을 이만큼 지나쳐야 놓친 것으로 본다. 얼굴 앞 급정지 방지.
    public float chargeMaxTime = 1.1f; // 돌진 제한 시간. 아무것도 못 맞히면 강제로 드리프트로 넘어간다.

    [Header("드리프트")]
    public float forwardDrag = 1.6f; // 진행 방향 성분의 감쇠율. 낮을수록 길게 미끄러진다.
    public float lateralDrag = 6f; // 옆으로 흐르는 성분의 감쇠율(그립). **forwardDrag 의 3~4배가 드리프트로 보이는 구간이다.**
    public float turnAccel = 12f; // 새 목표 방향으로 트는 힘.
    public float maxSpeedCap = 14f; // 넉백·벽반발이 주입한 속도까지 포함한 상한.
    public float driftMinTime = 0.45f; // 최소 드리프트 시간. 이게 짧으면 플레이어 무적 시간보다 빨리 다시 박아 집요해진다.
    public float driftMaxTime = 1.6f; // 드리프트가 끝나지 않을 때의 하드 타임아웃.
    public float reChargeSpeed = 9f; // 재돌진을 허용하는 속도 상한. 아직 빠르게 흘러가는 중이면 예고가 무의미해지므로 막는다.
                                     // **turnAccel / forwardDrag(종단속도)보다 커야 한다. 작으면 영원히 재돌진하지 않는다.**
    public float reChargeAlignDot = 0.85f; // 진행 방향이 목표와 이만큼 정렬돼야 재돌진. 0.85 ≈ 32도. 실질적으로 이쪽이 드리프트 길이를 정한다.

    [Header("박치기")]
    public float ramRadius = 0.6f; // 몸통 판정 반경.
    public int ramDamage = 15; // 박치기 데미지.
    public float ramRecoil = 6f; // 명중 후 뒤로 튕겨나가는 속도.

    [Header("벽 충돌")]
    public float wallBounce = 0.35f; // 벽 반사 계수. 자해 데미지가 0일 때만 쓴다.
    public float wallStunTime = 0.25f; // 벽에 박은 뒤 선회 추력을 끊는 시간. 이 틈이 플레이어의 반격 창이다.

    [Header("벽 충돌 자해")]
    public bool wallDamageEnabled = true; // 벽에 박았을 때 스스로 데미지를 받을지.
    public float wallDamageBase = 1f; // 기본 데미지. 충돌 속도가 최소치를 겨우 넘겼을 때의 값.
    public float wallDamageReferenceSpeed = 0f; // 100% 로 칠 기준 속도. **0이면 chargeSpeed 를 자동으로 쓴다.**
    public float wallDamageSpeedStep = 0.05f; // 기준 속도의 몇 %마다 한 단계씩 올릴지. 0.05 = 5%.
    public float wallDamagePerStep = 1f; // 한 단계당 늘어나는 데미지.
    public float wallDamageMinSpeed = 3f; // 이 속도 미만의 충돌은 무시. **patrolSpeed 보다 커야 순찰 중 천장을 스칠 때 안 깎인다.**
    public int wallDamageMax = 0; // 자해 데미지 상한. 0이면 무제한.
    public float wallHitCooldown = 0.4f; // 한 번의 충돌이 여러 번 잡히는 걸 막는 간격. 벽면을 따라 미끄러지면 접촉이 재발생한다.

    #endregion
    #region 컴포넌트 변수

    Rigidbody2D rigid;
    Health health;
    SpriteRenderer aimIndicator;

    FlyState state = FlyState.Patrol;
    float stateTimer; // 현재 상태에 머문 시간.
    float knockbackTimer; // 0보다 크면 넉백 중 - 추력을 넣지 않아 물리 힘이 그대로 유지된다.
    float restrainTimer; // 0보다 크면 구속 중(Close Call) - 허공에 멈춰 선다.
    float wallStunTimer;
    float wallHitTimer;
    float lostSightTimer;

    Vector2 patrolAnchor; // 순찰 레인의 중심. 씬에 배치한 위치가 그대로 중심이 된다.
    int patrolDirection = 1; // 1: 위, -1: 아래

    Transform target;
    Health targetHealth;

    float aimTime; // aimDuration + 랜덤 스태거.
    Vector2 aimStartPosition; // 예고를 시작한 위치. 떠오르는 목표 높이의 기준점이다.
    Vector2 chargeDirection = Vector2.right;
    Vector2 chargeOrigin;
    Vector2 chargeTargetPoint;
    float chargeDistance;
    bool hasHitThisCharge;

    Vector2 lastCheckPosition; // 박치기 스윕의 시작점. 직전 물리 스텝의 위치다.
    Vector2 facingDirection = Vector2.right;

    Vector2 gizmoAlong; // 기즈모 전용. 드리프트 성분 분해를 씬 뷰에서 눈으로 보려고 남긴다.
    Vector2 gizmoLateral;
    bool gizmoHasSight;

    #endregion

    enum FlyState { Patrol, Aim, Charge, Drift }

    #region 유니티 라이프 사이클

    void Awake() {
        rigid = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();

        // 씬에서 손으로 만든 인스턴스도 있을 수 있어 물리 설정을 코드로 못 박는다. 하나라도 틀리면 조용히 이상하게 움직인다.
        rigid.gravityScale = 0f; // 비행체.
        rigid.interpolation = RigidbodyInterpolation2D.Interpolate;
        rigid.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // 돌진이 빨라 Discrete 면 얇은 벽을 통과한다.
        rigid.sleepMode = RigidbodySleepMode2D.NeverSleep; // 예고 중 거의 멈추는데 기본 설정이면 0.5초 뒤 잠들어 다시 안 움직인다.
        rigid.freezeRotation = true; // 물리 충돌로 몸이 제멋대로 도는 것만 막는다. 좌우 표현은 visual 스케일 반전으로 따로 처리한다.
        rigid.excludeLayers = 1 << gameObject.layer; // 같은 몬스터끼리 밀치면 돌진 궤적이 흔들린다. 매트릭스를 건드리지 않고 이 몸만 뺀다.
    }

    void Start() {
        // 스포너가 Awake 이후에 위치를 옮길 수 있어 앵커는 여기서 잡는다.
        patrolAnchor = rigid.position;
        lastCheckPosition = rigid.position;
        BuildAimIndicator();
    }

    void Update() {
        if (health.IsDead) return;

        UpdateVisual();
        UpdateAimIndicator();
    }

    // 감지와 상태 전이까지 전부 FixedUpdate 에 둔다.
    // 돌진 거리 판정이 물리 스텝과 어긋나면 프레임레이트에 따라 오버슛 지점이 흔들리기 때문.
    void FixedUpdate() {
        if (health.IsDead) return; // Destroy 는 프레임 끝에 처리돼서 죽은 뒤에도 한 스텝 더 돈다.

        float dt = Time.fixedDeltaTime;
        TickTimers(dt);

        // 비행체라 중력이 없으므로 속도를 0으로 못 박으면 그 자리에 떠서 멈춘다.
        if (restrainTimer > 0f) {
            rigid.linearVelocity = Vector2.zero;
            lastCheckPosition = rigid.position;
            return;
        }

        if (knockbackTimer > 0f) {
            // 속도를 직접 대입하는 방식이라 한 스텝만 개입해도 넉백이 통째로 사라진다.
            lastCheckPosition = rigid.position;
            return;
        }

        CheckRam();
        lastCheckPosition = rigid.position;

        DetectTarget();

        switch (state) {
            case FlyState.Patrol:
                TickPatrol(dt);
                break;
            case FlyState.Aim:
                TickAim(dt);
                break;
            case FlyState.Charge:
                TickCharge(dt);
                break;
            case FlyState.Drift:
                TickDrift(dt);
                break;
        }
    }

    void OnDisable() {
        HideAimIndicator();
    }

    // 예고 링은 씬 루트에 따로 떠 있는 오브젝트라 이 몬스터가 죽어도 같이 안 지워진다. HealthBar 와 같은 정리 방식.
    void OnDestroy() {
        if (aimIndicator != null) Destroy(aimIndicator.gameObject);
    }

    #endregion
    #region 상태 전이

    void TickTimers(float dt) {
        if (knockbackTimer > 0f) knockbackTimer -= dt;
        if (restrainTimer > 0f) restrainTimer -= dt;
        if (wallStunTimer > 0f) wallStunTimer -= dt;
        if (wallHitTimer > 0f) wallHitTimer -= dt;
        stateTimer += dt;
    }

    void EnterPatrol() {
        state = FlyState.Patrol;
        stateTimer = 0f;
        HideAimIndicator();
        SetStateSprite("Idle");
    }

    void EnterAim() {
        state = FlyState.Aim;
        stateTimer = 0f;
        aimTime = aimDuration + Random.Range(0f, aimStagger);
        aimStartPosition = rigid.position;
        SetStateSprite("Aim");

        // 순간 임펄스로 속도를 위로 꽂아 넣는다. TickAim 의 Seek 는 목표 높이에 다가가는 뒷정리만 맡아서
        // 여기 없이 Seek 만 쓰면 가속이 붙는 데 시간이 걸려 흐르듯 뜨는 그림이 나온다.
        rigid.linearVelocity = Vector2.up * aimLiftBurstSpeed;

        // 찍는 건 도착 지점뿐이다. 예고 도중 계속 갱신하면 사실상 플레이어를 따라가며 박는
        // 호밍 공격이 되어 "위치를 찍고 박는다"는 의도가 사라진다. 돌진 방향은 떠오른 뒤
        // EnterCharge 에서 새로 계산해 위→아래로 내리찍는 궤적을 만든다.
        if (HasTarget()) chargeTargetPoint = target.position;

        ShowAimIndicator();
    }

    void EnterCharge() {
        state = FlyState.Charge;
        stateTimer = 0f;
        hasHitThisCharge = false;
        chargeOrigin = rigid.position;
        SetStateSprite("Charge");

        // 떠오른 위치에서 도착 지점을 다시 겨눈다. 이 한 번의 계산이 내리찍는 각도를 만든다.
        Vector2 toTarget = chargeTargetPoint - chargeOrigin;
        chargeDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : facingDirection;
        SetFacing(chargeDirection);

        // 조준점이 코앞이면 출발하자마자 오버슛 판정이 떠서 돌진이 성립하지 않는다.
        chargeDistance = Mathf.Max(Vector2.Dot(chargeTargetPoint - chargeOrigin, chargeDirection), chargeMinDistance);

        // 스윕 시작점을 여기서 다시 잡지 않으면 첫 판정이 순찰 위치에서 조준점까지 맵을 가로지른다.
        lastCheckPosition = rigid.position;
        HideAimIndicator();
    }

    void EnterDrift() {
        state = FlyState.Drift;
        stateTimer = 0f;
        HideAimIndicator();
        SetStateSprite("Drift"); // 급제동 → 선회 모션을 한 번 보여 주고, 컨트롤러가 끝나면 Idle 로 되돌린다.
    }

    #endregion
    #region 상태별 동작

    void TickPatrol(float dt) {
        if (HasTarget()) {
            EnterAim();
            return;
        }

        // 레인 끝점을 그냥 추격한다. 위아래 왕복·x축 복귀·드리프트로 벗어난 뒤 복귀가 전부 이 하나로 풀린다.
        // 밴드 안팎을 따로 처리하면 밖으로 밀려났을 때 매 프레임 방향이 뒤집혀 떤다.
        Vector2 patrolTarget = GetPatrolTarget();

        // 천장·바닥이 레인 끝보다 가까우면 미리 돌아선다. 지형에 계속 밀어붙이면 벽 자해가 쌓인다.
        bool reachedEnd = Vector2.Distance(rigid.position, patrolTarget) <= patrolFlipDistance;
        if (reachedEnd || IsBlocked(Vector2.up * patrolDirection)) {
            patrolDirection = -patrolDirection;
            patrolTarget = GetPatrolTarget();
        }

        Vector2 toTarget = patrolTarget - rigid.position;
        if (toTarget.sqrMagnitude > 0.0001f) {
            Vector2 direction = toTarget.normalized;
            SetFacing(direction);
            Seek(direction * patrolSpeed, patrolAccel, dt);
        }
    }

    void TickAim(float dt) {
        // 예고를 시작한 자리 바로 위로 떠오른다. 이 뜬 위치가 EnterCharge 에서 돌진의 새 출발점이 되어
        // 아래로 내리찍는 궤적을 만든다. 목표를 aimStartPosition 기준 고정값으로 잡아 몸이 떠 있는 동안 흔들리지 않는다.
        Vector2 liftTarget = aimStartPosition + Vector2.up * aimLiftHeight;
        Vector2 toLift = liftTarget - rigid.position;
        Vector2 liftVelocity = toLift.sqrMagnitude > 0.01f ? toLift.normalized * aimLiftSpeed : Vector2.zero;
        Seek(liftVelocity, aimLiftAccel, dt);

        // 도착 지점은 고정이지만, 몸이 떠오르는 동안 그 지점을 계속 내려다보게 시선만 갱신한다.
        // 조준점 자체를 다시 찍는 게 아니라서 호밍이 아니다.
        Vector2 toTargetPoint = chargeTargetPoint - rigid.position;
        if (toTargetPoint.sqrMagnitude > 0.0001f) SetFacing(toTargetPoint.normalized);

        if (stateTimer < aimTime) return;

        // 여기서 타깃이 사라졌어도 돌진은 그대로 나간다. 예고를 띄워놓고 취소하면 버그처럼 보인다.
        EnterCharge();
    }

    void TickCharge(float dt) {
        // 조준 시점의 방향·목표점 스냅샷만 쓰고 플레이어 트랜스폼은 읽지 않는다.
        // 그래서 예고한 그 자리로 정직하게 돌진하고, 플레이어가 비키면 옛 자리를 스쳐 지나간다. 이게 "놓친다"의 정체다.
        Seek(chargeDirection * chargeSpeed, chargeAccel, dt);
        SetFacing(chargeDirection);

        float traveled = Vector2.Dot(rigid.position - chargeOrigin, chargeDirection);
        if (traveled >= chargeDistance + overshootMargin || stateTimer >= chargeMaxTime) {
            EnterDrift();
        }
    }

    void TickDrift(float dt) {
        Vector2 aim = GetDriftAim();
        Vector2 v = rigid.linearVelocity;

        Vector2 along = Vector2.Dot(v, aim) * aim; // 다시 노리는 방향 성분.
        Vector2 lateral = v - along; // 옆으로 흐르는 성분.
        gizmoAlong = along;
        gizmoLateral = lateral;

        // 앞뒤 성분은 천천히, 옆으로 흐르는 성분은 빠르게 죽인다. 이 두 감쇠율의 비율이 곧 미끄러지는 정도다.
        // AddForce(-v * k) 로 하면 명시적 오일러라 k 를 키우다 어느 순간 발산하는데, 지수 감쇠는 어떤 값에서도 안정적이다.
        v = along * Mathf.Exp(-forwardDrag * dt) + lateral * Mathf.Exp(-lateralDrag * dt);

        // 벽에 박은 직후에는 선회 추력을 끊는다. 튕겨나가는 이 틈이 플레이어의 반격 창이다.
        if (wallStunTimer <= 0f) v += aim * turnAccel * dt;

        rigid.linearVelocity = Vector2.ClampMagnitude(v, maxSpeedCap);
        SetFacing(aim); // 몸은 관성으로 흘러가는데 시선만 목표를 향한다. 이 어긋남이 드리프트로 읽힌다.

        if (stateTimer < driftMinTime) return;

        if (!HasTarget()) {
            // 타깃을 잃었어도 드리프트는 마저 끝낸다. 순간적으로 방향이 꺾이면 관성이 사라져 보인다.
            if (stateTimer >= driftMaxTime || v.magnitude <= reChargeSpeed) EnterPatrol();
            return;
        }

        if (stateTimer >= driftMaxTime) {
            EnterAim();
            return;
        }

        // 속도가 거의 0이면 normalized 가 zero 라 내적이 0으로 나와 정렬 판정이 영원히 실패한다.
        bool aligned = v.sqrMagnitude < 0.01f || Vector2.Dot(v.normalized, aim) >= reChargeAlignDot;

        // 두 조건을 OR 로 묶으면 안 된다. turnAccel 이 속도를 급격히 꺾는 탓에 선회 시작 0.1초쯤에
        // 속도가 잠깐 바닥을 치는데, 그때 바로 튀어나가면 미끄러지는 구간이 통째로 사라진다.
        // 반대로 정렬만 보면 아직 전속력으로 흘러가는 중에 재돌진해 예고가 무의미해진다. 둘 다 만족해야 한다.
        if (aligned && v.magnitude <= reChargeSpeed) EnterAim();
    }

    // 목표 속도로 지수적으로 접근한다. dt 를 지수에 넣어 프레임레이트가 흔들려도 같은 곡선을 그린다.
    void Seek(Vector2 targetVelocity, float accel, float dt) {
        Vector2 v = Vector2.Lerp(rigid.linearVelocity, targetVelocity, 1f - Mathf.Exp(-accel * dt));
        rigid.linearVelocity = Vector2.ClampMagnitude(v, maxSpeedCap);
    }

    Vector2 GetPatrolTarget() {
        return new(patrolAnchor.x, patrolAnchor.y + patrolDirection * patrolRange);
    }

    // 타깃을 잃어도 순찰 앵커를 새 목표로 삼는다. 같은 코드로 복귀 궤적까지 드리프트로 그려진다.
    Vector2 GetDriftAim() {
        Vector2 point = HasTarget() ? (Vector2)target.position : patrolAnchor;
        Vector2 direction = point - rigid.position;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : facingDirection;
    }

    bool IsBlocked(Vector2 direction) {
        return Physics2D.Raycast(rigid.position, direction, clearance, groundLayer);
    }

    #endregion
    #region 플레이어 감지

    void DetectTarget() {
        // 추격 중일 때만 넓은 반경을 쓴다. 경계선에서 추격이 붙었다 떨어졌다 깜빡이는 걸 막는 히스테리시스.
        float range = HasTarget() ? loseRange : detectionRange;
        Collider2D hit = Physics2D.OverlapCircle(rigid.position, range, playerLayer);

        if (hit == null) {
            ClearTarget();
            return;
        }

        Transform candidate = hit.transform;
        gizmoHasSight = !requireLineOfSight || HasLineOfSight(candidate.position);

        if (!gizmoHasSight) {
            // 잠깐 가려진 것과 완전히 놓친 것을 구분한다. 점프 한 번에 추격이 끊기면 안 된다.
            lostSightTimer += Time.fixedDeltaTime;
            if (lostSightTimer >= loseSightTime) ClearTarget();
            return;
        }

        lostSightTimer = 0f;
        target = candidate;
        targetHealth = hit.GetComponentInParent<Health>(); // 콜라이더가 자식일 수 있어 부모까지 훑는다.
    }

    void ClearTarget() {
        target = null;
        targetHealth = null;
        lostSightTimer = 0f;
        gizmoHasSight = false;
    }

    // Destroy 가 프레임 끝까지 지연돼서, 논리적으로 죽었는데 트랜스폼은 살아있는 창이 존재한다.
    bool HasTarget() {
        return target != null && targetHealth != null && !targetHealth.IsDead;
    }

    bool HasLineOfSight(Vector2 point) {
        Vector2 origin = rigid.position;
        Vector2 toTarget = point - origin;
        float distance = toTarget.magnitude;
        if (distance <= losStartOffset) return true;

        Vector2 direction = toTarget / distance;

        // QueriesStartInColliders 가 켜져 있어 자기 콜라이더 안에서 쏘면 거리 0으로 즉시 히트한다. 몸 밖에서 시작한다.
        Vector2 start = origin + direction * losStartOffset;
        RaycastHit2D hit = Physics2D.Raycast(start, direction, distance - losStartOffset, groundLayer);

        // 벽에 살짝 파고든 상태에서도 거리 0 히트가 나온다. 그대로 두면 시야가 영구히 막혀 플레이어를 영원히 못 본다.
        return hit.collider == null || hit.distance <= 0.02f;
    }

    #endregion
    #region 박치기 판정

    void CheckRam() {
        if (state != FlyState.Charge || hasHitThisCharge) return;

        Collider2D hit = SweepForPlayer();
        if (hit == null) return;

        // 데미지 성공이 아니라 접촉 시점에 플래그를 세운다. 플레이어 무적 시간 중이면 TakeDamage 가 조용히 무시되는데,
        // 성공에만 걸면 무적인 플레이어를 계속 들이받으며 돌진과 드리프트를 무한 반복한다.
        hasHitThisCharge = true;

        // SweepForPlayer 가 Hurtbox 콜라이더만 돌려주므로 여기서는 바로 꺼내 쓴다.
        Health hitHealth = hit.TryGetComponent(out Hurtbox hurtbox) ? hurtbox.OwnerHealth : null;

        // 박치기는 몸통이 무기다. 공격 원점을 몬스터 위치로 넘겨야 플레이어가 몬스터 반대편으로 날아간다.
        if (hitHealth != null) hitHealth.TakeDamage(ramDamage, rigid.position);

        rigid.linearVelocity -= chargeDirection * ramRecoil; // 박고 튕겨나가는 반동.
        EnterDrift();
    }

    Collider2D SweepForPlayer() {
        Vector2 delta = rigid.position - lastCheckPosition;
        float distance = delta.magnitude;

        // 한 스텝의 이동 구간 전체를 훑는다. 지금 chargeSpeed 로는 터널링이 안 나지만,
        // 이게 있어야 나중에 속도를 크게 올려도 판정이 새지 않는다.
        // CircleCast(단일) 는 맨 처음 걸린 콜라이더를 돌려주는데, 그게 플레이어의 이동용 몸통 콜라이더일 수 있다.
        // All 로 훑어서 Hurtbox 가 붙은 콜라이더를 직접 골라낸다.
        if (distance > 0.0001f) {
            RaycastHit2D[] swept = Physics2D.CircleCastAll(lastCheckPosition, ramRadius, delta / distance, distance, playerLayer);
            foreach (RaycastHit2D hit in swept) {
                if (hit.collider.GetComponent<Hurtbox>() != null) return hit.collider;
            }
        }

        // 예고 중에 플레이어가 걸어와 이미 겹쳐 있으면 스윕 길이가 0이라 위에서 안 잡힌다.
        Collider2D[] overlaps = Physics2D.OverlapCircleAll(rigid.position, ramRadius, playerLayer);
        foreach (Collider2D overlap in overlaps) {
            if (overlap.GetComponent<Hurtbox>() != null) return overlap;
        }

        return null;
    }

    #endregion
    #region 벽 충돌

    void OnCollisionEnter2D(Collision2D collision) {
        if (health.IsDead) return;

        // 플레이어는 레이어 매트릭스에서 꺼져 있어 애초에 안 들어오지만, 명시해 두는 편이 안전하다.
        if (((1 << collision.gameObject.layer) & groundLayer) == 0) return;

        // 벽면을 따라 미끄러지면 접촉이 계속 재발생해서 한 번의 충돌이 여러 번으로 잡힌다.
        if (wallHitTimer > 0f) return;
        wallHitTimer = wallHitCooldown;

        // ReuseCollisionCallbacks 가 켜져 있어 Collision2D 객체는 재사용된다. 필요한 값을 지금 다 뽑아둔다.
        ContactPoint2D contact = collision.GetContact(0);
        Vector2 normal = contact.normal;
        Vector2 point = contact.point;
        float impactSpeed = Mathf.Abs(Vector2.Dot(collision.relativeVelocity, normal));

        wallStunTimer = wallStunTime;

        int damage = wallDamageEnabled ? CalculateWallDamage(impactSpeed) : 0;

        if (damage > 0) {
            // 공격 원점을 접촉점으로 넘기면 HitReactor 가 스파크·데미지 숫자·벽 반대 방향 넉백까지 전부 처리해 준다.
            // 그래서 여기서 Reflect 로 튕김을 또 계산하면 이중으로 밀린다.
            health.TakeDamage(damage, point);
            if (health.IsDead) return; // 자해로 죽었으면 Destroy 가 예약된 상태다. 더 만지지 않는다.
        }
        else {
            rigid.linearVelocity = Vector2.Reflect(rigid.linearVelocity, normal) * wallBounce;
        }

        // 넉백이 이미 Drift 로 바꿔놨으면 타이머를 또 리셋하지 않는다.
        if (state != FlyState.Drift) EnterDrift();
    }

    // 벽에 처박은 속도가 셀수록 크게 다친다. 플레이어가 돌진을 벽으로 유도해 반격할 수 있게 하는 장치다.
    // 속도 크기가 아니라 벽 법선 성분을 쓴다. 스치듯 미끄러지는 것과 정면으로 처박는 건 완전히 다른 사건인데
    // magnitude 만 보면 둘이 구분되지 않는다.
    int CalculateWallDamage(float impactSpeed) {
        if (impactSpeed < wallDamageMinSpeed) return 0; // 순찰 속도로 천장을 스치는 정도로는 안 깎인다.
        if (wallDamageSpeedStep <= 0f) return 0;

        // 기준 속도를 비워두면 돌진 속도를 그대로 쓴다. chargeSpeed 를 튜닝하면 자해 스케일이 알아서 따라온다.
        float reference = wallDamageReferenceSpeed > 0f ? wallDamageReferenceSpeed : chargeSpeed;
        if (reference <= 0f) return 0;

        int steps = Mathf.FloorToInt(impactSpeed / reference / wallDamageSpeedStep);
        int damage = Mathf.RoundToInt(wallDamageBase + steps * wallDamagePerStep);

        if (wallDamageMax > 0) damage = Mathf.Min(damage, wallDamageMax);
        return Mathf.Max(0, damage);
    }

    #endregion
    #region 외부 진입점

    // HitReactor 가 호출하는 넉백 진입점. GroundMoveSystem / Player_move 와 시그니처가 같아야 한다.
    public void ApplyKnockback(Vector2 force, float duration = 0.15f) {
        knockbackTimer = duration;
        rigid.linearVelocity = Vector2.zero;
        rigid.AddForce(force, ForceMode2D.Impulse);

        // 돌진 중에 맞으면 돌진이 취소된다. 플레이어의 반격이 보상받고,
        // 넉백 잔여 속도가 드리프트 감쇠에 그대로 흡수돼 "튕겨나갔다 미끄러지며 되돌아옴"으로 읽힌다.
        hasHitThisCharge = false;
        if (state != FlyState.Drift) EnterDrift();

        // 드리프트 모션보다 뒤에 건다. 맞았다는 반응이 먼저 읽혀야 하고, 컨트롤러가 끝나면 Idle 로 되돌린다.
        SetStateSprite("Hurt");
    }

    // 곡예사의 줄(Close Call)이 호출하는 구속 진입점. 넉백과 같은 이유로 돌진·조준을 끊고 드리프트로 넘긴다 —
    // 묶였다 풀린 뒤 곧장 돌진을 이어가면 구속이 공격을 늦춰주지 못한다.
    public void Restrain(float duration) {
        if (duration <= 0f) return;

        restrainTimer = Mathf.Max(restrainTimer, duration);
        knockbackTimer = 0f;
        rigid.linearVelocity = Vector2.zero;
        hasHitThisCharge = false;
        if (state != FlyState.Drift) EnterDrift();
    }

    #endregion
    #region 시각 처리

    void SetFacing(Vector2 direction) {
        if (direction.sqrMagnitude < 0.0001f) return;
        facingDirection = direction.normalized;
    }

    // GroundMoveSystem/Player_move 와 같은 방식이다: 플레이어를 향해 회전하지 않고, 좌우 스케일 부호만 뒤집는다.
    // 위아래로만 움직여 facingDirection.x 가 거의 0인 순간(순찰 반환점 등)에는 이전 좌우를 그대로 유지한다.
    void UpdateVisual() {
        if (visual == null || Mathf.Abs(facingDirection.x) < 0.0001f) return;

        bool faceLeft = facingDirection.x < 0f;
        Vector3 scale = visual.localScale;
        scale.x = Mathf.Abs(scale.x) * (faceLeft ? -1f : 1f);
        visual.localScale = scale;
    }

    // 상태 이름 하나로 연출을 고르는 창구. 예전 정지 이미지 방식(SpriteStateAnimator)과 이름 규칙이 같아
    // 호출부는 그대로 두고 Animator 로만 갈아끼웠다(Monster_Wraith 와 같은 방식).
    // 같은 이름을 걸러내지 않는다 — 상태에 들어서는 순간에만 부르고, Hurt 처럼 연달아 맞을 때마다 처음부터 다시 재생돼야 하는 모션이 있다.
    void SetStateSprite(string stateName) {
        if (animator != null) {
            int hash = Animator.StringToHash(stateName);
            // 스테이트 이름은 코드에 문자열로 박혀 있고 컨트롤러는 손으로 만드는 것이라, 이름이 어긋나면
            // Animator.Play 가 조용히 아무것도 안 한다. 그 연출이 통째로 사라진 걸 모르고 넘어가지 않도록 알린다.
            if (animator.HasState(0, hash)) animator.Play(hash, 0, 0f);
            else Debug.LogWarning($"[FlyMoveSystem] '{name}' — Animator 에 스테이트 '{stateName}' 가 없습니다. 컨트롤러의 스테이트 이름을 맞추세요.", this);
            return;
        }

        if (spriteAnimator != null) spriteAnimator.Play(stateName);
    }

    #endregion
    #region 돌진 예고 표시

    void BuildAimIndicator() {
        aimIndicator = AttackRangeIndicator.Create(ramRadius, aimIndicatorColor);
    }

    void ShowAimIndicator() {
        if (aimIndicator == null) return;
        aimIndicator.gameObject.SetActive(true);
    }

    void UpdateAimIndicator() {
        if (aimIndicator == null || !aimIndicator.gameObject.activeSelf) return;

        // 몸이 아니라 EnterAim 에서 찍어둔 도착 지점에 고정한다. 몸에 붙이면 몸이 브레이크 걸리며 미끄러지는 동안
        // 링도 같이 흘러가서 "여전히 플레이어를 쫓아간다"는 인상을 준다.
        aimIndicator.transform.position = chargeTargetPoint;

        // 링이 크게 시작해 몸 크기로 좁혀 들어온다. 남은 시간이 크기로 읽혀서 "언제 튀어나올지"까지 전달된다.
        float progress = aimTime > 0f ? Mathf.Clamp01(stateTimer / aimTime) : 1f;
        float scale = Mathf.Lerp(aimIndicatorStartScale, 1f, progress) * ramRadius * 2f * aimIndicatorVisualScale;
        aimIndicator.transform.localScale = new Vector3(scale, scale, 1f);
    }

    void HideAimIndicator() {
        if (aimIndicator != null) aimIndicator.gameObject.SetActive(false);
    }

    #endregion
    #region 기즈모

    private void OnDrawGizmos() {
        // 플레이 전에는 rigid 가 없고 앵커도 안 잡혀 있다. 배치할 때가 순찰 레인이 제일 필요한 순간이라 폴백을 둔다.
        Vector2 position = transform.position;
        Vector2 anchor = Application.isPlaying ? patrolAnchor : position;

        DrawPatrolLane(anchor);

        // 감지 반경 색으로 상태를 표현한다. 에디터 전용 API 없이 상태머신 전체가 씬 뷰에서 읽힌다.
        Gizmos.color = GetStateColor();
        Gizmos.DrawWireSphere(position, detectionRange);

        Gizmos.color = new(0.4f, 0f, 0.4f, 0.6f);
        Gizmos.DrawWireSphere(position, loseRange); // detectionRange 와의 간격이 곧 히스테리시스다.

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(position, ramRadius);

        if (!Application.isPlaying || rigid == null) return;

        DrawDriftVectors(position);
        DrawChargeSnapshot();
        DrawRamSweep(position);
        DrawSightLine(position);
    }

    void DrawPatrolLane(Vector2 anchor) {
        Vector2 top = anchor + Vector2.up * patrolRange;
        Vector2 bottom = anchor - Vector2.up * patrolRange;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(top, bottom);
        Gizmos.DrawLine(top + Vector2.left * 0.2f, top + Vector2.right * 0.2f);
        Gizmos.DrawLine(bottom + Vector2.left * 0.2f, bottom + Vector2.right * 0.2f);
    }

    // 속도(시안)와 시선(녹색)이 벌어진 정도가 곧 드리프트다. 겹쳐 보이면 미끄러지지 않고 있다는 뜻이라
    // forwardDrag / lateralDrag 비율을 눈으로 잡는 유일한 수단이 된다.
    void DrawDriftVectors(Vector2 position) {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(position, position + rigid.linearVelocity * 0.15f);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(position, position + facingDirection * 2f);

        if (state != FlyState.Drift) return;

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(position, position + gizmoAlong * 0.15f);

        Gizmos.color = new(1f, 0.6f, 0f);
        Gizmos.DrawLine(position, position + gizmoLateral * 0.15f);
    }

    // 커밋한 조준점과 플레이어의 실제 위치가 벌어진 만큼이 회피 성공이다.
    void DrawChargeSnapshot() {
        if (state != FlyState.Charge) return;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(chargeOrigin, chargeTargetPoint);
        Gizmos.DrawWireSphere(chargeTargetPoint, 0.15f);
    }

    void DrawRamSweep(Vector2 position) {
        if (state != FlyState.Charge) return;

        Gizmos.color = new(1f, 0.3f, 0.3f, 0.7f);
        Gizmos.DrawWireSphere(lastCheckPosition, ramRadius);
        Gizmos.DrawLine(lastCheckPosition, position);
    }

    void DrawSightLine(Vector2 position) {
        if (target == null) return;

        Gizmos.color = gizmoHasSight ? Color.green : Color.red;
        Gizmos.DrawLine(position, target.position);
    }

    Color GetStateColor() {
        switch (state) {
            case FlyState.Aim: return Color.yellow;
            case FlyState.Charge: return Color.red;
            case FlyState.Drift: return new(1f, 0.6f, 0f);
            default: return Color.gray;
        }
    }

    #endregion
}
