using System.Collections;
using UnityEngine;

// 반투명 상태로 제자리 좌우를 유영하며 순찰하다가, 플레이어를 감지하면 사라졌다 플레이어보다
// 위쪽 어딘가에 다시 나타나 투사체 한 발을 던지는 텔레포트형 원거리 몬스터.
// 근접형(GroundMoveSystem/FlyMoveSystem)과 짝을 이루는 챕터1 원거리 잡몹.
[RequireComponent(typeof(Health))]
public class Monster_Wraith : MonoBehaviour {
    #region 인스펙터 변수

    [Header("연결")]
    public Transform visual; // 반투명·페이드 처리할 스프라이트 자식. **몬스터 밑의 Renderer 오브젝트를 연결하세요.**
    public Collider2D hurtboxCollider; // 사라진 동안 비활성화할 피격 콜라이더. **Hurtbox가 붙은 콜라이더를 연결하세요.**
    public SkillProjectile projectilePrefab; // 발사할 투사체 프리팹. **Is Trigger 콜라이더 하나만 있으면 됩니다.**
    public SpriteStateAnimator spriteAnimator; // 상태별 스프라이트 전환 담당. **visual과 같은 Renderer 오브젝트에 붙이고 연결하세요.**

    [Header("레이어")]
    public LayerMask playerLayer; // 플레이어 레이어. **Player(3) 를 지정하세요.**
    public LayerMask groundLayer; // 지형 레이어. 시야 차단과 텔레포트 위치 검사에 쓴다. **ground(31) 를 지정하세요.** 마임의 투명 벽(SkillWall)도 여기 포함시키면 시야 차단·텔레포트 회피가 그대로 적용된다.
    public LayerMask obstacleLayer; // 발사한 투사체가 이 레이어에 닿으면 그 자리에서 사라진다. 기본값(0)이면 기존처럼 아무것도 막지 않는다. **투명 벽을 막으려면 SkillWall 레이어를 지정하세요.**

    [Header("순찰 (반투명)")]
    public float patrolRange = 2.5f; // 시작 위치를 중심으로 좌우 몇 유닛까지 오갈지.
    public float patrolSpeed = 1.2f; // 왕복 이동 속도.
    public float patrolAlpha = 0.45f; // 순찰 중 반투명 정도.
    public float bobHeight = 0.25f; // 유영하듯 위아래로 흔들리는 높이. 왕복 이동 위에 얹히는 시각적 흔들림이다.
    public float bobSpeed = 2f; // 흔들림 속도.

    [Header("플레이어 감지")]
    public float detectionRange = 7f; // 처음 전투 모드로 전환되는 감지 반경. 최초 발견에만 쓰이고, 한 번 전투에 들어가면 거리와 무관하게 유지된다.
    public bool requireLineOfSight = true; // 최초 발견 시 벽 너머는 못 본 것으로 친다.
    public float losStartOffset = 0.4f; // 시야 검사를 몸 밖에서 시작하는 거리. 자기 콜라이더에 걸려 즉시 막히는 것을 방지.

    [Header("사라짐 · 재등장")]
    public float vanishFadeDuration = 0.25f; // 사라지는 페이드 시간.
    public float appearFadeDuration = 0.2f; // 나타나는 페이드 시간.
    public float hiddenTimeMin = 1.2f; // 사라져 있는 최소 시간.
    public float hiddenTimeMax = 2f; // 사라져 있는 최대 시간.

    [Header("이동 잔상")]
    // 사라져 있는 동안 어디로 가는지 전혀 읽히지 않아, 플레이어 입장에서는 매번 "갑자기 머리 위에 생겼다"가 된다.
    // 그래서 착지 지점으로 즉시 순간이동시키지 않고, 투명해진 채로 흘러가며 잔상만 남긴다.
    public SpriteRenderer trailRenderer; // 잔상으로 찍을 스프라이트. **비워두면 자식에서 자동으로 찾습니다.**
    public float glideDuration = 0.45f; // 사라진 자리에서 착지 지점까지 흘러가는 시간. **0이면 예전처럼 즉시 순간이동한다.**
    [Min(1)] public int trailGhostCount = 8; // 이동 한 번에 남길 잔상 장수.
    public float trailGhostLifetime = 0.35f; // 한 장이 완전히 지워지기까지의 시간.
    public Color trailGhostTint = new(0.7f, 0.45f, 1f, 0.45f); // 잔상 색과 시작 투명도. 조준선과 같은 보랏빛 계열로 맞춘다.
    public int trailSortingOrderOffset = -1; // 본체보다 이만큼 뒤에 그린다.

