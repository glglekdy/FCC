using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 해금된 스킬 중 최대 3개를 슬롯에 장착하고, 입력에 맞춰 발동시킨다.
// 기획서상 장착 수는 3개 고정이고 교체는 거울(정비하기)에서 한다.
//
// **플레이어 오브젝트에 붙이세요.** 스킬이 owner로 받는 트랜스폼이 이 컴포넌트가 붙은 오브젝트다.
public class SkillManager : MonoBehaviour {
    // 기획서상 최대 장착 수. 배열·UI·세이브가 전부 이 값을 따라가야 하므로 상수로 둔다.
    public const int SlotCount = 3;

    #region 인스펙터 변수

    [Header("보유 스킬")]
    // 해금된 스킬 에셋 목록. 거울 정비 UI가 이 목록을 장착 후보로 뿌린다.
    // 해금은 스토리 진행으로만 일어난다 — 기억 조각은 해금이 아니라 강화에만 쓴다.
    public List<SkillBase> unlockedSkills = new();

    [Header("전체 스킬 (세이브 복원용)")]
    // 게임에 존재하는 모든 스킬 에셋. 세이브에는 id만 적히므로, 불러올 때 id를 다시 에셋으로 되돌리려면
    // 아직 해금되지 않은 것까지 전부 알고 있어야 한다.
    // **새 스킬 에셋을 만들면 여기에도 추가하세요.** 빠뜨리면 그 스킬은 세이브에서 복원되지 않는다.
    public List<SkillBase> allSkills = new();

    [Header("장착 슬롯")]
    // 슬롯 0·1·2. 인스펙터에 미리 넣어두면 그게 시작 장착 상태가 된다.
    // **크기는 3으로 고정입니다.** 어긋나게 바꿔도 Awake에서 3개로 맞춘다.
    public SkillBase[] equippedSkills = new SkillBase[SlotCount];

    [Header("입력")]
    // 슬롯 0·1·2를 발동시킬 입력 액션 이름. 플레이어의 PlayerInput(Client.inputactions ▸ Player)에서 찾는다.
    // 키보드 Q · W · E / 패드 LB · RB · RT 에 묶여 있다. 예전에는 Q · W · E 를 직접 읽었는데, 3번이 상호작용(당시 E)과
    // 겹쳐 거울 앞에서 스킬이 같이 나갔다. 그래서 상호작용은 F 로 옮겼다. 액션으로 두면 설정창의 키 재지정도 이 값을 그대로 따라온다.
    public string[] slotActionNames = { "Skill1", "Skill2", "Skill3" };

    [Header("강화 비용")]
    // 강화 비용 = max(하한, 그때 보유한 조각 × 비율). 인덱스 = 강화 단계(0: Lv0→1, 1: Lv1→2).
    //
    // 고정값이 아니라 보유량 비율로 두는 이유는 "모아둘수록 비싸진다"는 압박을 주기 위함이다.
    // 다만 비율만 쓰면 조각 1개만 있어도 강화가 되어 "조각이 모자라 못 올린다"는 상황이 사라지므로,
    // 단계별 하한을 함께 둔다. 초반(가난할 때)에는 하한이, 후반에는 비율이 값을 정한다.
    public float[] upgradePercents = { 0.10f, 0.20f };
    public int[] upgradeMinimums = { 3, 5 };

    [Header("해금")]
    // 스토리에서 새 스킬을 되찾았을 때(UnlockFromStory)만 쓰인다. 세이브 복원·시작 장착은 조용히 해금한다.
    public bool autoEquipOnUnlock = true; // 빈 슬롯이 있으면 바로 끼운다. 거울까지 가기 전에 써 봐야 보상으로 느껴진다.
    public bool announceUnlock = true; // 스킬 이름을 화면 가운데 알림으로 띄운다 (SkillUnlockView, Enter 로 닫는다).

    [Header("디버그")]
    public bool logCooldown = true; // 쿨타임 중에 눌렀을 때 남은 시간을 콘솔에 찍는다. 감각 조정용이라 빌드에선 꺼도 된다.

    #endregion
    #region 이벤트

    // (슬롯 번호, 장착된 스킬) — 해제한 경우 skill이 null로 온다. 정비 UI가 구독해 슬롯 아이콘을 갱신한다.
    public event Action<int, SkillBase> onSkillEquipped;

