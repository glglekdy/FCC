using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 거울(SaveMirror)에서 여는 스킬 정비 화면. 왼쪽은 장착 슬롯 3칸과 보유 스킬 목록, 오른쪽은 고른 스킬의
// 설명 · 현재와 다음 레벨 수치 · 강화 비용과 버튼이다.
//
// 생김새(Canvas · 창 · 슬롯 · 목록 줄)는 전부 프리팹 Prefabs/UI/SkillLoadout.prefab 에 들어 있고, 이 스크립트는
// 상태 계산과 입력만 맡는다. 표시 담당은 SkillSlotView · SkillRowView 다.
// 프리팹을 처음부터 다시 찍어내려면 에디터 메뉴 Tools ▸ FCC ▸ Build Skill Loadout Prefab.
//
// **씬(또는 InGameUi 프리팹)에 SkillLoadout 을 하나만, 켜진 상태로 놓으세요.** 꺼 두면 Awake 가 돌지 않아
// Instance 가 비고, 거울이 저장만 하고 정비 화면을 열지 못한다. 평소에는 이 스크립트가 창(windowRoot)만 숨긴다.
public class SkillLoadoutView : MonoBehaviour {
    public static SkillLoadoutView Instance;

    #region 인스펙터 변수

    [Header("연결 — 목록")]
    // 화면 전체를 덮는 어두운 막이자 창의 부모. 열고 닫을 때 이것만 켜고 끈다
    // (Canvas 자체를 끄면 다시 켤 때 레이아웃이 한 프레임 튄다).
    public GameObject windowRoot;
    // 장착 슬롯 3칸. **배열 순서가 그대로 슬롯 번호(0·1·2)입니다.**
    public SkillSlotView[] slotViews = new SkillSlotView[SkillManager.SlotCount];
    public Transform rowContainer; // 목록 줄이 쌓이는 부모. VerticalLayoutGroup이 붙어 있다.
    public SkillRowView rowPrefab; // 목록 한 줄 프리팹. **Prefabs/UI/SkillLoadoutRow.prefab**
    public TMP_Text shardCountLabel; // 오른쪽 위 보유 기억 조각 수.

    [Header("연결 — 상세")]
    public GameObject detailRoot; // 오른쪽 영역 전체. 고를 스킬이 하나도 없으면 통째로 숨긴다.
    public TMP_Text nameLabel;
    public TMP_Text roleLabel;
    public TMP_Text descriptionLabel;
    public GameObject levelRoot; // 레벨 칸 · 수치표 · 강화 영역. 강화 단계가 없는 스킬이면 숨긴다.
    public Image[] levelPips; // 강화 단계 칸. 최대 레벨 수만큼만 켜진다.
    public TMP_Text levelLabel; // "Lv 1 / 2".
    public TMP_Text statsLabel; // 현재 → 다음 수치 비교표. <pos> 태그로 열을 맞춘다.
    public TMP_Text costLabel; // 필요한 조각 수와 그 값이 나온 근거.
    public Button upgradeButton;
    public TMP_Text upgradeButtonLabel;
    public Button refundButton;
    public TMP_Text refundButtonLabel;
    public TMP_Text equipStatusLabel; // "슬롯 1 에 장착 중".

    [Header("색상")]
    // 좋아지는 수치와 그대로인 수치를 색이 아니라 밝기 차로 가른다. 포인트 컬러를 둘로 늘리지 않기 위함이다.
    public Color statImprovedColor = UiTheme.TextHigh;
    public Color statSameColor = UiTheme.TextDim;
    public Color pipOnColor = UiTheme.AccentBright;
    public Color pipOffColor = UiTheme.PanelRaised;
    public Color mutedTextColor = UiTheme.TextMuted; // 비용 근거처럼 한 단계 흐린 표기.

