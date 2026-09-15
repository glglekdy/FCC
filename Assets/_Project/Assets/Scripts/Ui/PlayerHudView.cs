using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 화면 좌측 상단에 상시 노출되는 인게임 플레이어 HUD. 자아 게이지(체력) · 장착 스킬 3칸과 쿨타임 · 기억 조각 수를 그린다.
//
// 생김새는 전부 프리팹 Prefabs/UI/PlayerHud.prefab 에 있고 이 스크립트는 값 갱신만 한다.
// 처음부터 다시 찍어내려면 에디터 메뉴 Tools ▸ FCC ▸ Build Player HUD Prefab.
//
// Health 에는 회복(SetHealth·RestoreFull) 이벤트가 없어 매 프레임 CurrentHealth 를 읽어 그린다
// (캐릭터 머리 위 HealthBar 와 같은 방식). OnDamaged 는 잔상 바가 뒤따라 줄기 시작하는
// 시점을 잡는 데만 쓴다.
public class PlayerHudView : MonoBehaviour {
    #region 인스펙터 변수

    [Header("연결 — 프리팹이 채워 둔 값입니다")]
    public Image fillImage;        // type=Filled, Horizontal. 자아 게이지 본체.
    public Image delayedFillImage; // 깎인 양을 잠깐 남기며 뒤따라 줄어드는 잔상 바. 비워도 된다.
    public TMP_Text valueLabel;    // "72 / 100" 표기. 비워도 된다.

    [Header("연결 — 스킬 · 기억 조각 (비워도 된다)")]
    // 슬롯 0·1·2 순서. **배열 순서가 그대로 스킬 키 1 · 2 · 3 입니다.**
    public HudSkillSlotView[] skillSlots;
    public TMP_Text shardValueLabel; // 보유한 기억 조각 수.

    [Header("대상 — 비우면 Player 태그로 찾습니다")]
    // 특정 Health 를 강제하고 싶을 때만 채운다. 평소에는 비워 두면 씬의 플레이어를 자동으로 문다.
    public Health overrideTarget;
    public string playerTag = "Player";

    [Header("잔상 바")]
    public float drainDelay = 0.25f; // 피격 후 잔상이 줄기 시작하기까지의 대기.
    public float drainSpeed = 0.8f;  // 잔상이 줄어드는 속도 (비율/초).

    #endregion
    #region 런타임 변수

    Health health;         // 실제로 값을 읽는 대상. 사망으로 파괴되면 다시 찾는다.
    SkillManager skills;   // 장착 스킬과 쿨타임을 읽는 대상. 플레이어와 함께 찾는다.
    Player_MemoryShardInventory shards;
    int shownShards = -1;  // 마지막으로 적은 조각 수. 같으면 문자열을 다시 만들지 않는다.
    float delayedRatio = 1f;
    float drainTimer;

    #endregion
    #region 유니티 라이프 사이클

    // 다른 오브젝트의 Awake 보다 OnEnable 이 먼저 돌 수 있어 플레이어가 아직 없을 수 있다.
    // 모든 Awake 가 끝난 뒤 보장되는 Start 에서 대상을 물고 초기 표시를 맞춘다.
    void Start() {
        if (!ValidateReferences()) {
            enabled = false;
            return;
        }
        Acquire();
        SnapToCurrent();
    }

    void OnDestroy() {
        // 파괴된 뒤에도 C# 참조가 남을 수 있어 ?. 대신 != null 로 Unity 의 == 오버로드를 탄다.
        if (health != null) health.OnDamaged -= HandleDamaged;
    }

    // 히트스톱 중에도 게이지가 멈추지 않아야 하므로 아래 연출은 전부 unscaledDeltaTime 기준.
    void LateUpdate() {
        if (health == null) Acquire(); // 사망 등으로 대상이 사라지면 재획득을 시도한다.
        if (health == null) return;

        float ratio = Ratio();
        fillImage.fillAmount = ratio;
        UpdateDelayed(ratio);
        UpdateLabel();
        UpdateSkills();
        UpdateShards();
    }

    #endregion
    #region 대상 획득

