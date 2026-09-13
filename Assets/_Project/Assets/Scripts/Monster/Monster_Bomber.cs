using System.Collections.Generic;
using UnityEngine;

// 플레이어를 발견하면 달려와 몸을 부풀리다 터지는 자폭형 지상 몬스터.
// 순찰·추격·낭떠러지 처리는 GroundMoveSystem 이 그대로 맡고, 이 컴포넌트는 도화선과 폭발만 담당한다.
//
// 지상 근접형의 Attack 컴포넌트를 대신하는 자리다. **둘을 같은 오브젝트에 함께 붙이지 마세요.**
// 둘 다 이동 잠금과 공격 판정을 건드려 서로의 상태를 덮어쓴다.
//
// 설계 의도: 빠르게 붙되, 불이 붙는 순간 그 자리에 못 박힌 듯 멈춰 선다.
// 한 번 붙은 도화선은 꺼지지 않으므로 플레이어의 정답이
// "닿기 전에 잡거나(체력이 아주 낮다), 불이 붙었으면 폭발 범위 밖으로 빠져나온다" 둘로 갈린다.
// 멈춰 서지 않고 계속 쫓아오게 두면 도망이 정답이 될 수 없어 회피가 사라진다.
[RequireComponent(typeof(GroundMoveSystem))]
[RequireComponent(typeof(Health))]
public class Monster_Bomber : MonoBehaviour {
    #region 인스펙터 변수

    [Header("연결")]
    public Transform visual; // 부풀릴 스프라이트 자식. **몬스터 밑의 Renderer 오브젝트를 연결하세요.**
    public SpriteStateAnimator spriteAnimator; // 상태별 스프라이트 전환 담당. **visual과 같은 Renderer 오브젝트에 붙이고 연결하세요.**

    [Header("레이어")]
    public LayerMask playerLayer; // 점화 조건을 검사할 레이어. **Player(3) 를 지정하세요.**
    public LayerMask explosionLayers; // 폭발에 휘말리는 레이어. **기본은 Player 만 지정하세요.** 몬스터 레이어를 더하면 연쇄 폭발이 된다.

    [Header("도화선")]
    public float fuseTriggerRange = 1.8f; // 이 거리 안으로 플레이어가 들어오면 점화. **GroundMoveSystem 의 stopDistanceToPlayer 보다 크게 잡으세요.** 작으면 플레이어 앞에 멈춰 선 채 영영 불이 붙지 않는다.
    public float fuseDuration = 1.1f; // 점화부터 폭발까지의 시간. 플레이어가 폭발 범위 밖으로 빠져나올 수 있는 유일한 창이다.

    [Header("점화 연출")]
    public float swellScale = 1.35f; // 폭발 직전의 최대 부풀기 배율.
    public Color fuseColor = new(1f, 0.35f, 0.2f, 1f); // 깜빡일 때 덧입히는 색.
    public float blinkStartInterval = 0.22f; // 점화 직후의 깜빡임 간격.
    public float blinkEndInterval = 0.05f; // 폭발 직전의 깜빡임 간격. 간격이 좁아지는 속도가 남은 시간을 알려준다.

    [Header("폭발")]
    public float explosionRadius = 2.4f; // 실제 피해 판정 반경.
    public int explosionDamage = 20;
    public Color explosionColor = new(1f, 0.45f, 0.1f, 0.9f); // 예고 링과 폭발 링에 함께 쓰는 색.
    public float explosionFlashDuration = 0.35f; // 터진 자리에 남는 링이 퍼지며 사라지는 시간.

    [Header("처치 시 처리")]
    // 도화선에 불이 붙은 뒤 처치당하면 그 자리에서 그대로 터진다. 붙기 전에 잡으면 안전하다.
    // 끄면 "붙기 전에 잡는다"는 정답이 사라지므로 기본값은 켜 둔다.
    public bool detonateOnFuseDeath = true;

    #endregion
    #region 컴포넌트 변수

    GroundMoveSystem moveSystem;
    Health health;
    HitFlash hitFlash; // 점화 중에는 꺼 둔다. 아래 LightFuse() 주석 참고.

    SpriteRenderer[] renderers;
    Color[] baseColors; // 깜빡임이 꺼진 순간 되돌릴 원래 색.
    Vector3 baseVisualScale;

    SpriteRenderer warningRing; // 점화 중 보여줄 폭발 반경 예고 링.

    bool fuseLit;
    bool hasDetonated; // 연쇄 폭발과 사망 이벤트가 겹쳐도 두 번 터지지 않게 막는 가드.
    float fuseTimer;
    float blinkTimer;
    bool blinkOn;

    readonly HashSet<Health> damagedTargets = new(); // 한 대상의 콜라이더가 여러 개여도 한 번만 때린다.

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        moveSystem = GetComponent<GroundMoveSystem>();
        health = GetComponent<Health>();
        hitFlash = GetComponent<HitFlash>();

        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;

        if (visual != null) baseVisualScale = visual.localScale;

