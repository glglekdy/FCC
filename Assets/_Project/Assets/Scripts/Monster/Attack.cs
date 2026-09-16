using UnityEngine;

[RequireComponent(typeof(GroundMoveSystem))]
public class Attack : MonoBehaviour {
    #region 인스펙터 변수

    [Header("공격 판정")]
    public Transform attackPoint; // **몬스터 공격 판정용 빈 오브젝트를 만드세요.**
    public float attackRange = 1f; // 공격이 닿는 범위.
    public LayerMask playerLayer; // 플레이어 레이어.

    [Header("공격 타이밍")]
    public float attackDelay = 1f; // 판정이 들어가기 전 선딜레이. 유저가 피할 시간을 준다.
    public float attackCooldown = 1f; // 공격 후 다시 움직이기 시작하기까지의 대기 시간.

    [Header("공격 데미지")]
    public int attackDamage = 10;

    #endregion
    #region 컴포넌트 변수

    GroundMoveSystem moveSystem;
    Animator animator; // 공격 준비·공격 모션 재생용. **비어 있어도 동작하지만 없으면 모션이 재생되지 않는다.**
    AttackState state = AttackState.Idle;
    float stateTimer;
    Vector3 currentAttackWorldPos; // 공격이 시작된 순간의 월드 좌표. 판정 도중 몬스터가 움직여도 이 위치에 고정된다.

    #endregion

    enum AttackState { Idle, Windup, Cooldown }

    static readonly int WindupTrigger = Animator.StringToHash("Windup"); // Animator Controller 의 트리거 파라미터와 이름을 맞춰야 한다.
    static readonly int StrikeTrigger = Animator.StringToHash("Strike");

    #region 유니티 라이프 사이클

    void Awake() {
        moveSystem = GetComponent<GroundMoveSystem>();
        animator = GetComponent<Animator>();
    }

    void Update() {
        // 묶인 동안은 공격 타이머를 멈춘다. 선딜레이를 취소하지 않고 멈추는 이유는, 준비 모션을 되돌리는
        // 애니메이션이 없어 취소하면 준비 자세로 굳은 채 서 있게 되기 때문이다.
        if (moveSystem.IsRestrained) return;

        switch (state) {
            case AttackState.Idle:
                CheckAttackRange();
                break;
            case AttackState.Windup:
                TickWindup();
                break;
            case AttackState.Cooldown:
                TickCooldown();
                break;
        }
    }

    #endregion

    #region 공격 관련 함수

    void CheckAttackRange() {
        if (attackPoint == null) return;

        bool playerInRange = Physics2D.OverlapCircle(attackPoint.position, attackRange, playerLayer);
        if (!playerInRange) return;

        StartWindup();
    }

    void StartWindup() {
        state = AttackState.Windup;
        stateTimer = attackDelay;
        moveSystem.isMovementLocked = true; // 선딜레이 동안 제자리에서 공격 준비.
        currentAttackWorldPos = attackPoint.position; // 공격 시작 순간의 위치를 고정.
        if (animator != null) animator.SetTrigger(WindupTrigger);
    }

    void TickWindup() {
        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f) return;

        if (animator != null) animator.SetTrigger(StrikeTrigger);
        DealDamage();
        StartCooldown();
    }

void DealDamage() {
        // 선딜레이가 끝난 시점에 플레이어가 범위 안에 있는지 다시 확인.
        // 단일 OverlapCircle 은 콜라이더를 하나만 돌려줘서 이동용 몸통 콜라이더가 먼저 잡힐 수 있다.
        // 전부 훑어 Hurtbox 가 붙은 콜라이더만 골라낸다.
        Collider2D[] hits = Physics2D.OverlapCircleAll(currentAttackWorldPos, attackRange, playerLayer);
        foreach (Collider2D hit in hits) {
            if (!hit.TryGetComponent(out Hurtbox hurtbox) || hurtbox.OwnerHealth == null) continue;

            hurtbox.OwnerHealth.TakeDamage(attackDamage, currentAttackWorldPos);
            break; // 플레이어는 하나뿐이라 찾으면 더 볼 필요 없다.
        }
    }

    void StartCooldown() {
        state = AttackState.Cooldown;
        stateTimer = attackCooldown; // 쿨다운 동안에도 계속 정지 상태 유지.
    }

    void TickCooldown() {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f) {
            moveSystem.isMovementLocked = false; // 쿨다운이 끝나는 시점에 이동 잠금 해제.
            state = AttackState.Idle;
        }
    }

    // 공격 판정 범위 표시. 인게임에 원을 띄우던 표시는 뺐고, 범위 조정용으로 씬 뷰에만 남긴다.
    // 골랐을 때만 그리는 이유는, 항상 그리면 몬스터가 여럿 놓인 방에서 원이 겹쳐 지형이 안 보이기 때문이다.
    void OnDrawGizmosSelected() {
        if (attackPoint == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }

    #endregion
}