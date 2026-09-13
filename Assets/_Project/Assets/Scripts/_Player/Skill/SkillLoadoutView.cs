using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 거울(SaveMirror)에서 여는 스킬 정비 화면. 장착 슬롯 3칸과 해금된 스킬 목록을 보여주고 교체시킨다.
//
// 생김새(Canvas·창·슬롯·목록 줄)는 전부 프리팹 Prefabs/UI/SkillLoadout.prefab 에 들어 있고, 이 스크립트는
// 상태 계산과 입력만 맡는다. 예전에는 Canvas를 코드로 지었는데 색·간격 하나를 옮길 때마다 스크립트를
// 고쳐야 했고 씬 뷰에서 결과를 볼 수 없어서 프리팹으로 뺐다. 표시 담당은 SkillSlotView·SkillRowView다.
// 프리팹을 처음부터 다시 찍어내려면 에디터 메뉴 Tools ▸ FCC ▸ Build Skill Loadout Prefab.
//
// **씬에 SkillLoadout 프리팹을 하나만 놓으세요.** (HitFeedback·HitVfx와 같은 싱글턴)
public class SkillLoadoutView : MonoBehaviour {
    public static SkillLoadoutView Instance;

    #region 인스펙터 변수

    [Header("연결 — 프리팹이 채워 둔 값입니다")]
    // 화면 전체를 덮는 어두운 막이자 창의 부모. 열고 닫을 때 이것만 켜고 끈다
    // (Canvas 자체를 끄면 다시 켤 때 레이아웃이 한 프레임 튄다).
    public GameObject windowRoot;
    // 장착 슬롯 3칸. **배열 순서가 그대로 슬롯 번호(0·1·2)입니다.** 순서를 바꾸면 표시도 따라 바뀐다.
    public SkillSlotView[] slotViews = new SkillSlotView[SkillManager.SlotCount];
    public Transform rowContainer; // 목록 줄이 쌓이는 부모. VerticalLayoutGroup이 붙어 있다.
    public SkillRowView rowPrefab; // 목록 한 줄 프리팹. **Prefabs/UI/SkillLoadoutRow.prefab**
    public TMP_Text listTitleLabel; // "보유 스킬". 목록이 비면 emptyListText로 갈아치운다.
    public TMP_Text descriptionLabel; // 커서가 올라간 스킬의 설명문.

    [Header("연결 — 강화 패널")]
    // 강화 관련 칸은 비워둬도 된다. 비면 그 부분만 뜨지 않고 장착·해제 같은 나머지 정비 기능은 그대로 동작한다.
    public GameObject upgradePanelRoot; // 강화 영역 전체. 강화 단계가 없는 스킬을 고르면 통째로 꺼진다.
    public TMP_Text levelLabel;         // "Lv 1 / 2".
    public TMP_Text statsLabel;         // 현재 → 다음 수치 비교표.
    public TMP_Text costLabel;          // 필요한 조각 수와 그 값이 나온 근거.
    public Button upgradeButton;
    public TMP_Text upgradeButtonLabel;
    public Button refundButton;
    public TMP_Text refundButtonLabel;

    // 초록/보라로 갈라 두었더니 화면에 포인트 컬러가 셋(금·적·보라)이 되어 버렸다.
    // 색을 늘리는 대신 밝기 차로 구분한다 — 바뀌는 수치만 밝고, 그대로인 수치는 거의 꺼둔다.
    [Header("강화 표시 색")]
    public Color statImprovedColor = UiTheme.TextHigh; // 다음 레벨에서 좋아지는 수치.
    public Color statSameColor = UiTheme.TextDim;      // 레벨이 올라도 그대로인 수치.

    [Header("문구")]
    // 제목·도움말처럼 고정된 문구는 프리팹의 TMP에 직접 적는다. 여기 있는 것들은 상황에 따라 코드가 갈아끼운다.
    public string emptyListText = "아직 해금한 스킬이 없습니다.";
    public string emptySlotText = "비어 있음";
    public string upgradeText = "Lv {0} 로 강화";        // {0} = 올라갈 레벨.
    public string shortOfShardsText = "조각 {0} 부족";   // {0} = 모자란 개수.
    public string maxLevelText = "최대 레벨";
    public string refundText = "되돌리기 · 조각 {0} 반환"; // {0} = 돌려받을 개수.
    public string costFormat = "필요 {0} ({1})   ·   보유 {2}"; // {0} 비용, {1} 근거, {2} 보유량.

    #endregion
    #region 컴포넌트 변수

    readonly List<SkillRowView> rows = new();

    SkillManager manager;
    Player_move playerMove;
    PauseMenuController pauseMenu; // 열려 있는 동안 꺼둔다. ESC를 서로 먹으면 timeScale이 엉키기 때문.

