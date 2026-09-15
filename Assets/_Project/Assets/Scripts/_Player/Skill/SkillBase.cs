using System.Collections.Generic;
using UnityEngine;

// 모든 스킬이 상속받는 공통 뼈대.
// 스킬 데이터를 씬이 아니라 에셋(ScriptableObject)으로 둔 이유는, 거울 정비 UI가 "해금된 스킬 목록"을
// 그대로 뿌려야 하고 세이브에도 id로만 기록해야 하기 때문이다. 씬 컴포넌트에 적어두면 씬마다 흩어진다.
//
// **파생 클래스는 [CreateAssetMenu]를 붙이고 Use()만 구현하면 됩니다.**
// 쿨타임 기록은 TryUse()가 대신 찍어주므로 Use() 안에서 lastUsedTime을 건드리지 말 것.
public abstract class SkillBase : ScriptableObject {
    #region 인스펙터 변수

    [Header("식별")]
    // 세이브·장착 슬롯 복원에 쓰는 고유 id. **에셋마다 겹치지 않게 지으세요.** 비워두면 에셋 이름을 대신 쓴다.
    public string skillID;
    public string skillName = "이름 없는 스킬"; // 슬롯·목록 UI에 표시되는 이름.
    public string roleLabel; // 정비 화면에서 이름 아래 붙는 한 줄. "칼잡이 · 투사체" 처럼 누구의 어떤 기술인지.
    [TextArea(2, 4)]
    public string description; // 정비 UI에서 보여줄 설명문.
    public Sprite icon; // 슬롯·목록 UI에 표시할 아이콘.

    [Header("쿨타임")]
    public float cooldown = 3f; // 재사용 대기 시간(초). 0이면 쿨타임 없이 연발된다.

    [Header("레벨")]
    // 새 게임을 시작했을 때의 강화 레벨. 진행 중 레벨은 SkillManager가 들고 세이브 파일에 기록되므로,
    // 이 값은 시작값·에디터 테스트용으로만 쓰인다. 강화 단계가 없는 스킬은 0으로 두면 된다.
    public int startingLevel = 0;

    #endregion
    #region 런타임 변수

    // 마지막으로 사용한 시각. ScriptableObject는 에셋 하나를 공유하므로 이 값도 공유된다.
    // 플레이어가 하나뿐인 게임이라 그 자체는 문제가 없지만, 에디터에서는 플레이를 멈춰도 값이 남아
    // 다음 플레이가 쿨타임이 걸린 채로 시작한다. 그래서 OnEnable과 SkillManager.Awake에서 씻어낸다.
    [HideInInspector]
    public float lastUsedTime = NeverUsed;

    // 지금 적용 중인 강화 레벨. lastUsedTime과 같은 이유로 여기 둔다 — 스킬 데이터는 에셋 하나를
    // 공유하므로 이 값도 공유되고, 에디터에서는 플레이를 멈춰도 남는다. 진짜 소유자는 SkillManager이며
    // 해금·강화·환불·세이브 복원 때마다 이 값을 다시 넣어준다.
    [HideInInspector]
    public int runtimeLevel;

    // 0으로 두면 게임 시작 직후 cooldown 초 동안 못 쓰는 것으로 계산된다 (Time.time이 0에서 출발하므로).
    const float NeverUsed = -9999f;

    // skillID를 깜빡해도 최소한 에셋 이름으로는 구분되게 한다 (SaveMirror.mirrorId와 같은 방식).
    public string SkillId => string.IsNullOrEmpty(skillID) ? name : skillID;

    public string DisplayName => string.IsNullOrEmpty(skillName) ? name : skillName;

    #endregion
    #region 유니티 라이프 사이클

    // 에셋이 로드될 때(에디터에서는 도메인 리로드마다) 초기화. 직전 플레이의 쿨타임·레벨을 물고 들어오지 않도록.
    // SkillManager.Awake가 그 뒤에 실제 레벨을 다시 넣어준다.
    void OnEnable() {
        ResetCooldown();
        runtimeLevel = startingLevel;
    }

    #endregion
    #region 레벨

    // 이 스킬이 올라갈 수 있는 최고 레벨. 레벨 테이블을 가진 파생 클래스가 levels.Length - 1로 덮어쓴다.
    // 기본값 0 = 강화 단계가 없는 스킬(Pure Dream 등)이라는 뜻이다.
    public virtual int MaxLevel => 0;

