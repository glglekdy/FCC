using System.Collections.Generic;
using UnityEngine;

// 저글러의 기본 공 스킬. 칼잡이처럼 슬롯 키를 누르고 있으면 마우스 방향으로 포물선 궤적 미리보기가 뜨고,
// 떼는 순간 그 방향으로 공을 던진다(IAimableSkill). 던진 공은 물리(중력·반발력)로 바닥에 튕기며 나아간다
// (SkillBouncingProjectile). 공은 항상 터진다 — maxBounces만큼 튕기거나 몬스터에 닿으면 그 자리에서
// 범위 피해를 준다. {폭발하는 공} 스택은 이 폭발을 강화하는 보너스다 — 던질 때마다 쌓이고, 스택이 다 찬
// 상태에서 다시 던지면 이번 폭발의 데미지·반경이 커진다(스택은 소모되고 다시 0부터 쌓인다).
//
// 레벨(0~2)마다 공 크기·데미지·사거리·스택 적립량·폭발 데미지가 달라진다. **기획 문서상 스택/레벨 수치는
// 예시이며 추후 변경 가능성이 매우 높다.** 정식 스킬 업그레이드 시스템(기억 조각 연동)이 붙기 전까지는
// level을 인스펙터에서 직접 조정한다.
//
// **에셋 생성: Create ▸ FCC ▸ Skill ▸ Cycle of Fate**
[CreateAssetMenu(fileName = "Skill_CycleOfFate", menuName = "FCC/Skill/Cycle of Fate")]
public class Skill_CycleOfFate : SkillBase, IAimableSkill {
    #region 레벨 데이터

    [System.Serializable]
    public struct LevelData {
        [Header("공")]
        public int ballCount; // 한 번에 던지는 공 개수. 2개 이상이면 부채꼴로 퍼진다.
        public float ballScale; // 투사체 스프라이트 크기 배수(1이 기본 크기).
        public int damage;
        public float range; // 사거리. 현재는 물리(중력·반발력)로 궤적이 정해져 수치가 직접 쓰이지 않는다 — 추후 UI 표시나 밸런싱 참고용.

        [Header("폭발하는 공 스택")]
        public int stackGainPerUse; // 이 레벨에서 한 번 던질 때 쌓이는 스택 수.

        [Header("폭발")]
        public int explosionDamage; // 이 레벨에서 공이 터질 때의 기본 범위 피해 (스택이 다 차면 배수가 곱해진다).
    }

    #endregion
    #region 인스펙터 변수

    [Header("레벨")]
    // 인덱스 = 레벨 (0: 기본, 1: 사이즈·데미지·사거리 증가, 2: 스택 적립량·공 개수 증가).
    // 기획서의 대략적인 수치를 임시로 채워 뒀다 — 밸런스 확정 전까지는 참고용.
    // **지금 몇 레벨인지는 여기가 아니라 SkillManager가 정합니다** (SkillBase.runtimeLevel).
    public LevelData[] levels = {
        new() { ballCount = 1, ballScale = 1f, damage = 12, range = 6f, stackGainPerUse = 1, explosionDamage = 20 },
        new() { ballCount = 1, ballScale = 1.3f, damage = 18, range = 8f, stackGainPerUse = 1, explosionDamage = 30 },
        new() { ballCount = 2, ballScale = 1.3f, damage = 18, range = 8f, stackGainPerUse = 2, explosionDamage = 45 },
    };

    public override int MaxLevel => levels != null && levels.Length > 0 ? levels.Length - 1 : 0;

    [Header("투사체")]
    public SkillBouncingProjectile projectilePrefab; // **Rigidbody2D + 트리거가 아닌 Collider2D + 반발력 있는 PhysicsMaterial2D가 필요합니다.**
    public LayerMask targetLayer; // 공이 맞는 대상. **몬스터 레이어를 지정하세요.**
    public float projectileSpeed = 16f; // 던지는 순간의 초기 속력. 이후 움직임은 중력·반발력에 맡긴다.
    public float maxAirTime = 3f; // 아무것도 못 맞히고 튕기기만 할 때 강제로 사라지는 시간(초). maxBounces에 못 미쳐도 이 시간이 지나면 사라진다.