    [Header("문구")]
    // 제목 · 도움말처럼 고정된 문구는 프리팹의 TMP에 직접 적는다. 여기 있는 것들은 상황에 따라 코드가 갈아끼운다.
    public string emptySlotText = "비어 있음";
    public string levelFormat = "Lv {0} / {1}"; // {0} 지금, {1} 최대.
    public string levelMaxFormat = "Lv {0} · 완성";
    public string upgradeText = "Lv {0} 로 강화"; // {0} = 올라갈 레벨.
    public string shortOfShardsText = "조각 {0} 부족"; // {0} = 모자란 개수.
    public string maxLevelText = "더 올릴 단계가 없습니다";
    public string refundText = "되돌리기 · 조각 {0} 반환"; // {0} = 돌려받을 개수.
    public string costFormat = "필요 {0}"; // {0} 비용. 근거와 보유량은 흐린 색으로 뒤에 붙인다.
    public string costBasisFormat = "{0} · 보유 {1}"; // {0} 근거("최소 3" / "보유의 10%"), {1} 보유량.
    public string minimumBasisFormat = "최소 {0}";
    public string percentBasisFormat = "보유의 {0}%";
    public string equippedFormat = "슬롯 {0} 에 장착 중"; // {0}은 1부터 세는 슬롯 번호.
    public string notEquippedText = "장착되어 있지 않음 · Enter 로 고른 슬롯에 끼웁니다";

    #endregion
    #region 컴포넌트 변수

    readonly List<SkillRowView> rows = new();

    SkillManager manager;
    Player_move playerMove;
    PauseMenuView pauseMenu; // 열려 있는 동안 꺼둔다. ESC를 서로 먹으면 timeScale이 엉키기 때문.
    Action onClosed; // 여는 쪽(거울)이 닫힌 뒤 할 일. 거울은 정비 결과를 한 번 더 저장한다.

    int selectedSlot; // 지금 고른 장착 슬롯 (0~2).
    int highlightedRow; // 지금 커서가 올라간 스킬 줄. 잠긴 줄에는 머물지 않는다.
    int unlockedRowCount; // 목록 앞쪽의 되찾은 스킬 줄 수. 잠긴 줄은 그 뒤에 붙는다.
    int openedFrame = -1; // 연 프레임. 거울을 연 입력(F)과 같은 프레임에 눌린 키가 정비 조작으로 먹히는 것을 막는다.
    float savedTimeScale = 1f;
    bool isReady; // 프리팹 연결이 온전한지. 어긋난 채로 열면 NullReference가 쏟아지므로 Awake에서 한 번만 검사한다.

    public bool IsOpen { get; private set; }

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        if (Instance != null && Instance != this) {
            // 프리팹 뿌리째 들어오므로 오브젝트를 통째로 지운다. 안 그러면 안 쓰는 Canvas가 화면에 남는다.
            Destroy(gameObject);
            return;
        }

        // DontDestroyOnLoad는 루트 오브젝트에서만 동작한다. InGameUi 같은 다른 오브젝트 밑에
        // 넣어뒀을 수 있으므로 먼저 떼어낸다 (SaveManager와 같은 이유).
        transform.SetParent(null);
        Instance = this;
        DontDestroyOnLoad(gameObject);

        isReady = ValidateReferences();
        if (!isReady) return;

        // 슬롯 번호는 배열 순서에서만 나온다. 여기서 한 번 물려두면 클릭이 알아서 SelectSlot으로 들어온다.
        for (int i = 0; i < SkillManager.SlotCount; i++) {
            slotViews[i].Bind(i, SelectSlot);
        }

        if (upgradeButton != null) upgradeButton.onClick.AddListener(UpgradeHighlighted);
        if (refundButton != null) refundButton.onClick.AddListener(RefundHighlighted);

