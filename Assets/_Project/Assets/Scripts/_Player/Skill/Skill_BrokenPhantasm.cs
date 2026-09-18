using System.Collections.Generic;
using UnityEngine;

// 칼잡이의 투사체 스킬. 슬롯 키를 누르고 있으면 마우스 방향으로 조준선이 뜨고, 떼는 순간 그 방향으로
// 칼을 부채꼴로 던져 맞은 대상에게 피해를 준다. IAimableSkill을 구현해 SkillManager가 누름·유지·뗌
// 입력을 이 스킬로 그대로 넘겨준다.
//
// **에셋 생성: Create ▸ FCC ▸ Skill ▸ Broken Phantasm**
[CreateAssetMenu(fileName = "Skill_BrokenPhantasm", menuName = "FCC/Skill/Broken Phantasm")]
public class Skill_BrokenPhantasm : SkillBase, IAimableSkill {
    #region 레벨 데이터

    [System.Serializable]
    public struct LevelData {
        public int damage;
        public float range; // 참고용 사거리 표시. Cycle of Fate와 동일하게 현재 로직에는 직접 쓰이지 않는다.
        public float speed; // 투사체 속도. 기획서 수치(5~7 m/s)는 "즉시 박히는 느낌"이 사라질 만큼 느려 대신 기존 게임 감각을 유지하는 자체 값을 쓴다.
        public int pierceCount; // 이 칼이 관통 가능한 대상 수.
    }

    #endregion
    #region 인스펙터 변수

    [Header("레벨")]
    // 인덱스 = 레벨 (0: 기본, 1: 데미지·사거리 증가, 2: 데미지 최대치 + 2체 관통).
    // 데미지·관통은 기획서 수치(30/42/60, Lv2 2체 관통) 그대로, speed는 기존 게임 감각 유지용 자체 값.
    // **지금 몇 레벨인지는 여기가 아니라 SkillManager가 정합니다** (SkillBase.runtimeLevel).
    public LevelData[] levels = {
        new() { damage = 30, range = 30f, speed = 60f, pierceCount = 1 },
        new() { damage = 42, range = 50f, speed = 72f, pierceCount = 1 },
        new() { damage = 60, range = 50f, speed = 84f, pierceCount = 2 },
    };

    public override int MaxLevel => levels != null && levels.Length > 0 ? levels.Length - 1 : 0;

    [Header("투사체")]
    public SkillProjectile projectilePrefab; // **Kinematic Rigidbody2D + Is Trigger 콜라이더가 붙은 프리팹을 연결하세요.**
    public LayerMask targetLayer; // 칼이 맞는 대상. **몬스터 레이어를 지정하세요.**
    public float projectileLifetime = 0.5f; // 아무것도 못 맞히고 사라지기까지의 시간(벽에 닿으면 이 시간 전에도 바로 사라진다).
    public LayerMask obstacleLayer = 1 << 31; // 이 레이어에 닿으면 그 자리에서 사라진다. **벽·바닥(ground) 레이어를 지정하세요.**

    [Header("발사 형태")]
    public int knifeCount = 1; // 한 번에 던지는 칼 개수. 2개 이상이면 부채꼴로 퍼진다.
    public float spreadAngle = 15f; // 칼 사이의 각도 간격(도).

    [Header("발사 위치")]
    public float forwardOffset = 0.6f; // 조준 방향으로 밀어낼 거리.
    public float heightOffset = 0.5f; // 발사 높이. 플레이어 원점이 발밑이라 조금 올려야 한다.

    [Header("조준선")]
    public Color aimLineColor = new(1f, 1f, 1f, 0.5f);
    public float aimLineWidth = 0.05f;

    [Header("사운드")]
    [FMODUnity.EventRef] public string skillSoundEvent; // event:/SFX/Skill/Broken phantasm

    #endregion
    #region 런타임 변수

    // 마지막으로 조준한 방향. ReleaseAim이 채워 넣고 Use()가 그대로 읽는다.
    Vector2 aimDirection = Vector2.right;

    // 조준선. 씬 전환 등으로 파괴됐을 수 있어 != null로 확인하고 없으면 다시 만든다.
    LineRenderer aimLine;

    #endregion
    #region 조준 (IAimableSkill)

    public void BeginAim(Transform owner) {
        GetAimLine().gameObject.SetActive(true);
    }

    public void UpdateAim(Transform owner, Vector2 aimPoint) {
        LineRenderer line = GetAimLine();
        line.SetPosition(0, owner.position);
        line.SetPosition(1, aimPoint);
        AimLineIndicator.ScrollDashes(line); // 점선이 조준 방향으로 흐르는 것처럼 보이게 한다.
    }

    public void ReleaseAim(Transform owner, Vector2 aimPoint) {
        HideAimLine();

        Vector2 direction = aimPoint - (Vector2)owner.position;
        aimDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : GetFacingDirection(owner);

        TryUse(owner); // 쿨타임 기록은 여기서 SkillBase가 대신 찍는다.
    }

    public void CancelAim(Transform owner) {
        HideAimLine();
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
        if (projectilePrefab == null) {
            Debug.LogWarning($"[{DisplayName}] projectilePrefab이 비어 있어 발동할 수 없습니다.", this);
            return;
        }

        LevelData data = GetLevelData();
        Vector2 origin = (Vector2)owner.position + aimDirection * forwardOffset + Vector2.up * heightOffset;
        Health ownerHealth = owner.GetComponentInParent<Health>();

        if (!string.IsNullOrEmpty(skillSoundEvent)) FMODUnity.RuntimeManager.PlayOneShot(skillSoundEvent, origin);

        // 조준 방향(aimDirection)을 중심으로 칼들을 좌우로 고르게 편다.
        float baseAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        float startAngle = baseAngle - spreadAngle * 0.5f * (knifeCount - 1);

        for (int i = 0; i < knifeCount; i++) {
            float angle = startAngle + spreadAngle * i;
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            SkillProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
            projectile.Launch(direction, targetLayer, data.damage, data.speed, projectileLifetime, ownerHealth, data.pierceCount, obstacleLayer);
        }
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
            new SkillStat(LocalizationText.Resolve(StatDamage, "피해"), data.damage.ToString()),
            new SkillStat(LocalizationText.Resolve(StatRange, "사거리"), data.range.ToString("0.#")),
            new SkillStat(LocalizationText.Resolve(StatPierce, "관통"),
                string.Format(LocalizationText.Resolve(StatPierceValueFormat, "{0}체"), data.pierceCount)),
        };
    }

    #endregion
}
