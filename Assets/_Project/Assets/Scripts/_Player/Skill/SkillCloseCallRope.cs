using System.Collections;
using UnityEngine;

// Close Call 이 던진 줄 하나. 뻗기 → (맞으면) 묶기 → 처형 기준 이하면 줄 타고 날아가 들이받기 → 되감기를 순서대로 돌고 스스로 사라진다.
// 스킬 에셋(Skill_CloseCall)은 ScriptableObject 라 코루틴을 돌릴 수 없어서, 던질 때마다 씬에 이 오브젝트를 하나 만든다.
//
// 플레이어 오브젝트에 붙이지 않고 따로 띄우는 이유는, 줄이 도는 동안 플레이어 쪽 컴포넌트 구성을 건드리지 않기 위해서다.
// 대신 플레이어를 몰고 가는 도중 이 오브젝트가 사라지면(씬 전환 등) OnDestroy 가 조작을 반드시 돌려준다.
//
// 줄은 조준선(AimLineIndicator)과 같은 방식으로 코드에서 LineRenderer 를 만든다. 화면 UI가 아니라 월드 연출이라
// 「UI 구현 규칙」 대상은 아니지만, **줄 전용 스프라이트가 생기면 프리팹으로 옮기는 편이 좋습니다.**
public class SkillCloseCallRope : MonoBehaviour {
    #region 런타임 변수

    Skill_CloseCall skill;
    Skill_CloseCall.LevelData data;
    Vector2 direction;

    Transform owner;
    Health ownerHealth;
    Player_move ownerMove;
    Rigidbody2D ownerBody;

    Health hitHealth; // 줄에 걸린 대상. 뻗기 단계가 채워 넣는다.
    Collider2D hitCollider; // 대상의 피격 콜라이더. 날아갈 목표 지점과 닿았는지 판정에 쓴다.

    LineRenderer line;
    Vector2 tip; // 줄 끝의 월드 좌표.
    bool isDrivingOwner; // 플레이어를 몰고 가는 중인지. 도중에 사라져도 조작을 돌려주기 위한 표시.

    // 판정 버퍼. 줄은 동시에 하나만 나가므로 공유해도 된다.
    static readonly RaycastHit2D[] castBuffer = new RaycastHit2D[16];
    static Material ropeMaterial;

    #endregion
    #region 생성

    public static SkillCloseCallRope Spawn(Skill_CloseCall skill, Transform owner, Vector2 direction, Skill_CloseCall.LevelData data) {
        SkillCloseCallRope rope = new GameObject("CloseCallRope").AddComponent<SkillCloseCallRope>();
        rope.Begin(skill, owner, direction, data);
        return rope;
    }

    void Begin(Skill_CloseCall skill, Transform owner, Vector2 direction, Skill_CloseCall.LevelData data) {
        this.skill = skill;
        this.owner = owner;
        this.direction = direction.normalized;
        this.data = data;

        ownerHealth = owner.GetComponentInParent<Health>();
        ownerMove = owner.GetComponentInParent<Player_move>();
        ownerBody = owner.GetComponentInParent<Rigidbody2D>();

        BuildLine();
        tip = Origin;
        DrawRope();

        StartCoroutine(Run());
    }

    void BuildLine() {
        line = gameObject.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.widthMultiplier = skill.ropeWidth;
        line.startColor = skill.ropeColor;
        line.endColor = skill.ropeColor;
        line.sortingOrder = 54; // 조준선(55) 바로 뒤. 던지는 순간 둘이 겹쳐도 조준선이 가려지지 않게.
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        // 줄마다 새로 만들면 던질 때마다 머티리얼이 쌓인다. 색은 LineRenderer 정점 색으로 넣으므로 하나를 같이 쓴다.
        if (ropeMaterial == null) ropeMaterial = new Material(Shader.Find("Sprites/Default"));
        line.sharedMaterial = ropeMaterial;
    }

    #endregion
    #region 유니티 라이프 사이클

    void OnDestroy() {
        ReleaseOwner(bounce: false);
    }

    #endregion
    #region 진행

    IEnumerator Run() {
        yield return Extend();

        if (IsTargetAlive()) {
            // **구속을 피해보다 먼저 겁니다.** 순찰 중인 Monster_Wraith 는 피해를 받는 즉시 전투 루프를 시작해
            // 사라져 버리는데, 먼저 묶여 있어야 그 루프가 사라지지 않고 기다린다.
            foreach (IRestrainable restrainable in hitHealth.GetComponentsInChildren<IRestrainable>()) {
                restrainable.Restrain(data.restrainDuration);
            }

            // 공격 원점은 줄이 나온 자리(플레이어)다. 스파크·넉백 방향이 전부 여기서 파생된다.
            hitHealth.TakeDamage(data.damage, Origin);

            if (CanExecute()) yield return FlyAndExecute();
            else yield return HoldRestraint();
        }

        yield return Retract();
        Destroy(gameObject);
    }