    [Header("폭발")]
    public float explosionRadius = 2.2f; // 공이 터질 때의 기본 범위 피해 반경. maxBounces·몬스터 접촉 어느 쪽으로 터지든 동일하게 적용된다.

    [Header("폭발하는 공 스택 (치명타 보너스)")]
    public int explodeStackThreshold = 2; // 이 값 이상 쌓인 상태로 던지면, 이번 폭발이 강화(치명타)된다.
    public float explosionCritDamageMultiplier = 1.8f; // 치명타 폭발의 데미지 배수.
    public float explosionCritRadiusMultiplier = 1.3f; // 치명타 폭발의 반경 배수.

    [Header("발사 형태")]
    public float spreadAngle = 20f; // 공 사이의 각도 간격(도). ballCount가 1이면 쓰이지 않는다.

    [Header("발사 위치")]
    public float forwardOffset = 0.6f; // 조준 방향으로 밀어낼 거리.
    public float heightOffset = 0.5f; // 발사 높이. 플레이어 원점이 발밑이라 조금 올려야 한다.

    [Header("조준 궤적 미리보기")]
    public Color trajectoryColor = new(1f, 1f, 1f, 0.5f);
    public float trajectoryWidth = 0.05f;
    public int trajectoryPointCount = 20; // 포물선을 몇 개의 점으로 그릴지. 많을수록 부드럽다.

    #endregion
    #region 런타임 변수

    // 현재 쌓인 폭발 스택. 도메인 리로드/재생마다 0으로 되돌아간다 (직렬화되지 않는 필드라 lastUsedTime과
    // 달리 별도 리셋 코드가 필요 없다).
    int currentStack;

    // 마지막으로 조준한 방향. ReleaseAim이 채워 넣고 Use()가 그대로 읽는다.
    Vector2 aimDirection = Vector2.right;

    // 궤적 미리보기 선. 씬 전환 등으로 파괴됐을 수 있어 != null로 확인하고 없으면 다시 만든다.
    LineRenderer trajectoryLine;

    #endregion
    #region 조준 (IAimableSkill)

    public void BeginAim(Transform owner) {
        GetTrajectoryLine().gameObject.SetActive(true);
    }

    public void UpdateAim(Transform owner, Vector2 aimPoint) {
        Vector2 direction = ComputeAimDirection(owner, aimPoint);
        Vector2 origin = GetLaunchOrigin(owner, direction);
        DrawTrajectory(origin, direction * projectileSpeed);
    }

    public void ReleaseAim(Transform owner, Vector2 aimPoint) {
        HideTrajectory();
        aimDirection = ComputeAimDirection(owner, aimPoint);
        TryUse(owner); // 쿨타임 기록은 여기서 SkillBase가 대신 찍는다.
    }

    public void CancelAim(Transform owner) {
        HideTrajectory();
    }

    Vector2 ComputeAimDirection(Transform owner, Vector2 aimPoint) {
        Vector2 direction = aimPoint - (Vector2)owner.position;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : GetFacingDirection(owner);
    }

    LineRenderer GetTrajectoryLine() {
        if (trajectoryLine == null) trajectoryLine = AimLineIndicator.Create(trajectoryColor, trajectoryWidth, 55, Mathf.Max(2, trajectoryPointCount));
        return trajectoryLine;
    }

    void HideTrajectory() {
        if (trajectoryLine != null) trajectoryLine.gameObject.SetActive(false);
    }

    // 실제 발사체와 같은 속도·중력으로 포물선을 미리 계산해서 그린다. 첫 번째 튕김 전(발사 높이로 돌아오는
    // 시점)까지만 보여줘도 던지는 감각을 파악하기에 충분하다.
    void DrawTrajectory(Vector2 origin, Vector2 velocity) {
        LineRenderer line = GetTrajectoryLine();
        Vector2 gravity = Physics2D.gravity * GetProjectileGravityScale();
        float duration = EstimateArcDuration(velocity.y, gravity.y);

        int pointCount = Mathf.Max(2, trajectoryPointCount);
        Vector3[] points = new Vector3[pointCount];
        for (int i = 0; i < pointCount; i++) {
            float t = duration * i / (pointCount - 1);
            points[i] = origin + velocity * t + 0.5f * gravity * (t * t);
        }

        line.positionCount = pointCount;
        line.SetPositions(points);
        AimLineIndicator.ScrollDashes(line); // 점선이 던지는 방향으로 흐르는 것처럼 보이게 한다.
    }

