using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Player_move : MonoBehaviour
{
    #region 인스펙터 변수

    [Header("무브먼트 설정")]
    public float speed = 6f; // 플레이어 이동 스피드.
    public float maxSpeed = 5f; // 플레이어가 걸을 수 있는 최대 속도.
    public float jumpForce = 11f; // 점프 파워.

    [Header("2단 점프")]
    // 곡예사에게 배우는 이동 패시브. 스킬 슬롯을 차지하지 않고, 한 번 배우면 계속 켜져 있다.
    // 진행 중에는 UnlockAbility 가 켜고 세이브가 기억한다. **테스트할 때만 인스펙터에서 직접 켜세요.**
    public bool doubleJumpEnabled = false;
    bool canDoubleJump; // 착지 후 아직 공중 점프를 쓰지 않았는지.

    [Header("대시")]
    // 2단 점프와 같은 곡예사 패시브. 예전에는 처음부터 켜져 있었지만, 메트로배니아 구조에서 "배워야 지나갈 수 있는 길"을
    // 만들려면 잠겨 있어야 해서 해금제로 바꿨다. **테스트할 때만 인스펙터에서 직접 켜세요.**
    public bool dashEnabled = false;
    public float dashSpeed = 18f; // 대시 중 유지하는 수평 속도.
    public float dashDuration = 0.18f; // 대시가 지속되는 시간(초).
    public float dashCooldown = 0.6f; // 대시 재사용 대기시간(초).

    float dashTimer; // 0보다 크면 대시 중 - 이 동안은 SmoothMove 대신 고정 속도를 그대로 유지한다.
    float dashCooldownTimer; // 남은 대시 쿨타임.
    float dashDirection; // 대시 시작 시점의 좌우 방향(+1/-1)을 고정해, 도중에 방향키를 바꿔도 궤적이 흔들리지 않게 한다.

    // 대시 잔상(Player_DashAfterimage) 같은 외부 연출이 대시 구간을 알 수 있도록 노출한다.
    public bool IsDashing => dashTimer > 0f;

    private float koyoteTime = 0.2f; // 고요테 타임 설정
    private float koyoteTimeCounter; // 고요테 타임 카운터


    public bool useFacingRotation = false; // 플레이어가 바라보는 방향을 정하는 방식이 회전인지 스케일인지 여부. true면 회전, false면 스케일로 방향 전환.

    private bool IsFacingRight; // 플레이어가 오른쪽을 바라보고 있는지 여부.

    [Header("Ground Check")]
    public Transform groundCheck; // **'Player_Controller' 발밑에 빈 오브젝트를 만드세요.**
    public Vector2 groundCheckSize = new Vector2(0.5f, 0.1f);
    public LayerMask groundLayer; // Ground 레이어 설정 필요

    private bool isGrounded;

    [Header("진단")]
    // 접지 판정은 점프·코요테·애니메이션이 모두 올라타 있는 값이라, 어긋나면 원인을 바깥에서 알 길이
    // 없다. 판정 결과뿐 아니라 "왜 그렇게 판정했는지"까지 플레이 중 인스펙터에서 바로 읽게 남긴다.
    [SerializeField] bool debugGrounded; // 이번 물리 프레임의 접지 판정 결과.
    [SerializeField] int debugGroundHits; // 발밑 상자에 걸린 ground 레이어 콜라이더 수.
    [SerializeField] string debugGroundReason; // 그렇게 판정한 근거.

    // OverlapBox 결과를 매 FixedUpdate 마다 새 배열로 받으면 GC가 계속 쌓이므로 버퍼를 재사용한다.
    readonly Collider2D[] groundHits = new Collider2D[8];
    ContactFilter2D groundFilter;

    [Header("Coyote Time")]
    public float coyoteDuration = 0.15f; // 코요테 점프 허용 시간 (초)
    public float coyoteCounter; // 남은 코요테 시간을 체크할 타이머

    [Header("Jump Buffer")]
    public float jumpBufferDuration = 0.15f; // 착지 전에 미리 누른 점프 입력을 기억해두는 시간 (초)
    private float jumpBufferCounter; // 남은 점프 버퍼 시간을 체크할 타이머

    [Header("이동 패시브 해금 알림")]
    public bool announceAbilityUnlock = true; // 배우는 순간 화면 위에 기술 이름을 띄운다 (AreaTitleView).
    public string abilityAnnounceSubtitle = "곡예사에게 배운 기술"; // 알림에서 기술 이름 아래 붙는 문구.

    #endregion
    #region 외부 제어용 변수

    [HideInInspector]
    public bool isMovementLocked; // Player_Combat 등 외부 시스템이 공격 중 이동을 멈출 때 사용.

    // 스킬이 리지드바디를 직접 몰고 가는 동안 켠다(Close Call 의 줄 타고 날아가기).
    // isMovementLocked 를 쓰지 않는 이유: 그 잠금은 가로 속도를 매 스텝 0으로 덮어써 날아가는 속도를 지워 버리고,
    // 대사·컷씬용이라 상호작용·스킬 입력까지 함께 막는다. 이쪽은 이동 로직만 비켜 준다.
    [HideInInspector]
    public bool isExternallyDriven;

    // Player_Animator 가 매 프레임 읽어 포즈(정지·걷기·상승·낙하·착지)를 고른다. 이동 로직이 이미
    // 들고 있는 값을 그대로 넘기는 이유는, 애니메이션 쪽에서 접지 판정을 다시 하면 판정 기준이
    // 갈라져 실제로는 땅에 있는데 공중 포즈가 나오는 식으로 어긋나기 때문이다.
    public bool IsGrounded => isGrounded;
    public float MoveInputX => moveInput.x;
    public float VerticalVelocity => rigid != null ? rigid.linearVelocityY : 0f;

    #endregion
    #region 컴포넌트 변수
    Rigidbody2D rigid;
    Collider2D bodyCollider; // 발바닥 높이를 재는 몸통 콜라이더(트리거가 아닌 것).
    Vector2 moveInput;
    float knockbackTimer; // 0보다 크면 넉백 중 - 일반 이동 로직을 건너뛰어 물리 힘이 그대로 유지되게 한다.

    #endregion

    #region 유니티 라이프 사이클
    void Awake() {
        rigid = GetComponent<Rigidbody2D>();
        rigid.interpolation = RigidbodyInterpolation2D.Interpolate;

        // 루트에는 상호작용 탐지용 트리거도 함께 붙어 있어, 실제로 땅을 딛는 콜라이더만 골라야 한다.
        foreach (Collider2D col in GetComponents<Collider2D>()) {
            if (col.isTrigger) continue;
            bodyCollider = col;
            break;
        }
        if (bodyCollider == null) Debug.LogWarning("[Player_move] 트리거가 아닌 몸통 콜라이더(Collider2D)를 찾지 못해 통과형 발판 착지를 판정 상자 윗변으로 대신 잽니다.", this);

        // IsFacingRight의 초기값이 실제 스프라이트/스케일의 방향과 어긋나면
        // 처음 반대 방향키를 눌렀을 때 방향 전환이 씹히는 문제가 있어, 시작 시 실제 상태와 동기화한다.
        IsFacingRight = transform.localScale.x > 0f;
    }

    void OnMove(InputValue value) {
        moveInput = value.Get<Vector2>();
    }

    void Update() {
        if (moveInput.x != 0) {
            TurnCheck();
        }

        SaveLoadSystem(); // 저장 & 불러오기
    }

    void FixedUpdate() {
        // 접지 판정/코요테타임/점프버퍼는 물리 힘이 실제로 적용되는 시점(FixedUpdate)과 맞춰야 한다.
        // Rigidbody2D의 Interpolation 때문에 Update()에서 읽는 transform 위치는 화면 표시용으로
        // 보간된 값이라 실제 물리 위치와 어긋날 수 있고, 이 오차가 점프 이륙/착지 순간의
        // isGrounded 판정을 한두 프레임 틀리게 만들어 점프가 살짝 걸리는 느낌으로 체감됐다.
        isGrounded = CheckGrounded();
        debugGrounded = isGrounded;

        // 스킬이 속도를 직접 넣는 중이다. 여기서 SmoothMove 나 점프 버퍼가 돌면 날아가는 궤적을 덮어쓴다.
        if (isExternallyDriven) {
            dashTimer = 0f;
            return;
        }

        coyoteJumpTime(); // 코요테
        JumpBufferTime(); // 점프 버퍼 (착지 전 미리 누른 점프 입력 처리)

        if (dashCooldownTimer > 0f) dashCooldownTimer -= Time.fixedDeltaTime;

        if (dashTimer > 0f) {
            Dash();
        } else {
            // immediateMove();
            SmoothMove();
        }
    }

    #endregion

    #region 접지 판정

    // 한 방향 발판(OneWayPlatform)은 물리적으로 통과되는 중에도 OverlapBox 에는 그대로 걸린다.
    // 그대로 두면 발판을 뚫고 올라가는 동안 isGrounded 가 켜져 코요테 시간과 2단 점프가 공짜로
    // 충전되어 공중에서 무한히 점프할 수 있게 되므로, 통과형 지형은 "상승 중이 아니고 발판 윗면이
    // 발밑까지 내려와 있을 때" 즉 실제로 올라선 상태일 때만 밟고 있는 것으로 인정한다.
    //
    // "발밑"은 판정 상자가 아니라 몸통 콜라이더의 바닥으로 잰다. 판정 상자는 씬마다 스프라이트 발끝에 맞춰
    // 옮겨지곤 하는데, 상자 윗변이 발바닥보다 아래로 내려가면 발판 위에 서 있어도 윗변이 늘 발판 속에 묻혀
    // "아직 통과 중"으로만 판정된다. 실제로 Play_First 씬에서 플레이어를 0.8배로 줄이며 상자를 내렸다가,
    // 착지해도 공중 포즈가 풀리지 않고 점프도 안 되는 상태가 됐다.
    const float FootProbeLift = 0.03f; // 착지 순간 콜라이더가 발판에 살짝 파고드는 만큼을 봐주는 여유(유닛).

    bool CheckGrounded() {
        groundFilter.useTriggers = false;
        groundFilter.SetLayerMask(groundLayer);

        int count = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundFilter, groundHits);
        float footY = bodyCollider != null
            ? bodyCollider.bounds.min.y + FootProbeLift
            : groundCheck.position.y + groundCheckSize.y * 0.5f;

        debugGroundHits = count;

        for (int i = 0; i < count; i++) {
            Collider2D col = groundHits[i];
            if (col == null) continue;

            if (!col.usedByEffector) { // 통과되지 않는 일반 지형은 겹친 것만으로 접지로 본다.
                debugGroundReason = "일반 지형을 밟음";
                return true;
            }

            if (rigid.linearVelocityY > 0.01f) { // 아래에서 뚫고 올라가는 중.
                debugGroundReason = "통과형 발판을 뚫고 상승 중";
                continue;
            }

            // 발바닥이 발판 안에 있으면 아직 발판 속을 지나는 중. bounds.max.y 로 윗면을 재지 않는 이유는,
            // 타일맵 발판은 층 전체가 CompositeCollider2D 하나로 합쳐져 bounds 가 가장 높은 발판을 가리키므로
            // 낮은 발판에서는 영영 착지로 인정되지 않기 때문이다.
            if (col.OverlapPoint(new Vector2(groundCheck.position.x, footY))) {
                debugGroundReason = $"발바닥({footY:0.00})이 '{col.name}' 안에 있음 - 아직 통과 중으로 판정";
                continue;
            }

            debugGroundReason = "통과형 발판 위에 올라섬";
            return true;
        }

        if (count == 0) debugGroundReason = "발밑 상자에 ground 레이어 지형이 하나도 안 걸림";
        return false;
    }

    #endregion
    #region 플레이어 움직임 관련 함수
    // 즉각적인 움직임 함수.
    void ImmediateMove() {
        rigid.linearVelocity = new Vector2(moveInput.x * speed, rigid.linearVelocity.y);
    }

    // 부드러운 움직임 함수.
    void SmoothMove() {
        if (knockbackTimer > 0f) {
            knockbackTimer -= Time.fixedDeltaTime;
            return;
        }

        if (isMovementLocked) {
            rigid.linearVelocity = new Vector2(0f, rigid.linearVelocityY);
            return;
        }

        rigid.AddForce(moveInput, ForceMode2D.Impulse);
        // Debug.Log(moveInput);

        // 최고 속도 관리.
        if (rigid.linearVelocityX >= maxSpeed) {
            rigid.linearVelocity = new Vector2(maxSpeed, rigid.linearVelocityY);
        }
        else if (rigid.linearVelocityX <= maxSpeed * (-1)) {
            rigid.linearVelocity = new Vector2(maxSpeed * (-1), rigid.linearVelocityY);
        }
    }

    public void ApplyKnockback(Vector2 force, float duration = 0.15f) {
        knockbackTimer = duration;
        rigid.linearVelocity = Vector2.zero;
        rigid.AddForce(force, ForceMode2D.Impulse);
    }

    void OnDash(InputValue value) {
        if (!value.isPressed) return;
        if (!dashEnabled) return;
        if (isMovementLocked || isExternallyDriven || knockbackTimer > 0f) return;
        if (dashTimer > 0f || dashCooldownTimer > 0f) return;

        dashDirection = IsFacingRight ? 1f : -1f;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;
    }

    // 대시 중에는 중력을 무시하고 수평으로만 미끄러지게 한다 - 세로 속도가 섞이면 대시 궤적이 지저분해진다.
    void Dash() {
        dashTimer -= Time.fixedDeltaTime;
        rigid.linearVelocity = new Vector2(dashDirection * dashSpeed, 0f);
    }