    // 줄 끝을 사거리까지 밀어내며 매 프레임 그 구간을 훑는다. 몬스터에 걸리거나 지형에 막히면 멈춘다.
    // 시작점을 매 프레임 플레이어 위치로 다시 잡으므로, 던진 뒤 움직여도 줄이 손에서 떨어지지 않는다.
    IEnumerator Extend() {
        float length = 0f;

        while (length < data.range) {
            if (owner == null) yield break;

            Vector2 origin = Origin;
            Vector2 from = origin + direction * length;
            float step = Mathf.Min(skill.ropeSpeed * Time.deltaTime, data.range - length);

            float blockedAt = CastObstacle(from, step);

            if (CastTarget(from, Mathf.Min(step, blockedAt), out float hitDistance)) {
                tip = from + direction * hitDistance;
                DrawRope();
                yield break;
            }

            if (blockedAt < step) {
                tip = from + direction * blockedAt; // 벽에 박힌 자리에서 되감는다.
                DrawRope();
                yield break;
            }

            length += step;
            tip = origin + direction * length;
            DrawRope();
            yield return null;
        }
    }

    // 처형 기준에 못 미친 대상은 묶어 두기만 한다. 묶인 동안 줄을 대상에 걸어 둬서 "지금 묶여 있다"가 눈에 보이게 한다.
    // 묶인 동안 플레이어가 때려 기준 아래로 떨어뜨려도 날아가지 않는다 — 평타 도중 갑자기 끌려가면 조작을 빼앗긴 것처럼 느껴진다.
    IEnumerator HoldRestraint() {
        float remaining = data.restrainDuration;

        while (remaining > 0f && owner != null && IsTargetAlive()) {
            remaining -= Time.deltaTime;
            tip = GetTargetCenter();
            DrawRope();
            yield return null;
        }
    }