    string authoredListTitle; // 프리팹에 적혀 있던 목록 제목. 빈 목록 문구로 바꿨다가 되돌릴 때 쓴다.
    int selectedSlot; // 지금 고른 장착 슬롯 (0~2).
    int highlightedRow; // 지금 커서가 올라간 스킬 줄.
    float savedTimeScale = 1f;
    bool isReady; // 프리팹 연결이 온전한지. 어긋난 채로 열면 NullReference가 쏟아지므로 Awake에서 한 번만 검사한다.

    public bool IsOpen { get; private set; }

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        if (Instance != null && Instance != this) {
            // 코드로 UI를 짓던 시절에는 컴포넌트만 지웠지만, 이제는 프리팹 뿌리째 들어오므로
            // 오브젝트를 통째로 지워야 한다. 안 그러면 안 쓰는 Canvas가 화면에 남는다.
            Destroy(gameObject);
            return;
        }

        // DontDestroyOnLoad는 루트 오브젝트에서만 동작한다. 씬에서 정리용으로 다른 오브젝트 밑에
        // 넣어뒀을 수 있으므로 먼저 떼어낸다 (SaveManager와 같은 이유).
        transform.SetParent(null);
        Instance = this;
        DontDestroyOnLoad(gameObject);

        isReady = ValidateReferences();
        if (!isReady) return;

