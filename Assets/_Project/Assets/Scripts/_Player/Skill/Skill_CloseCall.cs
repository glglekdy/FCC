using System.Collections.Generic;
using UnityEngine;

// 곡예사의 공격 스킬. 슬롯 키를 누르고 있으면 마우스 방향으로 사거리만큼 조준선이 뜨고, 떼는 순간 그 방향으로
// 줄을 던진다(IAimableSkill). 줄에 맞은 적은 피해를 입고 잠시 묶인다(IRestrainable).
// 묶은 적의 남은 체력이 처형 기준(최대 체력의 10/20/30%) 이하면, 곡예사가 줄을 타고 날아가 들이받아 즉사시킨다.
// 날아가는 동안은 무적이다.
//
// 이름(Close Call, "아슬아슬한 순간")처럼 "거의 다 잡은 적에게 파고들어 끝낸다"가 이 스킬의 역할이다.
// 칼잡이의 투사체와 겹치지 않도록, 줄 자체의 피해는 작게 두고 구속과 처형에 무게를 실었다.
//
// 에셋(ScriptableObject)은 코루틴을 돌릴 수 없어서 던진 뒤의 과정(뻗기 · 묶기 · 날아가기 · 되감기)은
// 씬에 따로 생기는 SkillCloseCallRope 가 맡는다.
//
// **에셋 생성: Create ▸ FCC ▸ Skill ▸ Close Call**
[CreateAssetMenu(fileName = "Skill_CloseCall", menuName = "FCC/Skill/Close Call")]
public class Skill_CloseCall : SkillBase, IAimableSkill {
    #region 레벨 데이터

    [System.Serializable]
    public struct LevelData {
        public int damage; // 줄이 맞았을 때의 피해. 처형 판정은 이 피해를 준 뒤의 체력으로 한다.
        public float range; // 줄이 뻗는 최대 거리.
        public float restrainDuration; // 묶어 두는 시간(초).
        [Range(0f, 1f)]
        public float executeThreshold; // 남은 체력이 최대 체력의 이 비율 이하면 날아가 처형한다.
    }

    #endregion
    #region 인스펙터 변수

    [Header("레벨")]
    // 인덱스 = 레벨. 처형 기준(10% / 20% / 30%)은 확정값이고, 피해·사거리·구속 시간은 임시값이다.
    // **지금 몇 레벨인지는 여기가 아니라 SkillManager가 정합니다** (SkillBase.runtimeLevel).
    public LevelData[] levels = {
        new() { damage = 10, range = 7f, restrainDuration = 1.2f, executeThreshold = 0.1f },
        new() { damage = 14, range = 8.5f, restrainDuration = 1.6f, executeThreshold = 0.2f },
        new() { damage = 18, range = 10f, restrainDuration = 2f, executeThreshold = 0.3f },
    };

    public override int MaxLevel => levels != null && levels.Length > 0 ? levels.Length - 1 : 0;

    [Header("줄")]
    public LayerMask targetLayer; // 줄이 묶는 대상. **몬스터 레이어를 지정하세요.**
    public LayerMask obstacleLayer = 1 << 31; // 줄이 막히는 지형. **벽·바닥(ground) 레이어를 지정하세요.** 통과형 발판은 막지 않는다.
    public float ropeSpeed = 45f; // 줄이 뻗어 나가는 속도.
    public float retractSpeed = 60f; // 빗나가거나 일이 끝난 뒤 줄을 되감는 속도.
    public float ropeRadius = 0.25f; // 줄 끝의 판정 반경. 너무 얇으면 작은 비행 몬스터를 스쳐 지나간다.
    public float heightOffset = 0.5f; // 줄이 나가는 높이. 플레이어 원점이 발밑이라 조금 올려야 한다.

    [Header("줄 모양")]
    public Color ropeColor = new(0.76f, 0.7f, 0.64f, 1f); // 바랜 삼베색. 전용 줄 스프라이트가 생기기 전까지 쓰는 단색.
    public float ropeWidth = 0.05f;