    [Header("텔레포트 위치")]
    public float teleportMinDistance = 5f; // 플레이어와 너무 가깝게 나타나지 않도록 하는 최소 거리.
    public float teleportMaxDistance = 9f; // 최대 거리.
    // 0=플레이어 오른쪽, 90=정수리 방향, 180=왼쪽 (단위원 각도). 이 범위 전체가 항상 "플레이어보다 위"를 보장한다.
    public float teleportMinAngle = 20f;
    public float teleportMaxAngle = 160f;
    public float teleportCheckRadius = 0.5f; // 착지 지점이 벽 안인지 확인하는 반경.
    public int teleportAttempts = 8; // 유효한 자리를 못 찾으면 각도·거리를 바꿔가며 이만큼 재시도.

    [Header("공격")]
    public float aimDuration = 0.4f; // 재등장 후 발사까지의 조준 시간. 회피 타이밍을 준다.
    public int projectileDamage = 12;
    public float projectileSpeed = 8f;
    public float projectileLifetime = 4f;
    public float postAttackPause = 0.4f; // 발사 후 다시 사라지기까지의 텀.

    [Header("조준선 표시")]
    public Color aimLineColor = new(0.6f, 0.2f, 0.9f, 0.75f); // 재등장 후 발사 직전 보여줄 조준선 색상.

    #endregion
    #region 컴포넌트 변수

    Health health;
    SpriteRenderer[] renderers;
    Color[] baseColors; // 알파만 따로 조절하기 위해 RGB는 보존해 둔다.
    LineRenderer aimLine;

    WraithState state = WraithState.Patrol;

    Vector2 patrolAnchor;
    float patrolCurrentX;
    int patrolDirection = 1;
    float bobTimer;

    Transform target;
    Health targetHealth;

    bool attackInterrupted; // 조준 중(또는 재등장 직후) 플레이어에게 맞으면 켜진다. 이번 공격 턴을 취소하고 바로 사라지게 만든다.

    #endregion

    enum WraithState { Patrol, Combat }

    #region 유니티 라이프 사이클

    void Awake() {
        health = GetComponent<Health>();

        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;

        // 연결을 깜빡해도 잔상이 조용히 사라지지 않도록 첫 스프라이트를 대신 쓴다.
        if (trailRenderer == null && renderers.Length > 0) trailRenderer = renderers[0];

        aimLine = AimLineIndicator.Create(aimLineColor);
    }

    void Start() {
        patrolAnchor = transform.position;
        patrolCurrentX = patrolAnchor.x;
        SetAlpha(patrolAlpha);
        SetStateSprite("Idle");
    }

    void Update() {
        if (health.IsDead) return;

        if (state == WraithState.Patrol) {
            DetectTarget();
            if (HasTarget()) EnterCombat();
            else TickPatrol(Time.deltaTime);
        }

        UpdateAimLine();
    }

    void OnEnable() {
        health.OnDamaged += HandleDamaged;
    }

    void OnDisable() {
        health.OnDamaged -= HandleDamaged;
        HideAimLine();
    }

    // 재등장한 뒤 발사 전에 맞으면(조준 중 포함) 이번 공격 턴을 취소한다. 사라져 있는 동안은
    // 애초에 Hurtbox가 꺼져 있어 이 이벤트가 들어올 수 없으므로 상태를 따로 확인할 필요가 없다.
    // 아직 순찰 중(플레이어를 발견 못한 상태)에 선공을 당하면, 감지 반경·시야와 무관하게 즉시 전투로 들어간다.
    void HandleDamaged(int damage, Vector2 sourcePosition) {
        if (state == WraithState.Patrol) {
            AcquireTargetUnconditionally();
            return;
        }

        attackInterrupted = true;
    }