        if (listTitleLabel != null) authoredListTitle = listTitleLabel.text;

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
            "Prefabs/UI/SkillLoadout 프리팹을 씬에 놓으세요. 프리팹이 없으면 Tools ▸ FCC ▸ Build Skill Loadout Prefab 으로 만들 수 있습니다.", this);
        return false;
    }

    #endregion
    #region 열기 · 닫기

    // SaveMirror가 저장을 마친 뒤 호출한다. manager가 없으면 열지 않는다.
    public void Open(SkillManager skillManager) {
        if (IsOpen || !isReady) return;

        if (skillManager == null) {
            Debug.LogWarning("[SkillLoadoutView] SkillManager를 찾지 못해 정비 화면을 열 수 없습니다. 플레이어에 SkillManager를 붙이세요.", this);
            return;
        }

        manager = skillManager;
        playerMove = manager.GetComponentInParent<Player_move>();

        // 정비 화면이 떠 있는 동안 플레이어가 움직이거나 거울에 다시 말을 거는 것을 막는다.
        // PlayerInteractor·SkillManager가 둘 다 이 잠금을 보고 입력을 무시한다 (대사·컷씬과 같은 방식).
        if (playerMove != null) playerMove.isMovementLocked = true;

        // ESC를 서로 먹으면 한쪽은 정비 화면을 닫고 다른 쪽은 일시정지를 켜서 timeScale이 어긋난다.
        pauseMenu = FindAnyObjectByType<PauseMenuController>();
        if (pauseMenu != null) pauseMenu.enabled = false;

        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f; // 정비 중에는 몬스터도 멈춘다.

        selectedSlot = 0;
        highlightedRow = 0;

        BuildRows();
        Refresh();

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

        manager = null;
        playerMove = null;
        pauseMenu = null;
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

        // 강화·되돌리기는 장착(Enter/Space)과 겹치지 않는 키로 둔다. 손이 미끄러져 조각을 쓰면 곤란하다.
        if (keyboard.eKey.wasPressedThisFrame) UpgradeHighlighted();
        if (keyboard.rKey.wasPressedThisFrame) RefundHighlighted();
    }

    #endregion
    #region 조작

    void SelectSlot(int slotIndex) {
        if (slotIndex < 0 || slotIndex >= SkillManager.SlotCount) return;

        selectedSlot = slotIndex;
        Refresh();
    }

    void MoveHighlight(int delta) {
        if (rows.Count == 0) return;

        // 목록 끝에서 반대편으로 넘어가게 감싼다. 스킬이 몇 개 없어 위아래로 자주 오가기 때문.
        highlightedRow = (highlightedRow + delta + rows.Count) % rows.Count;
        Refresh();
    }

    void EquipHighlighted() {
        if (manager == null || highlightedRow < 0 || highlightedRow >= rows.Count) return;

        SkillBase skill = rows[highlightedRow].Skill;

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
    }

    void RefundHighlighted() {
        SkillBase skill = HighlightedSkill;
        if (manager == null || skill == null) return;

        if (manager.TryRefundSkill(skill)) Refresh();
    }

    // 마우스로 줄을 누르면 커서를 그 줄로 옮기고 바로 장착까지 한다. 키보드 흐름과 결과가 같도록.
    void HandleRowClicked(SkillBase skill) {
        int index = rows.FindIndex(row => row.Skill == skill);
        if (index < 0) return;

        highlightedRow = index;
        EquipHighlighted();
    }

    #endregion
    #region 표시 갱신

    // 해금 목록에 맞춰 줄을 새로 찍는다. 열 때마다 목록이 달라질 수 있어 매번 다시 짓는다
    // (스킬은 최대 6종이라 재사용 풀을 둘 만큼 무겁지 않다).
    void BuildRows() {
        foreach (SkillRowView row in rows) {
            if (row != null) Destroy(row.gameObject);
        }
        rows.Clear();

        foreach (SkillBase skill in manager.unlockedSkills) {
            if (skill == null) continue; // 인스펙터에서 비워둔 칸.

            SkillRowView row = Instantiate(rowPrefab, rowContainer);
            row.Bind(skill, HandleRowClicked);
            rows.Add(row);
        }

        highlightedRow = Mathf.Clamp(highlightedRow, 0, Mathf.Max(0, rows.Count - 1));
    }

    // 지금 커서가 올라간 스킬. 목록이 비었거나 커서가 범위를 벗어나면 null.
    SkillBase HighlightedSkill =>
        highlightedRow >= 0 && highlightedRow < rows.Count ? rows[highlightedRow].Skill : null;

    void Refresh() {
        RefreshSlots();
        RefreshRows();
        RefreshDescription();
        RefreshUpgradePanel();
    }

    void RefreshSlots() {
        for (int i = 0; i < SkillManager.SlotCount; i++) {
            slotViews[i].Set(manager.GetSkillInSlot(i), emptySlotText);
            slotViews[i].SetSelected(i == selectedSlot);
        }
    }

    void RefreshRows() {
        for (int i = 0; i < rows.Count; i++) {
            rows[i].Refresh(manager.GetSlotOf(rows[i].Skill));
            rows[i].SetHighlighted(i == highlightedRow);
        }

        if (listTitleLabel != null) listTitleLabel.text = rows.Count > 0 ? authoredListTitle : emptyListText;
    }

    void RefreshDescription() {
        if (descriptionLabel == null) return;

        SkillBase skill = HighlightedSkill;
        descriptionLabel.text = skill != null ? skill.description : string.Empty;
    }

    #endregion
    #region 강화 패널 갱신

    void RefreshUpgradePanel() {
        if (upgradePanelRoot == null) return;

        SkillBase skill = HighlightedSkill;

        // 강화 단계가 아예 없는 스킬(Pure Dream 등)은 비교표도 버튼도 의미가 없으므로 통째로 감춘다.
        bool hasLevels = skill != null && manager.GetMaxLevel(skill) > 0;
        upgradePanelRoot.SetActive(hasLevels);
        if (!hasLevels) return;

        int level = manager.GetLevel(skill);
        bool atMax = manager.IsMaxLevel(skill);

        if (levelLabel != null) {
            levelLabel.text = atMax ? $"Lv {level} · 완성" : $"Lv {level} / {manager.GetMaxLevel(skill)}";
        }
        if (statsLabel != null) statsLabel.text = BuildStatTable(skill, level, atMax);

        RefreshCostLabel(skill, atMax);
        RefreshUpgradeButton(skill, atMax);
        RefreshRefundButton(skill);
    }

    // 현재 레벨 수치를 적고, 다음 레벨이 있으면 그 옆에 화살표로 이어 붙인다.
    // 좋아지는 줄만 색을 바꿔, 무엇이 달라지는지 한눈에 보이게 한다.
    string BuildStatTable(SkillBase skill, int level, bool atMax) {
        IReadOnlyList<SkillBase.SkillStat> current = skill.DescribeLevel(level);
        if (current == null || current.Count == 0) return string.Empty;

        IReadOnlyList<SkillBase.SkillStat> next = atMax ? null : skill.DescribeLevel(level + 1);
        string improved = ColorUtility.ToHtmlStringRGB(statImprovedColor);
        string same = ColorUtility.ToHtmlStringRGB(statSameColor);

        StringBuilder sb = new();
        for (int i = 0; i < current.Count; i++) {
            if (i > 0) sb.Append('\n');
            sb.Append(current[i].label).Append("   <b>").Append(current[i].value).Append("</b>");

            if (next == null || i >= next.Count) continue;

            bool changed = next[i].value != current[i].value;
            sb.Append("   <color=#").Append(changed ? improved : same).Append('>')
              .Append("→ ").Append(next[i].value).Append("</color>");
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
            ? $"최소 {cost}"
            : $"보유의 {Mathf.RoundToInt(manager.GetUpgradePercent(skill) * 100f)}%";

        costLabel.text = string.Format(costFormat, cost, basis, manager.ShardCount);
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