    // (슬롯 번호, 발동한 스킬) — 쿨타임 게이지 UI가 구독한다.
    public event Action<int, SkillBase> onSkillUsed;

    // (스킬, 바뀐 레벨) — 강화·환불·세이브 복원으로 레벨이 달라졌을 때. 정비 UI가 구독해 수치를 다시 그린다.
    public event Action<SkillBase, int> onSkillLeveled;

    // 스토리에서 새 스킬을 되찾았을 때. 세이브 복원처럼 조용히 늘어난 경우에는 오지 않는다.
    public event Action<SkillBase> onSkillUnlocked;

    #endregion
    #region 컴포넌트 변수

    Player_move playerMove; // 대사·컷씬 중에는 스킬도 막아야 해서 이동 잠금 상태를 본다.
    Player_MemoryShardInventory shards; // 강화 비용을 내고 환불을 돌려받는 지갑.
    PlayerInput playerInput; // 슬롯 입력 액션을 꺼내 올 곳. PlayerInput 이 쓰는 바로 그 액션이라 켜고 끄는 상태가 맞는다.
    readonly InputAction[] slotActions = new InputAction[SlotCount];
    bool warnedMissingActions; // 액션을 못 찾았다는 경고를 한 번만 찍는다.

    // 스킬 id → 지금 레벨. 스킬 에셋(SkillBase.runtimeLevel)에도 같은 값을 넣어주지만, 진짜 소유자는 여기다.
    // 에셋에만 두면 여러 세이브 슬롯·새 게임 사이에 값이 새어 나간다.
    readonly Dictionary<string, int> skillLevels = new();

    // 스킬 id → 강화 단계별로 실제 지불한 조각 수. 환불은 이 값을 그대로 돌려준다.
    // 환불 시점의 보유량으로 다시 계산하면, 조각이 적을 때 강화해 두고 많아진 뒤 되돌려
    // 차액을 버는 무한 증식이 생긴다.
    readonly Dictionary<string, List<int>> paidShards = new();

    // 슬롯별 조준 중 여부. IAimableSkill(마우스 조준형 스킬)에만 쓰인다 — 누른 순간 true, 뗀 순간 false.
    readonly bool[] isAimingSlot = new bool[SlotCount];

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        playerMove = GetComponentInParent<Player_move>();
        shards = GetComponentInParent<Player_MemoryShardInventory>();
        playerInput = GetComponentInParent<PlayerInput>();

        // 인스펙터에서 배열 크기를 잘못 건드려도 슬롯 수가 어긋나지 않게 맞춰 둔다.
        if (equippedSkills == null || equippedSkills.Length != SlotCount) {
            Array.Resize(ref equippedSkills, SlotCount);
        }

        // 인스펙터로 미리 끼워둔 스킬은 해금 목록에도 넣어준다.
        // 안 그러면 정비 UI에서 뺐다가 다시 끼울 수 없고, EquipSkill의 해금 검사에도 걸린다.
        for (int i = 0; i < SlotCount; i++) {
            if (equippedSkills[i] != null) UnlockSkill(equippedSkills[i]);
        }

        // 쿨타임이 ScriptableObject 에셋에 기록되는 구조라, 에디터에서 플레이를 다시 켜면 직전 판의 값이 남아 있다.
        ResetAllCooldowns();