// --TODO(박찬) : 점프 구현 (고요테 점프로 구현할 예정)
    void coyoteJumpTime() {
        if (isGrounded) {
            coyoteCounter = coyoteDuration;
            canDoubleJump = true; // 땅에 닿아 있는 동안 공중 점프 1회분을 항상 충전해 둔다.
        } else {
            coyoteCounter -= Time.fixedDeltaTime;
        }
    }

    void OnJump(InputValue value) {
        if (!value.isPressed) return;
        if (isExternallyDriven) return;

        if (coyoteCounter > 0f) {
            ExecuteJump();
        } else if (doubleJumpEnabled && canDoubleJump) {
            canDoubleJump = false; // 착지하기 전까지 다시 쓸 수 없다.
            ExecuteJump();
        } else {
            jumpBufferCounter = jumpBufferDuration; // 공중에서 미리 누른 점프 입력을 버퍼에 저장, 착지 시 바로 점프.
        }
    }

    void ExecuteJump() {
        isGrounded = false;
        rigid.linearVelocity = new Vector2(rigid.linearVelocityX, 0f); // 기존 속도를 초기화하여 키 씹힘 방지.

        // rigid.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        rigid.AddForceY(jumpForce, ForceMode2D.Impulse);
        coyoteCounter = 0f; // 타이머를 0으로 만들어 이단 점프 방지.
        jumpBufferCounter = 0f; // 버퍼 소모.
    }

    void JumpBufferTime() {
        if (jumpBufferCounter <= 0f) return;

        jumpBufferCounter -= Time.fixedDeltaTime;

        if (isGrounded) {
            ExecuteJump();
        }
    }

    // 점프 판정 범위
    private void OnDrawGizmos()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }
    }

    #endregion
    #region 카메라 관련 함수
    
    // --TODO(김지훈) : 카메라가 플레이어의 행동에 따라 움직이도록 하는 함수.
    void CameraControl() {
        
    }

    // 플레이어가 바라보는 방향을 체크해주고 방향이 바뀌면 플레이어의 방향을 바꿔주는 함수.
    void TurnCheck() {
        if (moveInput.x > 0 && !IsFacingRight) {
            Turn();
        }
        else if (moveInput.x < 0 && IsFacingRight) {
            Turn();
        }
    }

    private void Turn(){
        IsFacingRight = !IsFacingRight;
        ApplyFacing();
    }

    // 스킬이 플레이어를 특정 방향으로 돌려세울 때 쓴다(Close Call 로 날아가는 쪽을 보게 하기 등).
    // 0이면 그대로 둔다.
    public void FaceDirection(float directionX) {
        if (Mathf.Approximately(directionX, 0f)) return;

        bool faceRight = directionX > 0f;
        if (faceRight != IsFacingRight) Turn();
    }

    // 현재 IsFacingRight 상태를 현재 모드(회전/스케일)에 맞게 트랜스폼에 반영.
    // 모드가 전환되는 순간(카메라 영역 진입/이탈)에도 호출해서 반대 축에 남아있는
    // 이전 모드의 값(스케일 -1 또는 회전 180)이 겹쳐 방향이 어긋나는 것을 방지한다.
    public void ApplyFacing() {
        float yRotation = IsFacingRight ? 0f : 180f;
        // 부호만 바꾸고 크기는 지금 값을 그대로 둔다. ±1 로 덮어쓰면 씬에서 플레이어를 줄여 둔 경우
        // (Play_First 의 0.8 등) 돌아서는 순간 가로만 1 로 돌아가 캐릭터가 옆으로 퍼진다.
        float xScale = Mathf.Abs(transform.localScale.x) * (IsFacingRight ? 1f : -1f);

        if (useFacingRotation) {
            Vector3 rotator = new Vector3(transform.rotation.eulerAngles.x, yRotation, transform.rotation.eulerAngles.z);
            transform.rotation = Quaternion.Euler(rotator);

            // 스케일 모드에서 넘어왔을 때 남아있을 수 있는 음수 스케일을 중립화.
            Vector3 neutralScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
            transform.localScale = neutralScale;
        } else {
            transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            Vector3 newScale = new Vector3(xScale, transform.localScale.y, transform.localScale.z);
            transform.localScale = newScale;
        }
    }

    #endregion

    #region 이동 패시브 해금

    public bool HasAbility(Player_Ability ability) {
        switch (ability) {
            case Player_Ability.DoubleJump: return doubleJumpEnabled;
            case Player_Ability.Dash: return dashEnabled;
            default: return false;
        }
    }

    // 스토리에서 기술을 배웠을 때. 이미 배운 기술이면 아무것도 하지 않는다 — 불러오기 뒤 같은 대사를 다시 봐도
    // 알림이 반복되지 않게 하기 위함이다(SkillManager.UnlockFromStory 와 같은 규칙).
    // 씬 장치는 이것을 직접 부르지 않고 Player_AbilityUnlocker 를 거친다.
    public bool UnlockAbility(Player_Ability ability) {
        if (ability == Player_Ability.None || HasAbility(ability)) return false;

        SetAbility(ability, true);

        // 파괴된 뒤에도 C# 참조가 남을 수 있어 ?. 대신 != null 로 Unity의 == 오버로드를 탄다.
        if (announceAbilityUnlock && AreaTitleView.Instance != null) {
            AreaTitleView.Announce(GetAbilityDisplayName(ability), abilityAnnounceSubtitle);
        }
        return true;
    }

    // 알림 없이 켜고 끈다. 세이브 복원용.
    public void SetAbility(Player_Ability ability, bool enabled) {
        switch (ability) {
            case Player_Ability.DoubleJump:
                doubleJumpEnabled = enabled;
                break;
            case Player_Ability.Dash:
                dashEnabled = enabled;
                if (!enabled) dashTimer = 0f; // 대시 도중에 잠기면 고정 속도로 계속 미끄러진다.
                break;
        }
    }

    static string GetAbilityDisplayName(Player_Ability ability) {
        switch (ability) {
            case Player_Ability.DoubleJump: return "2단 점프";
            case Player_Ability.Dash: return "대시";
            default: return ability.ToString();
        }
    }

    #endregion

    #region 플레이어 데이터 저장/ 불러오기 관련 함수

    // 정식 저장 지점은 거울(SaveMirror)이다. 아래 F5/F9는 거울까지 가지 않고 바로 확인하기 위한 개발용 단축키로,
    // 체력·목표 진행도까지 포함한 저장/복원은 SaveManager가 전부 처리한다.
    public void SaveGame() {
        if (SaveManager.Instance != null) SaveManager.Instance.SaveGame(string.Empty); // checkpointId를 비워 퀵세이브로 기록.
    }

    public void LoadGame() {
        if (SaveManager.Instance != null) SaveManager.Instance.LoadGame();
    }

    public void SaveLoadSystem() {
        if (Keyboard.current == null) return; // 키보드가 연결되지 않은 환경(패드 전용)에서 널 참조를 피한다.

        if (Keyboard.current.f5Key.wasPressedThisFrame) {
            SaveGame();
        }

        if (Keyboard.current.f9Key.wasPressedThisFrame) {
            LoadGame();
        }
    }

    #endregion
}