    // 줄을 타고 대상에게 날아가 들이받는다. 닿으면 남은 체력을 전부 깎아 즉사시킨다.
    IEnumerator FlyAndExecute() {
        if (ownerMove == null || ownerBody == null) yield break; // 몰고 갈 몸이 없으면 묶기만 한 것으로 끝낸다.

        TakeOwner();

        bool arrived = false;
        float elapsed = 0f;

        while (elapsed < skill.maxFlyTime) {
            if (owner == null || !IsTargetAlive()) break;

            Vector2 center = Origin;
            if (Vector2.Distance(center, GetClosestTargetPoint(center)) <= skill.arriveDistance) {
                arrived = true;
                break;
            }

            // 가장 가까운 면이 아니라 몸 중심을 향해 날아가야 옆면을 스치지 않고 정면으로 들이받는다.
            Vector2 toTarget = GetTargetCenter() - center;
            Vector2 flyDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : direction;

            ownerBody.linearVelocity = flyDirection * skill.flySpeed;
            ownerMove.FaceDirection(flyDirection.x);

            tip = GetTargetCenter();
            DrawRope();

            // 속도는 물리 스텝 단위로 넣는다. 프레임 단위로 넣으면 프레임레이트에 따라 도착 판정이 한 스텝씩 어긋난다.
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        // 사망은 TakeDamage 를 통해서만 일어나야 HitReactor 의 사망 연출이 한 번만 재생된다(SetHealth(0) 금지).
        if (arrived && IsTargetAlive()) {
            hitHealth.ClearInvincible();
            hitHealth.TakeDamage(Mathf.Max(1, hitHealth.CurrentHealth), Origin);
        }

        ReleaseOwner(bounce: arrived);
    }

    IEnumerator Retract() {
        while (owner != null) {
            Vector2 origin = Origin;
            tip = Vector2.MoveTowards(tip, origin, skill.retractSpeed * Time.deltaTime);
            DrawRope();

            if ((tip - origin).sqrMagnitude < 0.01f) yield break;
            yield return null;
        }
    }

    #endregion
    #region 플레이어 조작 넘겨받기

    void TakeOwner() {
        isDrivingOwner = true;
        ownerMove.isExternallyDriven = true;

        // 최대 비행 시간만큼 무적을 걸고, 일찍 닿아도 걷어내지 않는다. ClearInvincible 은 다른 이유로 걸린
        // 무적(낙사 복귀 등)까지 함께 지워 버리기 때문이다. 남는 무적은 길어야 maxFlyTime 이다.
        if (ownerHealth != null) ownerHealth.SetInvincible(skill.maxFlyTime);
    }

    // 조작을 돌려준다. 여러 경로(정상 종료 · 도중 파괴)에서 불려도 한 번만 처리된다.
    void ReleaseOwner(bool bounce) {
        if (!isDrivingOwner) return;
        isDrivingOwner = false;

        if (ownerMove != null) ownerMove.isExternallyDriven = false;

        if (ownerBody != null) {
            // 들이받았으면 튕겨 올라 몬스터 몸에 파묻힌 채 멈추지 않게 한다. 못 닿았으면 날던 속도를 대부분 죽여,
            // 조작을 돌려받은 순간 엉뚱한 방향으로 미끄러지지 않게 한다.
            ownerBody.linearVelocity = bounce
                ? new Vector2(0f, skill.bounceUpSpeed)
                : ownerBody.linearVelocity * 0.3f;
        }

        if (bounce && ownerHealth != null) ownerHealth.SetInvincible(skill.postExecuteInvincible);
    }

    #endregion
    #region 판정

    // 이 구간에서 걸린 대상 중 가장 가까운 것. 피격 범위(Hurtbox)가 있는 콜라이더만 본다 — 몸통 이동용 콜라이더에
    // 걸리면 피격 판정 모양과 어긋난 자리에서 묶인다.
    bool CastTarget(Vector2 from, float distance, out float hitDistance) {
        hitDistance = 0f;

        ContactFilter2D filter = new() { useTriggers = true };
        filter.SetLayerMask(skill.targetLayer);

        int count = Physics2D.CircleCast(from, skill.ropeRadius, direction, filter, castBuffer, distance);
        float nearest = float.MaxValue;

        for (int i = 0; i < count; i++) {
            RaycastHit2D hit = castBuffer[i];
            if (!hit.collider.TryGetComponent(out Hurtbox hurtbox)) continue;

            Health health = hurtbox.OwnerHealth;
            if (health == null || health == ownerHealth || health.IsDead) continue;
            if (hit.distance >= nearest) continue;

            nearest = hit.distance;
            hitHealth = health;
            hitCollider = hit.collider;
        }

        if (hitHealth == null) return false;

        hitDistance = nearest;
        return true;
    }

    // 이 구간에서 줄을 막는 지형까지의 거리. 없으면 float.MaxValue.
    // 통과형 발판(PlatformEffector2D)은 막지 않는다 — 발판 아래에서 위의 몬스터를 걸 수 있어야 한다.
    float CastObstacle(Vector2 from, float distance) {
        ContactFilter2D filter = new() { useTriggers = false };
        filter.SetLayerMask(skill.obstacleLayer);

        int count = Physics2D.Raycast(from, direction, filter, castBuffer, distance);
        float nearest = float.MaxValue;

        for (int i = 0; i < count; i++) {
            RaycastHit2D hit = castBuffer[i];
            if (hit.collider.usedByEffector) continue;
            nearest = Mathf.Min(nearest, hit.distance);
        }

        return nearest;
    }

    bool CanExecute() {
        if (!IsTargetAlive() || hitHealth.executionImmune || hitHealth.MaxHealth <= 0) return false;
        return hitHealth.CurrentHealth <= hitHealth.MaxHealth * data.executeThreshold;
    }

    // Destroy 는 프레임 끝에 처리되므로 IsDead 도 함께 본다.
    bool IsTargetAlive() {
        return hitHealth != null && !hitHealth.IsDead;
    }

    // 피격 콜라이더가 꺼져 있으면(Monster_Wraith 가 사라지는 중 등) bounds 가 맞지 않으므로 몸 위치로 대신한다.
    Vector2 GetTargetCenter() {
        if (hitCollider != null && hitCollider.enabled) return hitCollider.bounds.center;
        return hitHealth != null ? (Vector2)hitHealth.transform.position : tip;
    }

    Vector2 GetClosestTargetPoint(Vector2 point) {
        if (hitCollider != null && hitCollider.enabled) return hitCollider.ClosestPoint(point);
        return GetTargetCenter();
    }

    #endregion
    #region 그리기

    Vector2 Origin => owner != null ? skill.GetRopeOrigin(owner) : tip;

    void DrawRope() {
        line.SetPosition(0, Origin);
        line.SetPosition(1, tip);
    }

    #endregion
}