        // 링은 부모에 붙지 않는 독립 오브젝트다. 몬스터가 사라질 때 같이 지우지 않으면 씬에 남는다.
        warningRing = AttackRangeIndicator.Create(explosionRadius, explosionColor);
        warningRing.gameObject.name = "BomberWarningRing";
    }

    void OnEnable() {
        health.OnDeath += HandleDeath;
    }

    void OnDisable() {
        health.OnDeath -= HandleDeath;
    }

    void Update() {
        if (health.IsDead) return;

        if (!fuseLit) CheckTrigger();
        else TickFuse(Time.deltaTime);
    }

    void OnDestroy() {
        if (warningRing != null) Destroy(warningRing.gameObject);
    }

    #endregion
    #region 점화

    void CheckTrigger() {
        if (!Physics2D.OverlapCircle(transform.position, fuseTriggerRange, playerLayer)) return;

        LightFuse();
    }

    void LightFuse() {
        fuseLit = true;
        fuseTimer = fuseDuration;
        blinkTimer = 0f;

        // 불이 붙는 순간 추격을 접고 그 자리에 멈춘다. 되돌리지 않는다 — 도화선은 꺼지지 않기 때문이다.
        // 폭발 지점이 여기서 고정되므로 플레이어는 링만 보고 벗어날 방향을 판단할 수 있다.
        // (넉백은 GroundMoveSystem 이 이 잠금보다 먼저 처리하므로, 밀어내서 떼어놓는 대응은 그대로 살아 있다.)
        moveSystem.isMovementLocked = true;

        // HitFlash 도 같은 SpriteRenderer 의 색과 visual 의 스케일을 만진다. 켜 둔 채로 두면
        // 도화선 깜빡임·부풀기와 매 프레임 서로 덮어써 둘 다 끊겨 보인다. 점화 중에는
        // 이쪽 연출이 훨씬 중요한 정보이므로 피격 플래시를 통째로 양보받는다.
        if (hitFlash != null) hitFlash.enabled = false;

        if (spriteAnimator != null) spriteAnimator.Play("Fuse");
        warningRing.gameObject.SetActive(true);
    }

    void TickFuse(float dt) {
        fuseTimer -= dt;

        float progress = fuseDuration <= 0f ? 1f : Mathf.Clamp01(1f - fuseTimer / fuseDuration);
        TickSwell(progress);
        TickBlink(dt, progress);

        // 점화와 동시에 멈추지만 넉백에는 여전히 밀린다. 밀려난 자리가 곧 폭발 지점이므로 링도 따라가야 한다.
        warningRing.transform.position = transform.position;

        if (fuseTimer <= 0f) Detonate();
    }

    void TickSwell(float progress) {
        if (visual == null) return;

        visual.localScale = baseVisualScale * Mathf.Lerp(1f, swellScale, progress);
    }

    void TickBlink(float dt, float progress) {
        blinkTimer -= dt;
        if (blinkTimer > 0f) return;

        blinkTimer = Mathf.Lerp(blinkStartInterval, blinkEndInterval, progress);
        blinkOn = !blinkOn;
        ApplyBlinkColor();
    }

    void ApplyBlinkColor() {
        for (int i = 0; i < renderers.Length; i++) {
            if (renderers[i] == null) continue;

            if (!blinkOn) {
                renderers[i].color = baseColors[i];
                continue;
            }

            // 알파는 원래 값을 유지한다. 반투명하게 세팅된 몬스터가 깜빡일 때만 불투명해지면 튄다.
            Color c = fuseColor;
            c.a = baseColors[i].a;
            renderers[i].color = c;
        }
    }

    #endregion
    #region 폭발

    void Detonate() {
        if (hasDetonated) return;
        hasDetonated = true;

        Vector2 center = transform.position;

        // 이 오브젝트는 바로 아래 KillSelf() 에서 Destroy 되므로 연출은 반드시 바깥에 띄운다.
        Monster_ExplosionEffect.Play(center, explosionRadius, explosionColor, explosionFlashDuration);
        if (HitVfx.Instance != null) HitVfx.Instance.PlayDeath(center);

        ApplyExplosionDamage(center);
        KillSelf(center);
    }

    void ApplyExplosionDamage(Vector2 center) {
        damagedTargets.Clear();

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, explosionRadius, explosionLayers);
        foreach (Collider2D hit in hits) {
            if (!hit.TryGetComponent(out Hurtbox hurtbox) || hurtbox.OwnerHealth == null) continue;
            if (hurtbox.OwnerHealth == health) continue; // 자기 자신은 아래에서 따로 죽인다.
            if (!damagedTargets.Add(hurtbox.OwnerHealth)) continue;

            // 폭발 중심을 원점으로 넘겨야 넉백이 바깥으로 밀려난다 (HitReactor 가 이 값으로 방향을 만든다).
            hurtbox.OwnerHealth.TakeDamage(explosionDamage, center);
        }
    }

    void KillSelf(Vector2 center) {
        // 사망은 TakeDamage 를 통해서만 일어나야 HitReactor 의 사망 연출이 한 번만 재생된다.
        // 점화 중 무적이 걸려 있으면 자폭이 조용히 씹히므로 먼저 걷어낸다.
        health.ClearInvincible();
        health.TakeDamage(Mathf.Max(1, health.CurrentHealth), center);
    }

    // 도화선에 불이 붙은 뒤 처치당하면 그 자리에서 터진다.
    // KillSelf() 로 스스로 죽는 경로도 이 이벤트를 타지만, hasDetonated 가 이미 켜져 있어 다시 터지지 않는다.
    void HandleDeath(Vector2 sourcePosition) {
        if (!detonateOnFuseDeath || !fuseLit) return;

        Detonate();
    }

    #endregion
    #region 기즈모

    void OnDrawGizmosSelected() {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, fuseTriggerRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }

    #endregion
}
