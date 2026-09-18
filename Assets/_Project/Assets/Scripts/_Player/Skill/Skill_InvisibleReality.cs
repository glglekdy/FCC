using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 마임의 방어/기믹 스킬. 슬롯 키를 누르고 있는 동안 마우스 위치를 따라가는 투명 벽 설치 미리보기가 뜨고,
// 우클릭으로 90도씩 회전, 좌클릭으로 그 자리에 설치한다. 슬롯 키를 떼는 것 자체는 아무 일도 하지 않는다 —
// 다른 조준형 스킬(칼잡이·저글러)과 달리 "떼는 순간 발동"이 아니라 "좌클릭 순간 발동"이기 때문이다
// (SkillManager도 이 차이를 지원하도록 onSkillUsed 발행 조건을 손봐 뒀다).
// 벽은 SkillInvisibleWall이 스스로 지속시간을 세고 자동으로 사라진다.
//
// **에셋 생성: Create ▸ FCC ▸ Skill ▸ Invisible Reality**
[CreateAssetMenu(fileName = "Skill_InvisibleReality", menuName = "FCC/Skill/Invisible Reality")]
public class Skill_InvisibleReality : SkillBase, IAimableSkill {
    #region 레벨 데이터

    [System.Serializable]
    public struct LevelData {
        public float range;    // 벽의 길이. 기획서상 Lv0·Lv1 모두 1.8로 동일하다.
        public float duration; // 지속시간(초).
        public int maxCount;   // 동시에 설치해 둘 수 있는 최대 개수. 넘으면 가장 오래된 것부터 사라진다.
    }

    #endregion
    #region 인스펙터 변수

    [Header("레벨")]
    // 인덱스 = 레벨 (0: 지속 7초·1개, 1: 지속 14초·3개).
    // 기획서 수치(범위 1.8 고정, 지속 7→14초, 최대 개수 1→3) 그대로.
    // **지금 몇 레벨인지는 여기가 아니라 SkillManager가 정합니다** (SkillBase.runtimeLevel).
    public LevelData[] levels = {
        new() { range = 1.8f, duration = 7f, maxCount = 1 },
        new() { range = 1.8f, duration = 14f, maxCount = 3 },
    };

    public override int MaxLevel => levels != null && levels.Length > 0 ? levels.Length - 1 : 0;

    [Header("벽")]
    public SkillInvisibleWall wallPrefab; // **비트리거 Collider2D(BoxCollider2D) + SpriteRenderer가 붙은 프리팹을 연결하세요.**
    public LayerMask wallLayer; // 벽에 실제로 지정할 레이어. **새로 만든 SkillWall 레이어 하나만 선택하세요.** 여러 개를 고르면 가장 낮은 비트만 쓰인다.
    public float thickness = 0.3f; // 벽의 두께(설치 방향과 수직인 축). 레벨과 무관하게 고정.

    [Header("설치 제한")]
    public float maxPlacementDistance = 10f; // 플레이어로부터 이 거리를 넘어서는 조준할 수 없다 — 넘어가면 이 거리로 당겨서 설치된다.

    [Header("미리보기")]
    public Color previewColor = new(0.8f, 0.95f, 1f, 0.25f); // 옅은 반투명 — "투명 벽"이라는 컨셉을 살리는 정도로만 보이게 한다.

    #endregion
    #region 런타임 변수

    // 현재 설치돼 있는 벽들. maxCount 관리용이라 Cycle of Fate의 currentStack처럼 에셋(스킬 인스턴스)이 직접 들고 있다
    // — 플레이어가 하나뿐인 게임이라 문제없다. null(자연 소멸분)은 새로 설치하기 직전에 정리한다.
    readonly List<SkillInvisibleWall> activeWalls = new();

    float placementRotation; // 우클릭으로 90도씩 누적되는 벽 회전각.
    Vector2 pendingPosition; // 좌클릭 시점에 확정된 설치 위치. Use()가 그대로 읽는다.

    Transform preview; // 설치 미리보기. 씬 전환 등으로 파괴됐을 수 있어 != null로 확인하고 없으면 다시 만든다.
    static Sprite whiteSprite; // 미리보기에 쓸 1x1 흰 텍스처 스프라이트. 스킬 인스턴스가 늘어나도 하나만 있으면 된다.

    #endregion
    #region 조준 (IAimableSkill)

    public void BeginAim(Transform owner) {
        placementRotation = 0f;
        GetPreview().gameObject.SetActive(true);
    }

