using UnityEngine;

// 플레이어 스프라이트를 상태(정지 · 걷기 · 상승 · 낙하 · 착지 · 공격)에 맞춰 갈아 끼운다.
//
// 걷기만 있을 때는 Player_move 가 SpriteFlipbook 을 켜고 끄는 것으로 충분했지만, 점프와 공격이
// 들어오면서 "무엇을 먼저 보여줄지" 고르는 판단이 필요해졌다. 그 판단을 이동 코드와 전투 코드에
// 나눠 두면 서로 상대의 상태를 몰라 공격 중에 걷기 프레임이 끼어드는 식으로 어긋나므로,
// 포즈를 고르는 일은 이 컴포넌트 한 곳에서만 한다. 이동·전투 쪽은 자기 상태만 공개한다.
//
// 프레임 교체는 Update 에서 끝낸다. Player_DashAfterimage 가 LateUpdate 에서 현재 스프라이트를
// 복제해 잔상을 찍으므로, 그보다 먼저 이번 프레임의 포즈가 확정되어 있어야 한다.
//
// 시간은 Time.deltaTime(스케일 시간)을 쓴다. 히트스톱 동안에는 포즈도 같이 멈춰야 타격이 박히는
// 느낌이 나고, 슬로모(GameSpeedController) 중에는 동작도 함께 느려져야 어색하지 않기 때문이다.
//
// **Player_Renderer(SpriteRenderer 가 붙은 오브젝트)에 붙이세요.**
// 프레임 목록은 `Tools ▸ FCC ▸ Player ▸ Apply Player Animation` 이 시트에서 잘라 채워 준다.
[RequireComponent(typeof(SpriteRenderer))]
public class Player_Animator : MonoBehaviour {
    #region 인스펙터 변수

    [Header("연결")]
    public Player_move move; // 접지·입력·수직 속도를 읽는다. 비워두면 부모에서 찾는다.
    public Player_Combat combat; // 공격 시작 신호를 받는다. 비워두면 부모에서 찾는다.

    [Header("프레임 · 지상")]
    public Sprite[] idleFrames; // 정지 포즈. 한 장만 넣어도 된다.
    public float idleFrameRate = 6f; // 숨쉬기 같은 여러 장짜리 정지 동작이 들어올 때를 대비해 둔다.
    public Sprite[] walkFrames; // 걷기(반복).
    public float walkFrameRate = 10f;

    [Header("프레임 · 공중")]
    public Sprite[] riseFrames; // 도약 → 상승. 마지막 칸에서 멈춰 상승하는 동안 유지된다.
    public Sprite[] fallFrames; // 낙하. 마찬가지로 마지막 칸에서 멈춘다.
    public Sprite[] landFrames; // 착지(눌림 → 웅크림). 끝까지 한 번만 재생한다.
    public float airFrameRate = 14f;
    public float landFrameRate = 14f;
    // 이보다 짧게 떴다가 내려온 경우에는 착지 동작을 건너뛴다. 지형 이음매를 지날 때 접지 판정이
    // 한두 프레임 끊기는데, 그때마다 착지 자세가 끼어들면 걷는 내내 덜컹거린다. 게임을 시작한
    // 직후 접지 판정이 처음 켜지는 순간에도 이 값 덕분에 착지 연출이 튀지 않는다.
    public float landMinAirTime = 0.08f;
    // 이 속도(유닛/초)보다 느리게 올라가면 낙하 포즈로 넘긴다. 0으로 두면 정점 근처에서 두 포즈가
    // 한 프레임씩 번갈아 나와 덜덜 떨린다.
    public float fallVelocity = -0.5f;

    [Header("프레임 · 공격")]
    public Sprite[] attackFrames; // 휘두르기. 한 번만 재생하며, 끝나기 전에는 다른 포즈가 끼어들지 못한다.
    // 기본 16은 Player_Combat 의 선딜레이(attackDelay 0.15초)에 3번째 칸(= 칼이 지나가는 칸)이
    // 오도록 맞춘 값이다. 선딜레이를 바꾸면 여기도 같이 맞춰야 맞는 순간과 그림이 어긋나지 않는다.
    public float attackFrameRate = 16f;

    [Header("진단")]
    // 포즈를 고르는 근거를 플레이 중 인스펙터에서 그대로 볼 수 있게 남긴다. 공중 포즈에서 빠져나오는
    // 조건은 접지뿐이라, 동작이 굳었을 때 원인이 애니메이션인지 접지 판정인지 여기서 바로 갈린다.
    public bool logStateChanges; // 켜면 포즈가 바뀔 때마다 콘솔에 한 줄씩 남긴다.
    [SerializeField] string debugState; // 지금 고른 포즈.
    [SerializeField] bool debugGrounded; // Player_move 가 알려준 접지 여부.
    [SerializeField] float debugVerticalVelocity; // 상승/낙하를 가르는 수직 속도.

    #endregion
    #region 컴포넌트 변수

    SpriteRenderer spriteRenderer;

    State state = State.Idle;
    Sprite[] clip; // 지금 재생 중인 프레임 목록.
    float clipRate;
    bool clipLoop;
    int frameIndex;
    float frameTimer;
    bool clipFinished; // 반복하지 않는 클립이 마지막 칸까지 간 뒤 true. 상태 전환을 허락하는 신호로도 쓴다.

    bool wasGrounded = true; // 착지하는 순간을 잡기 위한 직전 프레임의 접지 상태.
    float airTime; // 땅에서 떨어져 있던 시간. 착지 동작을 보여 줄지 고르는 데 쓴다.
    bool attackPending; // 이벤트로 들어온 공격을 다음 Update 에서 한 번만 처리한다.

