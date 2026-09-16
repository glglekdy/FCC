using UnityEngine;

// Health의 OnDamaged/OnDeath 이벤트를 받아 히트스톱/카메라쉐이크/파티클/넉백을 트리거한다.
// 피격 대상 본인의 시각 반응(플래시, 스쿼시)은 HitFlash가 따로 담당한다.
// 플레이어와 몬스터 양쪽에 Health와 함께 부착해서 사용한다.
[RequireComponent(typeof(Health))]
public class HitReactor : MonoBehaviour {
    #region 인스펙터 변수

    [Header("넉백")]
    public float knockbackForce = 6f; // 넉백 세기.

    // 넉백이 도는 동안 일반 이동 로직을 건너뛰는 시간. 예전에는 이 값이 Player_move·GroundMoveSystem·
    // FlyMoveSystem 세 곳의 ApplyKnockback 기본 인자로 각각 박혀 있었고, 호출부인 여기가 인자를 넘기지
    // 않아 한 곳만 고치면 플레이어와 몬스터의 넉백 감각이 조용히 어긋났다. 값을 여기 하나로 모은다.
    public float knockbackLockDuration = 0.15f;

    [Header("사망 잔해")]
    // **사망한 자리에 남을 시체 프리팹을 넣으세요. 비워두면 아무것도 남지 않습니다.**
    // Health.Die() 가 본체를 곧바로 Destroy 하므로 쓰러지는 모션은 이 프리팹이 대신 재생한다.
    public GameObject deathCorpsePrefab;

    [Header("이펙트 위치")]
    public Vector2 hitPointOffset = Vector2.zero; // 캐릭터 기준으로 이펙트 위치를 미세 조정.
    public float hitPointPullIn = 0.35f; // 공격이 들어온 쪽으로 얼마나 당겨서 띄울지. 0이면 캐릭터 중심에서 터진다.

    #endregion
    #region 컴포넌트 변수

    Health health;
    GroundMoveSystem groundMove;
    FlyMoveSystem flyMove;
    Player_move playerMove;
    SpriteRenderer visualRenderer; // 시체가 어느 쪽을 보고 남을지 판단할 기준. 아래 SpawnCorpse 주석 참고.

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        health = GetComponent<Health>();
        groundMove = GetComponent<GroundMoveSystem>();
        flyMove = GetComponent<FlyMoveSystem>();
        playerMove = GetComponent<Player_move>();
        visualRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    void OnEnable() {
        health.OnDamaged += HandleDamaged;
        health.OnDeath += HandleDeath;
    }

    void OnDisable() {
        health.OnDamaged -= HandleDamaged;
        health.OnDeath -= HandleDeath;
    }

    #endregion
    #region 피격 반응

    void HandleDamaged(int damage, Vector2 sourcePosition) {
        Vector2 hitDir = GetHitDirection(sourcePosition);
        Vector2 hitPoint = GetHitPoint(hitDir);
        bool isLethal = health.CurrentHealth <= 0; // OnDamaged는 체력을 깎은 뒤에 발행되므로 여기서 처치 여부를 알 수 있다.

        // 싱글턴은 파괴된 뒤에도 참조가 남을 수 있어 ?. 대신 Unity의 == 오버로드를 타는 != null로 검사한다.
        if (HitVfx.Instance != null) HitVfx.Instance.PlaySpark(hitPoint, hitDir);

        // 처치했을 때는 HandleDeath에서 더 강한 피드백을 재생하므로 여기서는 건너뛴다 (히트스톱 이중 적용 방지).
        if (!isLethal && HitFeedback.Instance != null) {
            HitFeedback.Instance.PlayHit(hitPoint, hitDir, damage, false);
        }

        ApplyKnockback(hitDir);
    }

    // Health.Die()가 곧바로 Destroy를 호출하므로, 사망 연출은 반드시 이 오브젝트 바깥(HitVfx 싱글턴)에서 재생해야 한다.
    void HandleDeath(Vector2 sourcePosition) {
        Vector2 hitDir = GetHitDirection(sourcePosition);
        Vector2 position = transform.position;

        if (HitFeedback.Instance != null) HitFeedback.Instance.PlayHit(position, hitDir, 0, true);
        if (HitVfx.Instance != null) HitVfx.Instance.PlayDeath(position);

        SpawnCorpse();
    }

    void SpawnCorpse() {
        if (deathCorpsePrefab == null) return;

        GameObject corpse = Instantiate(deathCorpsePrefab, transform.position, Quaternion.identity);

        // 크기는 루트에서, 좌우는 실제로 그림이 걸린 렌더러에서 가져온다.
        // 크기를 루트에서 보는 것은 씬에서 이 몬스터만 키워 놨을 때 시체도 같은 덩치로 남기기 위해서다.
        // 시체 프리팹은 몬스터 프리팹과 같은 1 배율 기준으로 만들어져 있어 그대로 곱하면 맞는다.
        // 좌우를 렌더러에서 보는 것은 뒤집는 자리가 몬스터마다 다르기 때문이다 — GroundMoveSystem 은 루트
        // 스케일을, Monster_Wraith 는 Renderer 자식 스케일을 뒤집는다. 렌더러의 lossyScale 을 보면 어느
        // 쪽이든 최종적으로 바라보던 방향이 나온다.
        float facing = visualRenderer != null && visualRenderer.transform.lossyScale.x < 0f ? -1f : 1f;

        Vector3 scale = corpse.transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Abs(transform.localScale.x) * facing;
        scale.y = Mathf.Abs(scale.y) * Mathf.Abs(transform.localScale.y);
        corpse.transform.localScale = scale;
    }

    void ApplyKnockback(Vector2 hitDir) {
        Vector2 force = hitDir * knockbackForce;

        if (groundMove != null) groundMove.ApplyKnockback(force, knockbackLockDuration);
        if (flyMove != null) flyMove.ApplyKnockback(force, knockbackLockDuration);
        if (playerMove != null) playerMove.ApplyKnockback(force, knockbackLockDuration);
    }

    // 공격 원점에서 피격자를 향하는 방향. 넉백과 이펙트/쉐이크가 같은 방향을 공유한다.
    Vector2 GetHitDirection(Vector2 sourcePosition) {
        Vector2 direction = (Vector2)transform.position - sourcePosition;
        if (direction == Vector2.zero) direction = Vector2.right;
        return direction.normalized;
    }

    // 캐릭터 중심에서 공격이 들어온 쪽으로 살짝 당긴 지점. 이펙트가 몸통 한가운데가 아니라 맞은 면에서 터진다.
    Vector2 GetHitPoint(Vector2 hitDir) {
        return (Vector2)transform.position + hitPointOffset - hitDir * hitPointPullIn;
    }

    #endregion
}