    public void UpdateAim(Transform owner, Vector2 aimPoint) {
        // 이미 이번 조준에서 좌클릭으로 설치를 마쳤다면(=쿨타임 진입) 더 이상 미리보기를 움직이지 않는다.
        // 슬롯 키를 계속 누르고 있어도 여기서 멈추므로 두 번째 설치가 끼어들지 않는다.
        if (!IsReady()) return;

        pendingPosition = ClampToMaxDistance(owner.position, aimPoint);

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) {
            placementRotation = (placementRotation + 90f) % 360f;
        }

        UpdatePreviewTransform();

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) {
            if (TryUse(owner)) HidePreview();
        }
    }

    public void ReleaseAim(Transform owner, Vector2 aimPoint) {
        // 발동은 좌클릭이 전담한다 — 슬롯 키를 떼는 시점에는 조준만 접는다(TryUse를 부르지 않는다).
        HidePreview();
    }

    public void CancelAim(Transform owner) {
        HidePreview();
    }

    Vector2 ClampToMaxDistance(Vector2 origin, Vector2 aimPoint) {
        Vector2 offset = aimPoint - origin;
        if (offset.magnitude <= maxPlacementDistance) return aimPoint;
        return origin + offset.normalized * maxPlacementDistance;
    }

    #endregion
    #region 발동

    public override void Use(Transform owner) {
        if (wallPrefab == null) {
            Debug.LogWarning($"[{DisplayName}] wallPrefab이 비어 있어 발동할 수 없습니다.", this);
            return;
        }

        LevelData data = GetLevelData();
        MakeRoomForNewWall(data.maxCount);

        SkillInvisibleWall wall = Instantiate(wallPrefab, pendingPosition, Quaternion.Euler(0f, 0f, placementRotation));
        wall.Initialize(data.range, thickness, data.duration, LayerFromMask(wallLayer));
        activeWalls.Add(wall);
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
            new SkillStat(LocalizationText.Resolve(StatArea, "범위"), data.range.ToString("0.0")),
            new SkillStat(LocalizationText.Resolve(StatDuration, "지속"),
                string.Format(LocalizationText.Resolve(StatSecondsFormat, "{0:0.#}초"), data.duration)),
            new SkillStat(LocalizationText.Resolve(StatMaxCount, "최대 개수"), data.maxCount.ToString()),
        };
    }

    // maxCount를 넘기지 않도록, 자연 소멸된 항목을 먼저 정리하고 그래도 넘치면 가장 오래된 것부터 없앤다.
    void MakeRoomForNewWall(int maxCount) {
        activeWalls.RemoveAll(wall => wall == null);

        while (activeWalls.Count >= maxCount && activeWalls.Count > 0) {
            SkillInvisibleWall oldest = activeWalls[0];
            activeWalls.RemoveAt(0);
            if (oldest != null) Destroy(oldest.gameObject);
        }
    }

    // LayerMask는 인스펙터에서 여러 레이어를 고를 수 있는 비트마스크지만, 오브젝트에 실제로 지정할 레이어는
    // 하나뿐이어야 한다. 가장 낮은 비트를 그 레이어로 취급한다. **인스펙터에서 레이어 하나만 선택하세요.**
    static int LayerFromMask(LayerMask mask) {
        int value = mask.value;
        if (value == 0) return 0;

        for (int i = 0; i < 32; i++) {
            if ((value & (1 << i)) != 0) return i;
        }
        return 0;
    }

    #endregion
    #region 미리보기

    Transform GetPreview() {
        if (preview != null) return preview;

        GameObject go = new("Skill_InvisibleReality_Preview");
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = GetWhiteSprite();
        renderer.color = previewColor;
        renderer.sortingOrder = 50; // 몬스터·이펙트보다는 위, UI보다는 아래에 오도록 적당히 높게 잡는다.

        preview = go.transform;
        return preview;
    }

    void UpdatePreviewTransform() {
        LevelData data = GetLevelData();
        Transform t = GetPreview();
        t.position = pendingPosition;
        t.rotation = Quaternion.Euler(0f, 0f, placementRotation);
        t.localScale = new Vector3(thickness, data.range, 1f); // 1x1 흰 스프라이트를 벽 크기만큼 늘려서 쓴다.
        t.gameObject.SetActive(true);
    }

    void HidePreview() {
        if (preview != null) preview.gameObject.SetActive(false);
    }

    static Sprite GetWhiteSprite() {
        if (whiteSprite != null) return whiteSprite;

        Texture2D texture = new(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return whiteSprite;
    }

    #endregion
}