    // 레벨 테이블을 짚을 때 쓰는 안전한 인덱스. 세이브가 깨져 범위 밖 값이 들어와도 테이블 밖을 읽지 않는다.
    protected int LevelIndex => Mathf.Clamp(runtimeLevel, 0, MaxLevel);

    // 정비 화면에 한 줄로 늘어놓을 수치 하나.
    public readonly struct SkillStat {
        public readonly string label;
        public readonly string value;

        public SkillStat(string label, string value) {
            this.label = label;
            this.value = value;
        }
    }

    // 지정한 레벨의 수치 목록. 정비 화면이 현재 레벨과 다음 레벨을 나란히 놓고 무엇이 좋아지는지 보여준다.
    //
    // 레벨 수치 구조(LevelData)는 스킬마다 필드가 달라 공통 타입으로 묶을 수 없다. 그래서 화면에 필요한
    // "이름 · 값" 문자열만 파생 클래스가 만들어 넘기게 했다. 강화 단계가 없는 스킬은 그대로 두면 되고,
    // 그러면 정비 화면에도 비교표가 뜨지 않는다.
    //
    // **두 레벨의 목록은 순서와 개수를 맞춰야 합니다** — 정비 화면이 같은 인덱스끼리 짝지어 비교한다.
    public virtual IReadOnlyList<SkillStat> DescribeLevel(int level) {
        return System.Array.Empty<SkillStat>();
    }

    #endregion
    #region 쿨타임

    public virtual bool IsReady() {
        return Time.time >= lastUsedTime + cooldown;
    }

    // 남은 쿨타임(초). UI에 숫자로 띄우는 용도라 음수로 내려가지 않게 잘라준다.
    public virtual float GetRemainingCooldown() {
        return Mathf.Max(0f, lastUsedTime + cooldown - Time.time);
    }

    // 쿨타임 게이지용 0~1 진행도. 1이면 사용 가능.
    public virtual float GetCooldownProgress() {
        if (cooldown <= 0f) return 1f; // 0으로 나누는 것을 막고, 쿨타임이 없는 스킬은 항상 가득 찬 상태로 보이게 한다.
        return Mathf.Clamp01(1f - GetRemainingCooldown() / cooldown);
    }

    public void ResetCooldown() {
        lastUsedTime = NeverUsed;
    }

    #endregion
    #region 발동

    // 쿨타임 검사 → 발동 → 사용 시각 기록까지 한 번에. SkillManager는 이것만 부른다.
    // 발동에 성공했으면 true.
    public bool TryUse(Transform owner) {
        if (owner == null || !IsReady()) return false;

        // Use()보다 먼저 찍는다. 파생 클래스가 스스로 기록하는 것을 잊거나 내부에서 예외가 나도
        // 쿨타임 없이 무한 연발되는 사고가 나지 않도록.
        lastUsedTime = Time.time;
        Use(owner);
        return true;
    }

    // 실제 스킬 로직. owner는 스킬을 쓴 주체(플레이어)의 트랜스폼.
    // 쿨타임 검사는 TryUse가 이미 끝냈으므로 여기서는 효과만 구현하면 된다.
    public abstract void Use(Transform owner);

    #endregion
    #region 파생 클래스용 도우미

    // 주체가 바라보는 방향(오른쪽이면 +1, 왼쪽이면 -1).
    // Player_move가 회전 방식(useFacingRotation)과 스케일 반전 방식을 둘 다 지원해서 한쪽만 봐서는 알 수 없다.
    // 회전으로 뒤집으면 right.x가 음수가 되고, 스케일로 뒤집으면 lossyScale.x가 음수가 되므로 둘을 곱해 판단한다.
    protected static float GetFacingSign(Transform owner) {
        float sign = owner.lossyScale.x < 0f ? -1f : 1f;
        if (owner.right.x < 0f) sign = -sign;
        return sign;
    }

    // 주체가 바라보는 방향 벡터. 판정 중심을 전방으로 밀어낼 때 쓴다.
    protected static Vector2 GetFacingDirection(Transform owner) {
        return new Vector2(GetFacingSign(owner), 0f);
    }

    #endregion
}