    enum State { Idle, Walk, Rise, Fall, Land, Attack }

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (move == null) move = GetComponentInParent<Player_move>();
        // Player_Combat 은 부모가 아니라 형제 오브젝트(Combat_Manager)에 있으므로 부모를 거슬러
        // 올라가면 찾지 못한다. 이동 컴포넌트가 붙은 플레이어 루트에서 아래로 훑어야 한다.
        if (combat == null && move != null) combat = move.GetComponentInChildren<Player_Combat>(true);

        if (move == null) Debug.LogError("[Player_Animator] 'move'(Player_move) 를 찾지 못했습니다.", this);
        if (combat == null) Debug.LogWarning("[Player_Animator] 'combat'(Player_Combat) 을 찾지 못해 공격 동작이 재생되지 않습니다.", this);

        StartClip(State.Idle);
    }

    // 공격 신호는 Player_Combat 이 선딜레이를 시작하는 순간 날아온다. 판정이 들어가는 순간이 아니라
    // 입력을 받아들인 순간이므로, 여기서 바로 휘두르기를 시작하면 버튼과 그림이 붙어 보인다.
    void OnEnable() {
        if (combat != null) combat.OnAttackStarted += OnAttackStarted;
    }

    void OnDisable() {
        if (combat != null) combat.OnAttackStarted -= OnAttackStarted;
    }

    void Update() {
        if (move == null) return;

        bool grounded = move.IsGrounded;
        bool landed = grounded && !wasGrounded && airTime >= landMinAirTime; // airTime 은 떠 있는 동안 쌓인 값이므로 갱신 전에 읽는다.
        wasGrounded = grounded;
        airTime = grounded ? 0f : airTime + Time.deltaTime;

        if (attackPending) {
            attackPending = false;
            StartClip(State.Attack); // 연타해도 스윙이 처음부터 다시 보여야 몇 번 쳤는지 읽힌다.
        } else {
            State next = ResolveState(grounded, landed);
            if (next != state) StartClip(next);
        }

        Advance();

        debugState = state.ToString();
        debugGrounded = grounded;
        debugVerticalVelocity = move.VerticalVelocity;
    }

    #endregion
    #region 상태 결정

    // 위쪽 조건일수록 우선한다. 한 번 시작한 공격·착지 연출은 끝까지 보여 주고, 그 외에는
    // 공중 → 걷기 → 정지 순으로 고른다.
    State ResolveState(bool grounded, bool landed) {
        if (state == State.Attack && !clipFinished) return State.Attack;
        if (state == State.Land && !clipFinished && grounded) return State.Land; // 착지 직후 다시 떠오르면 아래 공중 판정으로 넘어간다.

        if (landed && HasFrames(landFrames)) return State.Land;
        if (!grounded) return move.VerticalVelocity > fallVelocity ? State.Rise : State.Fall;

        // 걷기 조건은 이전 구현(Player_move.UpdateWalkAnimation)과 같다. 대사·컷씬으로 이동이 잠기면
        // 입력이 남아 있어도 제자리걸음처럼 보이지 않도록 정지 포즈로 돌린다.
        if (!move.isMovementLocked && move.MoveInputX != 0f) return State.Walk;
        return State.Idle;
    }

    void OnAttackStarted(Vector2 origin, float facing) {
        if (!HasFrames(attackFrames)) return;

        attackPending = true;
    }

    #endregion
    #region 프레임 재생

    void StartClip(State next) {
        // Awake 에서도 불리므로 move 를 아직 못 찾았을 수 있다.
        if (logStateChanges && move != null) Debug.Log($"[Player_Animator] {state} → {next} (접지 {move.IsGrounded} · 수직속도 {move.VerticalVelocity:0.00})", this);

        state = next;
        frameIndex = 0;
        frameTimer = 0f;
        clipFinished = false;

        switch (next) {
            case State.Walk: SetClip(walkFrames, walkFrameRate, true); break;
            case State.Rise: SetClip(riseFrames, airFrameRate, false); break;
            case State.Fall: SetClip(fallFrames, airFrameRate, false); break;
            case State.Land: SetClip(landFrames, landFrameRate, false); break;
            case State.Attack: SetClip(attackFrames, attackFrameRate, false); break;
            default: SetClip(idleFrames, idleFrameRate, true); break;
        }
    }

    // 그림이 아직 없는 상태(빈 목록)로 바꾸면 스프라이트를 지우지 않고 직전 포즈를 그대로 둔다.
    // 렌더러를 비우면 플레이어가 한 프레임 통째로 사라져 눈에 띄기 때문이다.
    void SetClip(Sprite[] frames, float rate, bool loop) {
        if (!HasFrames(frames)) {
            clip = null;
            clipFinished = true;
            return;
        }

        clip = frames;
        clipRate = rate;
        clipLoop = loop;
        spriteRenderer.sprite = frames[0];
    }

    void Advance() {
        if (clip == null || clipFinished) return;

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(clipRate, 0.01f);
        if (frameTimer < frameDuration) return;

        frameTimer -= frameDuration;

        if (frameIndex + 1 >= clip.Length) {
            if (!clipLoop) {
                clipFinished = true; // 마지막 칸을 그대로 유지한다. 상승·낙하처럼 계속 이어져야 하는 포즈가 있다.
                return;
            }
            frameIndex = 0;
        } else {
            frameIndex++;
        }

        spriteRenderer.sprite = clip[frameIndex];
    }

    static bool HasFrames(Sprite[] frames) {
        return frames != null && frames.Length > 0;
    }

    #endregion
}