    [Header("처형 (날아가기)")]
    public float flySpeed = 28f; // 줄을 타고 날아가는 속도.
    public float maxFlyTime = 0.6f; // 이 시간 안에 닿지 못하면(벽에 막히는 등) 처형을 포기하고 조작을 돌려준다.
    public float arriveDistance = 0.6f; // 플레이어 몸 중심이 대상 피격 범위에서 이 거리 안으로 들어오면 들이받은 것으로 본다.
    public float bounceUpSpeed = 7f; // 들이받은 뒤 튕겨 오르는 속도. 몬스터 몸에 파묻힌 채 멈추지 않게 한다.
    public float postExecuteInvincible = 0.25f; // 착지 직후 남겨 두는 무적(초). 날아가 멈춘 순간 옆 몬스터에게 곧바로 맞지 않게.

    [Header("조준선")]
    public Color aimLineColor = new(1f, 1f, 1f, 0.5f);
    public float aimLineWidth = 0.05f;

    #endregion
    #region 런타임 변수

    // 마지막으로 조준한 방향. ReleaseAim이 채워 넣고 Use()가 그대로 읽는다.
    Vector2 aimDirection = Vector2.right;

    // 조준선. 씬 전환 등으로 파괴됐을 수 있어 != null로 확인하고 없으면 다시 만든다.
    LineRenderer aimLine;

    // 지금 나가 있는 줄. 줄이 돌아오기 전에는 다시 던지지 못한다 — 쿨타임을 짧게 조정해도
    // 날아가는 도중 두 번째 줄이 플레이어를 다른 방향으로 끌어당기는 일이 없게 하기 위함이다.
    SkillCloseCallRope activeRope;

    #endregion
    #region 쿨타임

    public override bool IsReady() {
        return base.IsReady() && activeRope == null;
    }

    #endregion
    #region 조준 (IAimableSkill)

    public void BeginAim(Transform owner) {
        GetAimLine().gameObject.SetActive(true);
    }

    // 조준선을 커서까지가 아니라 사거리까지만 그린다. 줄이 어디까지 닿는지가 이 스킬에서 가장 중요한 정보다.
    public void UpdateAim(Transform owner, Vector2 aimPoint) {
        Vector2 origin = GetRopeOrigin(owner);
        Vector2 direction = ComputeAimDirection(owner, aimPoint);

        LineRenderer line = GetAimLine();
        line.SetPosition(0, origin);
        line.SetPosition(1, origin + direction * GetLevelData().range);
        AimLineIndicator.ScrollDashes(line);
    }

    public void ReleaseAim(Transform owner, Vector2 aimPoint) {
        HideAimLine();
        aimDirection = ComputeAimDirection(owner, aimPoint);
        TryUse(owner); // 쿨타임 기록은 여기서 SkillBase가 대신 찍는다.
    }

    public void CancelAim(Transform owner) {
        HideAimLine();
    }

    // 발밑이 아니라 줄이 나가는 높이에서 커서를 겨눠야, 조준선과 실제로 날아가는 줄이 같은 선 위에 놓인다.
    Vector2 ComputeAimDirection(Transform owner, Vector2 aimPoint) {
        Vector2 direction = aimPoint - GetRopeOrigin(owner);
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : GetFacingDirection(owner);
    }

    LineRenderer GetAimLine() {
        if (aimLine == null) aimLine = AimLineIndicator.Create(aimLineColor, aimLineWidth);
        return aimLine;
    }

    void HideAimLine() {
        if (aimLine != null) aimLine.gameObject.SetActive(false);
    }

    #endregion
    #region 발동

    public override void Use(Transform owner) {
        activeRope = SkillCloseCallRope.Spawn(this, owner, aimDirection, GetLevelData());
    }

    // 줄이 뻗어 나가는 자리. SkillCloseCallRope 도 매 프레임 이 값으로 줄의 시작점을 다시 잡는다.
    public Vector2 GetRopeOrigin(Transform owner) {
        return (Vector2)owner.position + Vector2.up * heightOffset;
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
            new SkillStat("처형 기준", $"체력 {Mathf.RoundToInt(data.executeThreshold * 100f)}% 이하"),
            new SkillStat("구속", $"{data.restrainDuration:0.#}초"),
            new SkillStat("피해", data.damage.ToString()),
            new SkillStat("사거리", data.range.ToString("0.#")),
        };
    }

    #endregion
}