    // 거리 제한 없이 플레이어를 찾아 바로 타깃으로 삼는다. 선공을 당했다는 것 자체가 플레이어가 어딘가에
    // 존재한다는 증거이므로, detectionRange/시야 조건을 굳이 다시 검사할 필요가 없다.
    void AcquireTargetUnconditionally() {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, 10000f, playerLayer);
        if (hit == null) return;

        target = hit.transform;
        targetHealth = hit.GetComponentInParent<Health>();
        EnterCombat();
    }

    void OnDestroy() {
        if (aimLine != null) Destroy(aimLine.gameObject);
    }

    #endregion
    #region 순찰

    void TickPatrol(float dt) {
        float targetX = patrolAnchor.x + patrolDirection * patrolRange;
        patrolCurrentX = Mathf.MoveTowards(patrolCurrentX, targetX, patrolSpeed * dt);
        if (Mathf.Abs(patrolCurrentX - targetX) < 0.05f) patrolDirection = -patrolDirection;

        // 왕복 이동(patrolCurrentX) 위에 sin 파형으로 유영하는 느낌을 얹는다.
        bobTimer += dt * bobSpeed;
        float bobOffset = Mathf.Sin(bobTimer) * bobHeight;
        transform.position = new Vector3(patrolCurrentX, patrolAnchor.y + bobOffset, transform.position.z);

        SetFacing(patrolDirection);
    }

    void SetFacing(float direction) {
        if (visual == null || Mathf.Approximately(direction, 0f)) return;
        Vector3 scale = visual.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction);
        visual.localScale = scale;
    }

    #endregion
    #region 플레이어 감지

    void DetectTarget() {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRange, playerLayer);
        if (hit == null) { ClearTarget(); return; }
        if (requireLineOfSight && !HasLineOfSight(hit.transform.position)) { ClearTarget(); return; }

        target = hit.transform;
        targetHealth = hit.GetComponentInParent<Health>();
    }

    // 한 번 전투에 들어가면 거리·시야와 무관하게 플레이어가 죽기 전까지는 놓치지 않는다.
    // (텔레포트로 따라붙는 몬스터라 거리 기준 이탈을 두면 오히려 의도한 압박감이 사라진다.)
    bool StillHasTarget() {
        return HasTarget();
    }

    void ClearTarget() {
        target = null;
        targetHealth = null;
    }

    bool HasTarget() => target != null && targetHealth != null && !targetHealth.IsDead;

    bool HasLineOfSight(Vector2 point) {
        Vector2 origin = transform.position;
        Vector2 toTarget = point - origin;
        float distance = toTarget.magnitude;
        if (distance <= losStartOffset) return true;

        Vector2 direction = toTarget / distance;
        Vector2 start = origin + direction * losStartOffset;
        RaycastHit2D hit = Physics2D.Raycast(start, direction, distance - losStartOffset, groundLayer);
        return hit.collider == null;
    }

    #endregion
    #region 전투 진입 · 루프

    void EnterCombat() {
        state = WraithState.Combat;
        StartCoroutine(CombatLoop());
    }

    void EnterPatrol() {
        state = WraithState.Patrol;
        patrolAnchor = transform.position; // 마지막으로 나타난 자리를 새 순찰 중심으로 삼는다.
        patrolCurrentX = patrolAnchor.x;
        SetHurtboxEnabled(true);
        StartCoroutine(FadeAlpha(GetCurrentAlpha(), patrolAlpha, appearFadeDuration));
        SetStateSprite("Idle");
    }

    // 사라짐 → 대기 → 텔레포트 → 재등장 → 조준 → 발사를 반복하다가, 플레이어를 놓치면 순찰로 복귀한다.
    IEnumerator CombatLoop() {
        while (true) {
            if (!StillHasTarget()) break;

            attackInterrupted = false;

            SetHurtboxEnabled(false); // 투명해지기 시작하는 순간부터 무적 처리한다. 페이드가 끝나길 기다리면 반투명해지는 도중에 맞을 수 있다.
            yield return FadeAlpha(GetCurrentAlpha(), 0f, vanishFadeDuration);

            if (!StillHasTarget()) break; // 사라지는 동안 놓쳤으면 숨은 채로 순찰 복귀.

            Vector2 landingPos = ChooseTeleportPosition();
            yield return TravelWhileHidden(landingPos);

            SetHurtboxEnabled(true);
            SetStateSprite("Idle");
            yield return FadeAlpha(0f, 1f, appearFadeDuration);

            if (!StillHasTarget()) break; // 숨어 있는 사이 플레이어가 멀어졌으면 공격 없이 종료.

            yield return AimAtTarget(aimDuration);

            // 조준 중(또는 재등장 직후) 맞았으면 이번 턴은 던지지 않고, 대기 없이 바로 다음 반복의 사라짐으로 넘어간다.
            if (!attackInterrupted && StillHasTarget()) FireProjectile();
            if (!attackInterrupted) {
                SetStateSprite("Idle"); // 발사 후 사라지기 전까지 공격 포즈로 굳어 있지 않도록 되돌린다.
                yield return new WaitForSeconds(postAttackPause);
            }
        }

        EnterPatrol();
    }

    #endregion
    #region 텔레포트 위치 계산

    Vector2 ChooseTeleportPosition() {
        for (int i = 0; i < teleportAttempts; i++) {
            Vector2 candidate = SampleTeleportPoint(teleportMinDistance, teleportMaxDistance);
            if (!Physics2D.OverlapCircle(candidate, teleportCheckRadius, groundLayer)) return candidate;
        }

        // 그래도 못 찾으면 조건을 완화한다. 계속 실패해서 영원히 숨어 있는 것보다는
        // 벽에 살짝 겹쳐서라도 반드시 자리를 잡는 편이 낫다. "플레이어보다 위" 조건만은 그대로 유지한다.
        for (int i = 0; i < teleportAttempts; i++) {
            Vector2 candidate = SampleTeleportPoint(teleportMinDistance * 0.5f, teleportMaxDistance);
            if (!Physics2D.OverlapCircle(candidate, teleportCheckRadius * 0.5f, groundLayer)) return candidate;
        }

        return SampleTeleportPoint(teleportMinDistance, teleportMaxDistance);
    }

    Vector2 SampleTeleportPoint(float minDistance, float maxDistance) {
        float angle = Random.Range(teleportMinAngle, teleportMaxAngle) * Mathf.Deg2Rad;
        float distance = Random.Range(minDistance, maxDistance);
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        return (Vector2)target.position + offset;
    }

    #endregion
    #region 숨은 채 이동 (잔상)

    // 사라져 있는 시간을 "제자리 대기 → 잔상을 흘리며 이동" 두 토막으로 나눈다.
    // 이동을 마지막에 두는 이유는, 잔상이 아직 남아 있는 동안 곧바로 모습을 드러내야
    // "저기서 여기로 왔고 지금 여기서 나타난다"가 한 호흡으로 읽히기 때문이다.
    // 먼저 이동해 버리면 착지 지점에서 기다리는 사이 잔상이 다 지워져 아무 단서도 남지 않는다.
    IEnumerator TravelWhileHidden(Vector2 landingPos) {
        float hiddenTime = Random.Range(hiddenTimeMin, hiddenTimeMax);
        float glide = Mathf.Clamp(glideDuration, 0f, hiddenTime);

        float wait = hiddenTime - glide;
        if (wait > 0f) yield return new WaitForSeconds(wait);

        if (glide <= 0f) {
            transform.position = landingPos; // 뿅. (glideDuration 을 0으로 두면 예전 동작 그대로)
            yield break;
        }

        Vector2 start = transform.position;
        SetFacing(landingPos.x - start.x);

        float ghostInterval = glide / trailGhostCount;
        float ghostTimer = 0f;
        float elapsed = 0f;
        SpawnTrailGhost(); // 출발점에도 한 장. 어디서 떠났는지가 보여야 궤적이 선으로 이어진다.

        while (elapsed < glide) {
            elapsed += Time.deltaTime;

            // 확 튀어나갔다가 착지 지점에서 감속한다. 잔상이 도착점 쪽에 몰려 "여기로 온다"가 강조된다.
            float linear = Mathf.Clamp01(elapsed / glide);
            float eased = 1f - (1f - linear) * (1f - linear);
            transform.position = Vector2.Lerp(start, landingPos, eased);

            ghostTimer += Time.deltaTime;
            while (ghostTimer >= ghostInterval) {
                ghostTimer -= ghostInterval;
                SpawnTrailGhost();
            }

            yield return null;
        }

        transform.position = landingPos;
        SpawnTrailGhost(); // 도착점에 한 장 더. 모습을 드러내기 직전에 시선을 이쪽으로 붙잡아 둔다.
    }

    // 본체는 알파 0으로 투명해져 있지만, 잔상은 trailGhostTint 를 그대로 쓰므로 이때도 보인다.
    void SpawnTrailGhost() {
        AfterimageGhost.Spawn(trailRenderer, trailGhostTint, trailGhostLifetime, trailSortingOrderOffset);
    }

    #endregion
    #region 공격

    IEnumerator AimAtTarget(float duration) {
        if (!HasTarget() || attackInterrupted) yield break;

        SetStateSprite("Aim");
        ShowAimLine();
        float elapsed = 0f;
        while (elapsed < duration) {
            if (!HasTarget() || attackInterrupted) break; // 조준 중 맞으면 즉시 중단.
            UpdateAimLinePositions();
            AimLineIndicator.ScrollDashes(aimLine);
            elapsed += Time.deltaTime;
            yield return null;
        }
        HideAimLine();
    }

    void FireProjectile() {
        if (projectilePrefab == null || target == null) return;

        SetStateSprite("Attack");
        Vector2 direction = ((Vector2)target.position - (Vector2)transform.position).normalized;
        SkillProjectile projectile = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        projectile.Launch(direction, playerLayer, projectileDamage, projectileSpeed, projectileLifetime, health, 1, obstacleLayer);
    }

    #endregion
    #region 시각 처리 (페이드 · 알파)

    IEnumerator FadeAlpha(float from, float to, float duration) {
        if (duration <= 0f) { SetAlpha(to); yield break; }

        float elapsed = 0f;
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        SetAlpha(to);
    }

    void SetAlpha(float alpha) {
        for (int i = 0; i < renderers.Length; i++) {
            if (renderers[i] == null) continue;
            Color c = baseColors[i];
            c.a = alpha;
            renderers[i].color = c;
        }
    }

    float GetCurrentAlpha() {
        return renderers.Length > 0 && renderers[0] != null ? renderers[0].color.a : patrolAlpha;
    }

    void SetHurtboxEnabled(bool value) {
        if (hurtboxCollider != null) hurtboxCollider.enabled = value;
    }

    void SetStateSprite(string stateName) {
        if (spriteAnimator != null) spriteAnimator.Play(stateName);
    }

    #endregion
    #region 조준선 표시

    void ShowAimLine() {
        if (aimLine != null) aimLine.gameObject.SetActive(true);
    }

    void UpdateAimLine() {
        if (aimLine == null || !aimLine.gameObject.activeSelf) return;
        UpdateAimLinePositions();
    }

    void UpdateAimLinePositions() {
        if (target == null) return;
        aimLine.SetPosition(0, transform.position);
        aimLine.SetPosition(1, target.position);
    }

    void HideAimLine() {
        if (aimLine != null) aimLine.gameObject.SetActive(false);
    }

    #endregion
    #region 기즈모

    void OnDrawGizmos() {
        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        if (!Application.isPlaying || target == null) return;

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, target.position);
    }

    #endregion
}