    float GetProjectileGravityScale() {
        if (projectilePrefab == null) return 1f;
        Rigidbody2D body = projectilePrefab.GetComponent<Rigidbody2D>();
        return body != null ? body.gravityScale : 1f;
    }

    // 위로 던진 만큼 다시 내려와 발사 높이와 같아지는 시점(t = -2*vy/gy)을 궤적 길이로 쓴다.
    float EstimateArcDuration(float verticalVelocity, float gravityY) {
        if (gravityY >= 0f) return 0.4f; // 중력 설정이 이상한 경우를 위한 안전장치.

        float t = verticalVelocity > 0f ? -2f * verticalVelocity / gravityY : 0.4f;
        return Mathf.Clamp(t, 0.2f, maxAirTime);
    }

    #endregion
    #region 발동

    public override void Use(Transform owner) {
        if (projectilePrefab == null) {
            Debug.LogWarning($"[{DisplayName}] projectilePrefab이 비어 있어 발동할 수 없습니다.", this);
            return;
        }

        LevelData data = GetLevelData();
        bool isCritical = currentStack >= explodeStackThreshold; // 이번 폭발이 강화될지는 "던지기 전" 스택으로 정한다.

        ThrowBalls(owner, data, isCritical);

        currentStack = isCritical ? 0 : Mathf.Min(currentStack + data.stackGainPerUse, explodeStackThreshold);
    }

    LevelData GetLevelData() {
        if (levels == null || levels.Length == 0) return default;
        return levels[Mathf.Clamp(LevelIndex, 0, levels.Length - 1)];
    }

    // 정비 화면 비교표용. 레벨이 올라도 줄 순서·개수가 달라지면 안 된다.
    public override IReadOnlyList<SkillStat> DescribeLevel(int level) {
        if (levels == null || levels.Length == 0) return System.Array.Empty<SkillStat>();

        LevelData data = levels[Mathf.Clamp(level, 0, levels.Length - 1)];
        return new[] {
            new SkillStat(LocalizationText.Resolve(StatBallCount, "공 개수"), data.ballCount.ToString()),
            new SkillStat(LocalizationText.Resolve(StatBallSize, "공 크기"), data.ballScale.ToString("0.0")),
            new SkillStat(LocalizationText.Resolve(StatDamage, "피해"), data.damage.ToString()),
            new SkillStat(LocalizationText.Resolve(StatStackGain, "스택 적립"), data.stackGainPerUse.ToString()),
            new SkillStat(LocalizationText.Resolve(StatExplosionDamage, "폭발 피해"), data.explosionDamage.ToString()),
        };
    }

    Vector2 GetLaunchOrigin(Transform owner, Vector2 direction) {
        return (Vector2)owner.position + direction * forwardOffset + Vector2.up * heightOffset;
    }

    void ThrowBalls(Transform owner, LevelData data, bool isCritical) {
        Vector2 origin = GetLaunchOrigin(owner, aimDirection);
        Health ownerHealth = owner.GetComponentInParent<Health>();

        // 공은 항상 터진다 — 스택 보너스(isCritical)는 이번 폭발의 데미지·반경만 키운다.
        SkillProjectileExplosion explosion = new() {
            radius = isCritical ? explosionRadius * explosionCritRadiusMultiplier : explosionRadius,
            damage = isCritical ? Mathf.RoundToInt(data.explosionDamage * explosionCritDamageMultiplier) : data.explosionDamage,
            layer = targetLayer
        };

        // 조준 방향(aimDirection)을 중심으로 공들을 좌우로 고르게 편다.
        float baseAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        float startAngle = baseAngle - spreadAngle * 0.5f * (data.ballCount - 1);

        for (int i = 0; i < data.ballCount; i++) {
            float angle = startAngle + spreadAngle * i;
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            SkillBouncingProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
            projectile.transform.localScale = Vector3.one * data.ballScale;
            projectile.Launch(direction * projectileSpeed, targetLayer, data.damage, maxAirTime, ownerHealth, explosion);
        }
    }

    #endregion
}