    void Acquire() {
        Health found = overrideTarget;
        if (found == null && !string.IsNullOrEmpty(playerTag)) {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null) found = player.GetComponentInChildren<Health>();
        }
        if (found == health) return;

        if (health != null) health.OnDamaged -= HandleDamaged;
        health = found;
        if (health != null) health.OnDamaged += HandleDamaged;

        // 스킬 · 조각은 체력과 같은 플레이어에 붙어 있다. 대상을 바꿀 때 함께 다시 문다.
        skills = health != null ? health.GetComponentInParent<SkillManager>() : null;
        if (skills == null && health != null) skills = health.GetComponentInChildren<SkillManager>();
        shards = health != null ? health.GetComponentInParent<Player_MemoryShardInventory>() : null;
        if (shards == null && health != null) shards = health.GetComponentInChildren<Player_MemoryShardInventory>();
        shownShards = -1;
    }

    #endregion
    #region 표시 갱신

    float Ratio() {
        return health.MaxHealth > 0 ? Mathf.Clamp01((float)health.CurrentHealth / health.MaxHealth) : 0f;
    }

    // 세이브 복원·거울 회복처럼 큰 폭으로 값이 튀는 경우 잔상까지 즉시 맞춰 둔다.
    void SnapToCurrent() {
        if (health == null) return;

        float ratio = Ratio();
        fillImage.fillAmount = ratio;
        delayedRatio = ratio;
        if (delayedFillImage != null) delayedFillImage.fillAmount = ratio;
        UpdateLabel();
    }

    // 피격 시 잔상은 곧바로 따라가지 않고 drainDelay 만큼 멈췄다가 천천히 줄어든다.
    // 얼마나 깎였는지 눈에 남기기 위함이다. 회복은 UpdateDelayed 에서 즉시 따라간다.
    void HandleDamaged(int damage, Vector2 sourcePosition) {
        drainTimer = drainDelay;
    }

    void UpdateDelayed(float ratio) {
        if (delayedFillImage == null) return;

        if (delayedRatio < ratio) {
            delayedRatio = ratio; // 회복했을 때는 잔상이 즉시 붙는다.
        }
        else if (drainTimer > 0f) {
            drainTimer -= Time.unscaledDeltaTime;
        }
        else {
            delayedRatio = Mathf.MoveTowards(delayedRatio, ratio, drainSpeed * Time.unscaledDeltaTime);
        }

        delayedFillImage.fillAmount = delayedRatio;
    }

    void UpdateLabel() {
        if (valueLabel == null || health == null) return;
        valueLabel.text = $"{Mathf.Max(0, health.CurrentHealth)} / {health.MaxHealth}";
    }

    // 장착은 거울 정비 · 스토리 해금으로만 바뀌지만, 이벤트를 구독하면 씬 전환 때마다 풀고 다시 걸어야 해서
    // 매 프레임 슬롯 3개만 읽는다. HudSkillSlotView 가 바뀐 경우에만 다시 그리므로 비용은 거의 없다.
    void UpdateSkills() {
        if (skillSlots == null || skills == null) return;

        for (int i = 0; i < skillSlots.Length; i++) {
            if (skillSlots[i] == null) continue;
            skillSlots[i].Set(skills.GetSkillInSlot(i));
            skillSlots[i].Tick();
        }
    }

    void UpdateShards() {
        if (shardValueLabel == null || shards == null) return;
        if (shards.Count == shownShards) return;

        shownShards = shards.Count;
        shardValueLabel.text = shownShards.ToString();
    }

    #endregion
    #region 검사

    bool ValidateReferences() {
        List<string> missing = new();
        if (fillImage == null) missing.Add(nameof(fillImage));

        if (missing.Count == 0) return true;

        Debug.LogError($"[PlayerHudView] 프리팹 연결이 비어 있어 HUD 를 그릴 수 없습니다 — {string.Join(", ", missing)}. " +
            "Prefabs/UI/PlayerHud 프리팹을 씬에 놓으세요. " +
            "없으면 Tools ▸ FCC ▸ Build Player HUD Prefab 으로 만들 수 있습니다.", this);
        return false;
    }

    #endregion
}