        windowRoot.SetActive(false);
    }

    void OnDestroy() {
        if (Instance == this) Instance = null;
    }

    void Update() {
        if (!IsOpen) return;
        if (Time.frameCount == openedFrame) return;

        HandleInput();
    }

    #endregion
    #region 프리팹 연결 검사

    // 프리팹을 쓰지 않고 컴포넌트만 붙였거나, 프리팹을 손보다 참조를 끊었을 때 조용히 죽지 않도록
    // 무엇이 비었는지 이름으로 찍어준다. 하나라도 비면 화면을 아예 열지 않는다.
    bool ValidateReferences() {
        List<string> missing = new();

        if (windowRoot == null) missing.Add(nameof(windowRoot));
        if (rowContainer == null) missing.Add(nameof(rowContainer));
        if (rowPrefab == null) missing.Add(nameof(rowPrefab));
        if (detailRoot == null) missing.Add(nameof(detailRoot));
        if (nameLabel == null) missing.Add(nameof(nameLabel));

        if (slotViews == null || slotViews.Length < SkillManager.SlotCount) {
            missing.Add($"{nameof(slotViews)}(칸 {SkillManager.SlotCount}개 필요)");
        }
        else {
            for (int i = 0; i < SkillManager.SlotCount; i++) {
                if (slotViews[i] == null) missing.Add($"{nameof(slotViews)}[{i}]");
            }
        }

        if (missing.Count == 0) return true;

        Debug.LogError($"[SkillLoadoutView] 프리팹 연결이 비어 있어 정비 화면을 쓸 수 없습니다 — {string.Join(", ", missing)}. " +
            "Prefabs/UI/SkillLoadout 프리팹을 쓰세요. 없으면 Tools ▸ FCC ▸ Build Skill Loadout Prefab 으로 만들 수 있습니다.", this);
        return false;
    }

    #endregion
    #region 열기 · 닫기

    // SaveMirror가 저장을 마친 뒤 호출한다. closed 는 창이 닫힌 뒤 한 번 불린다(비워도 된다).
    public void Open(SkillManager skillManager, Action closed = null) {
        if (IsOpen || !isReady) return;

        if (skillManager == null) {
            Debug.LogWarning("[SkillLoadoutView] SkillManager를 찾지 못해 정비 화면을 열 수 없습니다. 플레이어에 SkillManager를 붙이세요.", this);
            return;
        }

        manager = skillManager;
        onClosed = closed;
        playerMove = manager.GetComponentInParent<Player_move>();

        // 정비 화면이 떠 있는 동안 플레이어가 움직이거나 스킬을 쓰거나 거울에 다시 말을 거는 것을 막는다.
        // PlayerInteractor · SkillManager 가 둘 다 이 잠금을 보고 입력을 무시한다 (대사 · 컷씬과 같은 방식).
        if (playerMove != null) playerMove.isMovementLocked = true;

        // ESC를 서로 먹으면 한쪽은 정비 화면을 닫고 다른 쪽은 일시정지를 켜서 timeScale이 어긋난다.
        pauseMenu = FindAnyObjectByType<PauseMenuView>();
        if (pauseMenu != null) pauseMenu.enabled = false;

        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f; // 정비 중에는 몬스터도 멈춘다.

        selectedSlot = 0;
        highlightedRow = 0;
        openedFrame = Time.frameCount;

        BuildRows();
        Refresh();
        ClearUiSelection();

        windowRoot.SetActive(true);
        IsOpen = true;
    }

    public void Close() {
        if (!IsOpen) return;

        IsOpen = false;
        windowRoot.SetActive(false);

        Time.timeScale = savedTimeScale;

        if (pauseMenu != null) pauseMenu.enabled = true;
        if (playerMove != null) playerMove.isMovementLocked = false;

        Action closed = onClosed;
        manager = null;
        playerMove = null;
        pauseMenu = null;
        onClosed = null;

        ClearUiSelection();
        if (closed != null) closed();
    }

    #endregion
    #region 입력 처리

    // EventSystem의 내비게이션 액션에 기대지 않고 키보드를 직접 읽는다.
    // UI 모듈 설정이 씬마다 달라도 똑같이 동작해야 하기 때문 (마우스 클릭은 Button이 따로 받는다).
    void HandleInput() {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.escapeKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame) {
            Close();
            return;
        }

        if (keyboard.digit1Key.wasPressedThisFrame) SelectSlot(0);
        if (keyboard.digit2Key.wasPressedThisFrame) SelectSlot(1);
        if (keyboard.digit3Key.wasPressedThisFrame) SelectSlot(2);

        if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) MoveHighlight(-1);
        if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) MoveHighlight(1);

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) {
            EquipHighlighted();
        }

        if (keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame) {
            UnequipSelectedSlot();
        }

        // 강화 · 되돌리기는 장착(Enter/Space)과 겹치지 않는 키로 둔다. 손이 미끄러져 조각을 쓰면 곤란하다.
        if (keyboard.eKey.wasPressedThisFrame) UpgradeHighlighted();
        if (keyboard.rKey.wasPressedThisFrame) RefundHighlighted();
    }

    // 버튼을 마우스로 누르면 EventSystem 이 그 버튼을 붙잡아 두어, 다음 Space/Enter 에 한 번 더 눌린다.
    // 이 화면은 입력을 직접 처리하므로 선택을 늘 비운다 (설정창 · 기억 선택 화면과 같은 이유).
    static void ClearUiSelection() {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    #endregion
    #region 조작

    void SelectSlot(int slotIndex) {
        if (slotIndex < 0 || slotIndex >= SkillManager.SlotCount) return;

        selectedSlot = slotIndex;
        Refresh();
        ClearUiSelection();
    }

    // 잠긴 줄은 목록 뒤쪽에 모여 있으므로, 되찾은 줄 사이에서만 감싸 돈다.
    void MoveHighlight(int delta) {
        if (unlockedRowCount == 0) return;

        highlightedRow = (highlightedRow + delta + unlockedRowCount) % unlockedRowCount;
        Refresh();
    }

    void EquipHighlighted() {
        SkillBase skill = HighlightedSkill;
        if (manager == null || skill == null) return;

        // 고른 슬롯에 이미 그 스킬이 들어있으면 해제로 동작한다. 같은 키로 넣고 빼는 편이 손에 익는다.
        if (manager.GetSkillInSlot(selectedSlot) == skill) {
            manager.UnequipSlot(selectedSlot);
        }
        else {
            manager.EquipSkill(selectedSlot, skill);
        }

        Refresh();
    }

    void UnequipSelectedSlot() {
        if (manager == null) return;

        manager.UnequipSlot(selectedSlot);
        Refresh();
    }

    void UpgradeHighlighted() {
        SkillBase skill = HighlightedSkill;
        if (manager == null || skill == null) return;

        if (manager.TryUpgradeSkill(skill)) Refresh();
        ClearUiSelection();
    }

    void RefundHighlighted() {
        SkillBase skill = HighlightedSkill;
        if (manager == null || skill == null) return;

        if (manager.TryRefundSkill(skill)) Refresh();
        ClearUiSelection();
    }

    // 마우스로 줄을 누르면 커서를 그 줄로 옮기고 바로 장착까지 한다. 키보드 흐름과 결과가 같도록.
    void HandleRowClicked(SkillRowView row) {
        int index = rows.IndexOf(row);
        if (index < 0 || row.IsLocked) return;

        highlightedRow = index;
        EquipHighlighted();
        ClearUiSelection();
    }

    void HandleRowHovered(SkillRowView row) {
        int index = rows.IndexOf(row);
        if (index < 0 || row.IsLocked || index == highlightedRow) return;

        highlightedRow = index;
        Refresh();
    }

    #endregion
    #region 표시 갱신

    // 줄을 새로 찍는다. 되찾은 스킬을 앞에, 아직 못 배운 스킬을 이름을 가려 뒤에 둔다.
    // 열 때마다 목록이 달라질 수 있어 매번 다시 짓는다 (스킬은 최대 6종이라 재사용 풀을 둘 만큼 무겁지 않다).
    void BuildRows() {
        foreach (SkillRowView row in rows) {
            if (row == null) continue;

            // Destroy 는 프레임 끝에 지우므로 부모에서 먼저 떼어낸다. 그래야 새 줄이 옛 줄 뒤에 밀려 서지 않는다.
            row.transform.SetParent(null, false);
            Destroy(row.gameObject);
        }
        rows.Clear();

        List<SkillBase> locked = new();
        foreach (SkillBase skill in manager.KnownSkills) {
            if (skill == null) continue;
            if (!manager.unlockedSkills.Contains(skill)) {
                locked.Add(skill);
                continue;
            }
            AddRow(skill, false);
        }

        unlockedRowCount = rows.Count;
        foreach (SkillBase skill in locked) AddRow(skill, true);

        highlightedRow = Mathf.Clamp(highlightedRow, 0, Mathf.Max(0, unlockedRowCount - 1));
    }

    void AddRow(SkillBase skill, bool isLocked) {
        SkillRowView row = Instantiate(rowPrefab, rowContainer);
        row.Bind(skill, isLocked, HandleRowClicked, HandleRowHovered);
        rows.Add(row);
    }

    // 지금 커서가 올라간 스킬. 되찾은 스킬이 하나도 없으면 null.
    SkillBase HighlightedSkill =>
        highlightedRow >= 0 && highlightedRow < unlockedRowCount ? rows[highlightedRow].Skill : null;

    void Refresh() {
        if (shardCountLabel != null) shardCountLabel.text = manager.ShardCount.ToString();

        RefreshSlots();
        RefreshRows();
        RefreshDetail();
    }

    void RefreshSlots() {
        for (int i = 0; i < SkillManager.SlotCount; i++) {
            SkillBase skill = manager.GetSkillInSlot(i);
            slotViews[i].Set(skill, skill != null ? manager.GetLevel(skill) : 0, emptySlotText);
            slotViews[i].SetSelected(i == selectedSlot);
        }
    }

    void RefreshRows() {
        for (int i = 0; i < rows.Count; i++) {
            SkillBase skill = rows[i].Skill;
            rows[i].Refresh(manager.GetSlotOf(skill), manager.GetLevel(skill), manager.GetMaxLevel(skill));
            rows[i].SetHighlighted(i == highlightedRow);
        }
    }

    #endregion
    #region 상세 갱신

    void RefreshDetail() {
        SkillBase skill = HighlightedSkill;
        detailRoot.SetActive(skill != null);
        if (skill == null) return;

        nameLabel.text = skill.DisplayName;
        if (roleLabel != null) roleLabel.text = skill.roleLabel;
        if (descriptionLabel != null) descriptionLabel.text = skill.description;

        RefreshEquipStatus(skill);

        // 강화 단계가 아예 없는 스킬은 레벨 칸 · 비교표 · 버튼이 의미가 없으므로 통째로 감춘다.
        int maxLevel = manager.GetMaxLevel(skill);
        if (levelRoot != null) levelRoot.SetActive(maxLevel > 0);
        if (maxLevel <= 0) return;

        int level = manager.GetLevel(skill);
        bool atMax = manager.IsMaxLevel(skill);

        RefreshPips(level, maxLevel);
        if (levelLabel != null) levelLabel.text = atMax ? string.Format(levelMaxFormat, level) : string.Format(levelFormat, level, maxLevel);
        if (statsLabel != null) statsLabel.text = BuildStatTable(skill, level, atMax);

        RefreshCostLabel(skill, atMax);
        RefreshUpgradeButton(skill, atMax);
        RefreshRefundButton(skill);
    }

    void RefreshEquipStatus(SkillBase skill) {
        if (equipStatusLabel == null) return;

        int slot = manager.GetSlotOf(skill);
        equipStatusLabel.text = slot >= 0 ? string.Format(equippedFormat, slot + 1) : notEquippedText;
    }

    // 최대 레벨 수만큼만 칸을 켜고, 지금 레벨까지 채운다. 스킬마다 강화 단계 수(1 또는 2)가 달라서다.
    void RefreshPips(int level, int maxLevel) {
        if (levelPips == null) return;

        for (int i = 0; i < levelPips.Length; i++) {
            if (levelPips[i] == null) continue;

            bool exists = i < maxLevel;
            levelPips[i].gameObject.SetActive(exists);
            if (exists) levelPips[i].color = i < level ? pipOnColor : pipOffColor;
        }
    }

    // 항목 · 현재 · 다음을 세 열로 맞춘다. 좋아지는 줄만 다음 값을 밝게 두어, 무엇이 달라지는지 한눈에 보이게 한다.
    // 열 위치는 프리팹의 머리글(StatsHeader)과 같은 <pos> 값을 쓴다 — 둘이 어긋나면 표가 틀어진다.
    string BuildStatTable(SkillBase skill, int level, bool atMax) {
        IReadOnlyList<SkillBase.SkillStat> current = skill.DescribeLevel(level);
        if (current == null || current.Count == 0) return string.Empty;

        IReadOnlyList<SkillBase.SkillStat> next = atMax ? null : skill.DescribeLevel(level + 1);
        string improved = ColorUtility.ToHtmlStringRGB(statImprovedColor);
        string same = ColorUtility.ToHtmlStringRGB(statSameColor);

        StringBuilder sb = new();
        for (int i = 0; i < current.Count; i++) {
            if (i > 0) sb.Append('\n');
            sb.Append(current[i].label).Append("<pos=46%>").Append(current[i].value);

            if (next == null || i >= next.Count) continue;

            bool changed = next[i].value != current[i].value;
            string color = changed ? improved : same;
            sb.Append("<pos=64%><color=#").Append(same).Append(">→</color>")
              .Append("<pos=76%><color=#").Append(color).Append('>').Append(next[i].value).Append("</color>");
        }

        return sb.ToString();
    }

    void RefreshCostLabel(SkillBase skill, bool atMax) {
        if (costLabel == null) return;

        if (atMax) {
            costLabel.text = maxLevelText;
            return;
        }

        // 하한이 걸렸는지 비율이 이겼는지를 그대로 적는다. 하한인데 "보유의 10%"라고 쓰면 숫자와 설명이 어긋난다.
        int cost = manager.GetUpgradeCost(skill);
        string basis = manager.IsUpgradeCostAtMinimum(skill)
            ? string.Format(minimumBasisFormat, cost)
            : string.Format(percentBasisFormat, Mathf.RoundToInt(manager.GetUpgradePercent(skill) * 100f));

        string muted = ColorUtility.ToHtmlStringRGB(mutedTextColor);
        costLabel.text = string.Format(costFormat, cost) +
            $"   <color=#{muted}>{string.Format(costBasisFormat, basis, manager.ShardCount)}</color>";
    }

    void RefreshUpgradeButton(SkillBase skill, bool atMax) {
        if (upgradeButton == null) return;

        upgradeButton.gameObject.SetActive(!atMax);
        if (atMax) return;

        bool can = manager.CanUpgrade(skill);
        upgradeButton.interactable = can;

        if (upgradeButtonLabel == null) return;

        upgradeButtonLabel.text = can
            ? string.Format(upgradeText, manager.GetLevel(skill) + 1)
            : string.Format(shortOfShardsText, manager.GetUpgradeCost(skill) - manager.ShardCount);
    }

    // 산 적이 없는 단계(시작 레벨로 받은 것)는 돌려줄 것이 없으므로 버튼 자체를 감춘다.
    void RefreshRefundButton(SkillBase skill) {
        if (refundButton == null) return;

        bool can = manager.CanRefund(skill);
        refundButton.gameObject.SetActive(can);

        if (can && refundButtonLabel != null) {
            refundButtonLabel.text = string.Format(refundText, manager.GetRefundAmount(skill));
        }
    }

    #endregion
}