        // 레벨도 같은 이유로 에셋에 남으므로, 여기서 시작 레벨을 다시 심어 새 판을 깨끗하게 시작한다.
        // 세이브를 불러오면 RestoreState가 이 값을 덮어쓴다.
        SeedStartingLevels();
    }

    void Update() {
        if (!ResolveSlotActions()) return;

        if (IsInputBlocked()) {
            CancelAllAiming(); // 조준 도중 대사·컷씬이 끼어들면 조준선이 화면에 남지 않도록 정리한다.
            return;
        }

        for (int i = 0; i < SlotCount; i++) {
            HandleSlotInput(i);
        }
    }

    #endregion
    #region 입력 처리

    // 대사·컷씬 중에는 스킬을 막는다. PlayerInteractor가 상호작용을 막는 것과 같은 기준.
    // Close Call 로 줄을 타고 날아가는 중에도 막는다 — 날아가는 도중 조준을 시작하면 조준선이 궤적과 엉킨다.
    // 일시정지(timeScale 0) 중에도 막는다 — 설정창이 Q/E 로 탭을 넘기고 W 로 줄을 올리므로, 안 막으면 그때마다 스킬이 함께 나간다.
    // 히트스톱은 0 이 아니라 아주 작은 값(HitFeedback.hitStopTimeScale)이라 여기에 걸리지 않는다.
    bool IsInputBlocked() {
        if (Time.timeScale == 0f) return true;
        return playerMove != null && (playerMove.isMovementLocked || playerMove.isExternallyDriven);
    }

    // PlayerInput 은 자기 Awake/OnEnable 에서 액션을 준비하므로, 순서가 어긋나도 되도록 처음 쓸 때 찾는다.
    bool ResolveSlotActions() {
        if (slotActions[0] != null) return true;
        if (playerInput == null || playerInput.actions == null) {
            WarnMissingActions("플레이어에 PlayerInput 이 없습니다");
            return false;
        }

        // 설정에서 바꾼 키를 이 에셋에도 걸어야 한다. 씬이 열릴 때 PlayerInput.all 로도 찾지만,
        // 플레이어가 나중에 생성되는 경우(던전 입장 등)를 위해 쓰는 쪽에서도 한 번 알린다.
        InputBindings.Register(playerInput.actions);

        for (int i = 0; i < SlotCount; i++) {
            string actionName = slotActionNames != null && i < slotActionNames.Length ? slotActionNames[i] : null;
            slotActions[i] = string.IsNullOrEmpty(actionName) ? null : playerInput.actions.FindAction(actionName);
        }

        if (slotActions[0] == null) {
            WarnMissingActions($"입력 액션 '{(slotActionNames != null && slotActionNames.Length > 0 ? slotActionNames[0] : "")}' 을 찾지 못했습니다");
            return false;
        }
        return true;
    }

    // HUD 가 칸에 적을 키 이름을 알아내려고 묻는다. 아직 액션을 찾지 못했으면 null.
    public InputAction GetSlotAction(int slotIndex) {
        if (slotIndex < 0 || slotIndex >= SlotCount) return null;
        if (!ResolveSlotActions()) return null;

        return slotActions[slotIndex];
    }

    void WarnMissingActions(string reason) {
        if (warnedMissingActions) return;
        warnedMissingActions = true;
        Debug.LogError($"[SkillManager] {reason}. Client.inputactions ▸ Player 에 Skill1~3 액션이 있어야 스킬을 쓸 수 있습니다.", this);
    }

    void HandleSlotInput(int slotIndex) {
        InputAction action = slotActions[slotIndex];
        if (action == null) return;

        SkillBase skill = equippedSkills[slotIndex];
        if (skill == null) return;

        // 조준형 스킬(IAimableSkill)은 누름·유지·뗌을 전부 스킬에 넘겨준다. 아니면 기존처럼 누르는 즉시 발동.
        if (skill is IAimableSkill aimable) {
            HandleAimableInput(slotIndex, skill, aimable, action);
        }
        else if (action.WasPressedThisFrame()) {
            UseSkillInSlot(slotIndex);
        }
    }

    void HandleAimableInput(int slotIndex, SkillBase skill, IAimableSkill aimable, InputAction action) {
        if (action.WasPressedThisFrame()) {
            if (!skill.IsReady()) {
                if (logCooldown) {
                    Debug.Log($"[SkillManager] '{skill.DisplayName}' 쿨타임 — 남은 시간 {skill.GetRemainingCooldown():F1}초");
                }
                return;
            }

            isAimingSlot[slotIndex] = true;
            aimable.BeginAim(transform);
            return;
        }

        if (!isAimingSlot[slotIndex]) return; // 쿨타임 중이라 조준을 시작하지 못했던 경우 등.

        if (action.WasReleasedThisFrame()) {
            isAimingSlot[slotIndex] = false;

            // ReleaseAim이 항상 발동으로 이어지는 건 아니다 — Invisible Reality처럼 좌클릭으로 이미 설치를
            // 끝냈거나 아예 설치하지 않고 취소한 스킬은 이 시점에 TryUse가 호출되지 않는다.
            // lastUsedTime이 실제로 갱신됐을 때만(=TryUse가 방금 실행됐을 때만) 발동 이벤트를 쏜다.
            float beforeUse = skill.lastUsedTime;
            aimable.ReleaseAim(transform, GetMouseWorldPosition());
            if (skill.lastUsedTime != beforeUse) onSkillUsed?.Invoke(slotIndex, skill);
        }
        else if (action.IsPressed()) {
            aimable.UpdateAim(transform, GetMouseWorldPosition());
        }
    }

    void CancelAllAiming() {
        for (int i = 0; i < SlotCount; i++) {
            if (!isAimingSlot[i]) continue;

            isAimingSlot[i] = false;
            if (equippedSkills[i] is IAimableSkill aimable) aimable.CancelAim(transform);
        }
    }

    // 마우스 커서 위치를 플레이어와 같은 z 평면의 월드 좌표로 변환한다.
    Vector2 GetMouseWorldPosition() {
        if (Mouse.current == null || Camera.main == null) return transform.position;

        Vector3 screenPos = Mouse.current.position.ReadValue();
        screenPos.z = Mathf.Abs(Camera.main.transform.position.z - transform.position.z);
        return Camera.main.ScreenToWorldPoint(screenPos);
    }

    #endregion
    #region 스킬 장착

    // 슬롯에 스킬을 장착한다. skill에 null을 넣으면 해제.
    // 다른 슬롯에 이미 끼워져 있는 스킬을 넣으면 두 슬롯의 내용을 맞바꾼다 (같은 스킬이 두 칸을 먹지 않도록).
    public bool EquipSkill(int slotIndex, SkillBase skill) {
        if (!IsValidSlot(slotIndex)) {
            Debug.LogWarning($"[SkillManager] 슬롯 번호 {slotIndex}는 범위 밖입니다. (0~{SlotCount - 1})", this);
            return false;
        }

        // 해금하지 않은 스킬이 UI 버그나 잘못된 호출로 끼워지는 것을 막는다.
        if (skill != null && !unlockedSkills.Contains(skill)) {
            Debug.LogWarning($"[SkillManager] '{skill.DisplayName}'은 아직 해금되지 않아 장착할 수 없습니다.", this);
            return false;
        }

        if (equippedSkills[slotIndex] == skill) return true; // 이미 같은 상태. 이벤트를 헛돌리지 않는다.

        SkillBase displaced = equippedSkills[slotIndex]; // 이 슬롯에 원래 있던 스킬. 자리를 맞바꿀 때 돌려보낸다.
        int previousSlot = GetSlotOf(skill); // 스킬이 null이면 -1이 나와 교체 처리를 건너뛴다.

        equippedSkills[slotIndex] = skill;
        onSkillEquipped?.Invoke(slotIndex, skill);

        // 다른 슬롯에 있던 스킬을 가져온 경우, 원래 자리에는 이 슬롯에 있던 것을 넣어 맞바꾼다.
        // displaced가 null이면 그 슬롯은 그냥 비워진다.
        if (previousSlot >= 0 && previousSlot != slotIndex) {
            equippedSkills[previousSlot] = displaced;
            onSkillEquipped?.Invoke(previousSlot, displaced);
        }

        return true;
    }

    public bool UnequipSlot(int slotIndex) {
        return EquipSkill(slotIndex, null);
    }

    // 해금 목록에 조용히 넣는다. 시작 장착·세이브 복원처럼 "원래 가지고 있던" 경우에 쓴다. 이미 있으면 false.
    // 스토리에서 새로 되찾는 순간에는 알림·자동 장착이 붙는 UnlockFromStory 를 쓴다.
    public bool UnlockSkill(SkillBase skill) {
        if (skill == null || unlockedSkills.Contains(skill)) return false;

        unlockedSkills.Add(skill);
        return true;
    }

    // 스토리 진행으로 새 스킬을 되찾았을 때. 해금 + 빈 슬롯 자동 장착 + 화면 알림.
    // 이미 가진 스킬이면 아무것도 하지 않는다 — 불러오기 뒤 같은 컷씬을 다시 봐도 알림이 반복되지 않게 하기 위함이다.
    // 씬에서는 SkillUnlockZone · SkillUnlockStep · DialogueTriggerZone.unlockSkill 이 SkillUnlocker 를 거쳐 부른다.
    public bool UnlockFromStory(SkillBase skill) {
        if (!UnlockSkill(skill)) return false;

        // 새로 합류한 스킬의 레벨을 시작값으로 맞춘다. 인스펙터의 allSkills 에 없던 스킬이면 아직 레벨이 심어지지 않았다.
        if (!skillLevels.ContainsKey(skill.SkillId)) SetLevelInternal(skill, skill.startingLevel, notify: false);

        if (autoEquipOnUnlock) {
            for (int i = 0; i < SlotCount; i++) {
                if (equippedSkills[i] != null) continue;
                EquipSkill(i, skill);
                break;
            }
        }

        if (announceUnlock) SkillUnlockView.Announce(skill, playerMove);

        onSkillUnlocked?.Invoke(skill);
        return true;
    }

    #endregion
    #region 스킬 발동

    // 슬롯의 스킬을 발동한다. 빈 슬롯이거나 쿨타임 중이면 false.
    public bool UseSkillInSlot(int slotIndex) {
        if (!IsValidSlot(slotIndex)) return false;

        SkillBase skill = equippedSkills[slotIndex];
        if (skill == null) return false;

        if (!skill.IsReady()) {
            if (logCooldown) {
                Debug.Log($"[SkillManager] '{skill.DisplayName}' 쿨타임 — 남은 시간 {skill.GetRemainingCooldown():F1}초");
            }
            return false;
        }

        // owner로 이 컴포넌트가 붙은 트랜스폼을 넘긴다. 스킬의 판정 위치·방향이 전부 여기서 파생된다.
        if (!skill.TryUse(transform)) return false;

        onSkillUsed?.Invoke(slotIndex, skill);
        return true;
    }

    #endregion
    #region 레벨 · 강화

    // 알고 있는 모든 스킬에 시작 레벨을 심는다. 세이브를 불러오면 RestoreState가 덮어쓴다.
    void SeedStartingLevels() {
        skillLevels.Clear();
        paidShards.Clear();

        foreach (SkillBase skill in EnumerateKnownSkills()) {
            SetLevelInternal(skill, skill.startingLevel, notify: false);
        }
    }

    // 게임에 있는 모든 스킬(해금 여부와 무관). 정비 화면이 아직 못 배운 스킬을 이름을 가린 줄로 보여줄 때 쓴다.
    public IEnumerable<SkillBase> KnownSkills => EnumerateKnownSkills();

    // 이 매니저가 아는 모든 스킬. allSkills에 넣는 것을 깜빡해도 해금·장착 목록에 있으면 함께 챙긴다.
    IEnumerable<SkillBase> EnumerateKnownSkills() {
        HashSet<SkillBase> seen = new();

        foreach (SkillBase skill in allSkills) {
            if (skill != null && seen.Add(skill)) yield return skill;
        }
        foreach (SkillBase skill in unlockedSkills) {
            if (skill != null && seen.Add(skill)) yield return skill;
        }
        foreach (SkillBase skill in equippedSkills) {
            if (skill != null && seen.Add(skill)) yield return skill;
        }
    }

    public int GetLevel(SkillBase skill) {
        if (skill == null) return 0;
        return skillLevels.TryGetValue(skill.SkillId, out int level) ? level : skill.startingLevel;
    }

    public int GetMaxLevel(SkillBase skill) {
        return skill != null ? skill.MaxLevel : 0;
    }

    // 지금 보유한 기억 조각 수. 정비 화면이 "필요 3 · 보유 12" 를 적을 때 쓴다.
    public int ShardCount => shards != null ? shards.Count : 0;

    public bool IsMaxLevel(SkillBase skill) {
        return skill != null && GetLevel(skill) >= skill.MaxLevel;
    }

    // 다음 단계로 올리는 데 드는 조각 수. 더 올릴 수 없으면 -1.
    // **보유량에 따라 값이 달라지므로 UI는 그릴 때마다 다시 물어봐야 합니다.**
    public int GetUpgradeCost(SkillBase skill) {
        if (skill == null || IsMaxLevel(skill)) return -1;

        int step = GetLevel(skill);
        float percent = step < upgradePercents.Length ? upgradePercents[step] : 0f;
        int minimum = step < upgradeMinimums.Length ? upgradeMinimums[step] : 1;
        int held = shards != null ? shards.Count : 0;

        return Mathf.Max(minimum, Mathf.FloorToInt(held * percent));
    }

    // 하한과 비율 중 어느 쪽이 비용을 정했는지. 정비 UI가 "최소 3" / "보유의 10%" 를 골라 쓰기 위한 것으로,
    // 하한이 걸렸는데 비율로 적으면 숫자와 설명이 어긋나 보인다.
    public bool IsUpgradeCostAtMinimum(SkillBase skill) {
        if (skill == null || IsMaxLevel(skill)) return false;

        int step = GetLevel(skill);
        float percent = step < upgradePercents.Length ? upgradePercents[step] : 0f;
        int minimum = step < upgradeMinimums.Length ? upgradeMinimums[step] : 1;
        int held = shards != null ? shards.Count : 0;

        return minimum > Mathf.FloorToInt(held * percent);
    }

    public float GetUpgradePercent(SkillBase skill) {
        if (skill == null || IsMaxLevel(skill)) return 0f;

        int step = GetLevel(skill);
        return step < upgradePercents.Length ? upgradePercents[step] : 0f;
    }

    public bool CanUpgrade(SkillBase skill) {
        int cost = GetUpgradeCost(skill);
        return cost > 0 && shards != null && shards.Count >= cost;
    }

    // 조각을 내고 한 단계 올린다. 낸 값은 환불용으로 기록해 둔다.
    public bool TryUpgradeSkill(SkillBase skill) {
        if (!CanUpgrade(skill)) return false;

        int cost = GetUpgradeCost(skill);
        if (!shards.Spend(cost)) return false;

        int level = GetLevel(skill);
        RecordPayment(skill, level, cost);
        SetLevelInternal(skill, level + 1, notify: true);
        return true;
    }

    // 되돌렸을 때 돌아오는 조각 수. 산 적이 없는 단계(시작 레벨로 받은 것)는 0이다.
    public int GetRefundAmount(SkillBase skill) {
        if (skill == null) return 0;

        int level = GetLevel(skill);
        if (level <= 0) return 0;
        if (!paidShards.TryGetValue(skill.SkillId, out List<int> paid)) return 0;

        int step = level - 1;
        return step < paid.Count ? Mathf.Max(0, paid[step]) : 0;
    }

    public bool CanRefund(SkillBase skill) {
        return GetRefundAmount(skill) > 0;
    }

    // 한 단계 되돌리고 그때 낸 조각을 그대로 돌려준다.
    // **지금 보유량으로 다시 계산하면 안 된다** — 조각이 적을 때 강화해 두고 많아진 뒤 되돌려
    // 차액을 버는 무한 증식이 생긴다.
    public bool TryRefundSkill(SkillBase skill) {
        int back = GetRefundAmount(skill);
        if (back <= 0) return false;

        int level = GetLevel(skill);
        shards.Add(back);
        paidShards[skill.SkillId][level - 1] = 0; // 같은 단계를 두 번 환불받지 못하게 지운다.
        SetLevelInternal(skill, level - 1, notify: true);
        return true;
    }

    void RecordPayment(SkillBase skill, int step, int cost) {
        if (!paidShards.TryGetValue(skill.SkillId, out List<int> paid)) {
            paid = new List<int>();
            paidShards[skill.SkillId] = paid;
        }

        while (paid.Count <= step) paid.Add(0);
        paid[step] = cost;
    }

    // 레벨을 실제로 적용하는 유일한 통로. 스킬 에셋의 runtimeLevel까지 같이 맞춰야
    // 파생 스킬의 GetLevelData()가 새 수치를 읽는다.
    void SetLevelInternal(SkillBase skill, int level, bool notify) {
        if (skill == null) return;

        int clamped = Mathf.Clamp(level, 0, skill.MaxLevel);
        skillLevels[skill.SkillId] = clamped;
        skill.runtimeLevel = clamped;

        if (notify) onSkillLeveled?.Invoke(skill, clamped);
    }

    #endregion
    #region 조회

    public bool IsValidSlot(int slotIndex) {
        return slotIndex >= 0 && slotIndex < SlotCount && equippedSkills != null && slotIndex < equippedSkills.Length;
    }

    public SkillBase GetSkillInSlot(int slotIndex) {
        return IsValidSlot(slotIndex) ? equippedSkills[slotIndex] : null;
    }

    // 스킬이 장착된 슬롯 번호. 장착돼 있지 않으면 -1.
    public int GetSlotOf(SkillBase skill) {
        if (skill == null || equippedSkills == null) return -1;

        for (int i = 0; i < equippedSkills.Length; i++) {
            if (equippedSkills[i] == skill) return i;
        }
        return -1;
    }

    public bool IsEquipped(SkillBase skill) {
        return GetSlotOf(skill) >= 0;
    }

    // 장착·해금 스킬 전부의 쿨타임을 초기화. 리스폰이나 거울 정비 후처럼 판을 새로 시작할 때 쓴다.
    public void ResetAllCooldowns() {
        foreach (SkillBase skill in unlockedSkills) {
            if (skill != null) skill.ResetCooldown();
        }

        for (int i = 0; i < equippedSkills.Length; i++) {
            if (equippedSkills[i] != null) equippedSkills[i].ResetCooldown();
        }
    }

    #endregion
    #region 세이브

    public List<string> CaptureUnlocked() {
        List<string> ids = new();
        foreach (SkillBase skill in unlockedSkills) {
            if (skill != null) ids.Add(skill.SkillId);
        }
        return ids;
    }

    // 빈 슬롯도 빈 문자열로 자리를 채운다. 건너뛰면 복원할 때 슬롯 번호가 앞으로 밀린다.
    public List<string> CaptureEquipped() {
        List<string> ids = new();
        for (int i = 0; i < SlotCount; i++) {
            ids.Add(equippedSkills[i] != null ? equippedSkills[i].SkillId : string.Empty);
        }
        return ids;
    }

    public List<SkillSaveEntry> CaptureLevels() {
        List<SkillSaveEntry> entries = new();

        foreach (SkillBase skill in EnumerateKnownSkills()) {
            SkillSaveEntry entry = new() { id = skill.SkillId, level = GetLevel(skill) };
            if (paidShards.TryGetValue(skill.SkillId, out List<int> paid)) entry.paid = new List<int>(paid);
            entries.Add(entry);
        }

        return entries;
    }

    // 세이브에서 되돌린다. 각 목록은 **비어 있으면 건드리지 않는다** — 스킬 정보가 없던 구버전 세이브를
    // 불러왔을 때 인스펙터로 맞춰둔 해금·장착 상태를 날려버리지 않기 위함이다(체력의 maxHealth == 0 규칙과 같은 방식).
    public void RestoreState(List<string> unlockedIds, List<SkillSaveEntry> levels, List<string> equippedIds) {
        Dictionary<string, SkillBase> byId = BuildIdLookup();

        if (unlockedIds != null && unlockedIds.Count > 0) {
            unlockedSkills.Clear();
            foreach (string id in unlockedIds) {
                if (byId.TryGetValue(id, out SkillBase skill)) {
                    unlockedSkills.Add(skill);
                }
                else {
                    Debug.LogWarning($"[SkillManager] 세이브의 스킬 id '{id}'에 해당하는 에셋을 찾지 못했습니다. " +
                        "allSkills 목록에 그 스킬을 넣어 두세요.", this);
                }
            }
        }

        if (levels != null && levels.Count > 0) {
            foreach (SkillSaveEntry entry in levels) {
                if (entry == null || !byId.TryGetValue(entry.id, out SkillBase skill)) continue;

                paidShards[entry.id] = entry.paid != null ? new List<int>(entry.paid) : new List<int>();
                SetLevelInternal(skill, entry.level, notify: true);
            }
        }

        if (equippedIds != null && equippedIds.Count > 0) {
            for (int i = 0; i < SlotCount; i++) {
                string id = i < equippedIds.Count ? equippedIds[i] : string.Empty;
                SkillBase skill = null;
                if (!string.IsNullOrEmpty(id)) byId.TryGetValue(id, out skill);

                // EquipSkill()을 쓰지 않는다 — 해금 검사와 자리 맞바꾸기가 통째 복원과 충돌한다.
                equippedSkills[i] = skill;
                onSkillEquipped?.Invoke(i, skill);
            }
        }

        // 불러온 직후부터 바로 쓸 수 있어야 한다. 저장 시점의 쿨타임까지 물고 오면 판이 답답해진다.
        ResetAllCooldowns();
    }

    Dictionary<string, SkillBase> BuildIdLookup() {
        Dictionary<string, SkillBase> byId = new();
        foreach (SkillBase skill in EnumerateKnownSkills()) {
            byId[skill.SkillId] = skill;
        }
        return byId;
    }

    #endregion
}
