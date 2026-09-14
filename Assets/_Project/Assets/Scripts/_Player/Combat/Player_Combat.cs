using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player_Combat : MonoBehaviour {
    #region 인스펙터 변수

    [Header("공격 판정")]
    public Transform attackPoint; // **플레이어 공격 판정용 빈 오브젝트를 만드세요.**
    public float attackRange = 1f; // 공격이 닿는 범위.
    public LayerMask enemyLayer; // 몬스터 레이어.

    [Header("공격 타이밍")]
    public float attackDelay = 0.15f; // 판정이 들어가기 전 선딜레이.
    public float attackCooldown = 0.4f; // 공격 후 다음 공격까지의 대기 시간.

    [Header("공격 데미지")]
    public int attackDamage = 10;

    #endregion
    #region 컴포넌트 변수

    AttackState state = AttackState.Idle;
    float stateTimer;
    Vector3 currentAttackWorldPos; // 공격이 시작된 순간의 월드 좌표. 판정 도중 플레이어가 움직여도 이 위치에 고정된다.
    readonly HashSet<Health> hitTargets = new(); // 한 번의 공격에서 이미 때린 대상. 매번 새로 할당하지 않도록 재사용한다.

    #endregion

    enum AttackState { Idle, Windup, Cooldown }

    #region 유니티 라이프 사이클

    void Update() {
        switch (state) {
            case AttackState.Windup:
                TickWindup();
                break;
            case AttackState.Cooldown:
                TickCooldown();
                break;
        }
    }

    #endregion
    #region 입력 처리

    void OnAttack(InputValue value) {
        if (!value.isPressed) return;
        if (state != AttackState.Idle) return;

        StartWindup();
    }

    #endregion
    #region 공격 관련 함수

    void StartWindup() {
        state = AttackState.Windup;
        stateTimer = attackDelay;
        currentAttackWorldPos = attackPoint.position; // 공격 시작 순간의 위치를 고정.
    }

    void TickWindup() {
        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f) return;

        DealDamage();
        StartCooldown();
    }

    void DealDamage() {
        if (attackPoint == null) return;

        // OverlapCircle은 콜라이더를 하나만 돌려줘서 겹쳐 있는 몬스터 중 한 마리만 맞는다. 범위 안의 전부를 때린다.
        Collider2D[] hits = Physics2D.OverlapCircleAll(currentAttackWorldPos, attackRange, enemyLayer);
        if (hits.Length == 0) return;

        hitTargets.Clear();
        foreach (Collider2D hit in hits) {
            // Hurtbox 가 없는 콜라이더(몬스터 이동용 몸통 콜라이더 등)는 판정에서 제외한다.
            if (!hit.TryGetComponent(out Hurtbox hurtbox) || hurtbox.OwnerHealth == null) continue;

            // 한 대상에 콜라이더가 여러 개 붙어 있어도 한 번만 때린다.
            if (!hitTargets.Add(hurtbox.OwnerHealth)) continue;

            hurtbox.OwnerHealth.TakeDamage(attackDamage, currentAttackWorldPos);
        }
    }

    void StartCooldown() {
        state = AttackState.Cooldown;
        stateTimer = attackCooldown;
    }

    void TickCooldown() {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f) {
            state = AttackState.Idle;
        }
    }

    // 공격 판정 범위 표시. 인게임에 원을 띄우던 표시는 뺐고, 범위 조정용으로 씬 뷰에만 남긴다.
    // 골랐을 때만 그리는 이유는, 항상 그리면 플레이어를 볼 때마다 원이 따라다녀 씬이 지저분해지기 때문이다.
    void OnDrawGizmosSelected() {
        if (attackPoint == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }

    #endregion
}
