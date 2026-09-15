using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 기록 선택 화면의 한 자리. SaveSlotSelectView 가 자리 수만큼 이 프리팹을 찍어낸다.
//
// 기록이 있을 때(Filled)와 없을 때(Empty)의 생김새를 한 프리팹 안에 둘 다 두고 켜고 끈다.
// 두 프리팹으로 나누면 기록을 지우는 순간 줄을 통째로 갈아끼워야 해서, 목록 순서와 포커스가 흔들린다.
//
// 지역·거울의 표시 이름은 이 줄이 풀지 않고 화면(SaveSlotSelectView)이 풀어서 넘긴다.
// 이름 대응표가 화면 한 곳에 모여 있어야 세 자리가 같은 이름을 쓴다.
//
// **Prefabs/UI/SaveSlotRow.prefab 의 루트에 붙어 있습니다.** (ObjectiveItemView 와 같은 구조)
public class SaveSlotRowView : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler {
    #region 인스펙터 변수

    [Header("연결 — 공통")]
    public Image background; // 줄 바탕. 마우스를 받는 면이라 raycastTarget 이 켜져 있어야 한다.
    public GameObject focusBorder; // 골라진 줄에만 켜지는 2px 포인트 테두리.
    public TMP_Text numberLabel; // "01".

    [Header("연결 — 기록 있음")]
    public GameObject filledGroup;
    public TMP_Text regionLabel; // 저장한 지역.
    public TMP_Text checkpointLabel; // 저장한 거울.
    public RectTransform egoTrack; // 자아 게이지 바탕. 채움의 최대 폭을 여기서 읽는다.
    public RectTransform egoFill;
    public TMP_Text egoValueLabel;
    public TMP_Text shardValueLabel;
    public Image[] skillSlots; // 장착 스킬 3칸. 빈 칸은 면 색만 낮춘다.
    public TMP_Text savedAtLabel;

    [Header("연결 — 빈자리")]
    public GameObject emptyGroup;
    public TMP_Text emptySubLabel; // 빈자리 안내. 새로 시작할 때와 불러올 때 문구가 다르다.
    public GameObject createButton; // [새 기록 만들기]. 새로 시작하러 온 화면의 골라진 빈자리에서만 보인다.

    [Header("색상")]
    public Color normalColor = UiTheme.Panel;
    public Color focusedColor = UiTheme.PanelRaised;
    public Color numberColor = UiTheme.TextMuted;
    public Color focusedNumberColor = UiTheme.TextHigh;
    public Color skillFilledColor = UiTheme.PanelRaised;
    public Color skillEmptyColor = UiTheme.Panel;

    [Header("문구")]
    public string numberFormat = "{0:00}"; // {0} = 화면에 보이는 번호(1부터).
    public string egoFormat = "{0} / {1}"; // {0} 현재, {1} 최대.
    public string unknownEgoText = "-"; // 체력을 기록하지 않던 구버전 세이브.
    public string savedAtFormat = "yyyy.MM.dd  HH:mm"; // SaveData.savedAt 을 다시 적는 형식.
    public string emptyCreateText = "이 자리에 새 기억을 만들고 프롤로그부터 시작합니다";
    public string emptyLoadText = "이 자리에는 아직 기억이 없습니다";

    #endregion
    #region 런타임 변수

    public int SlotIndex { get; private set; } // 0부터.
    public SaveData Data { get; private set; } // 빈자리면 null.
    public bool HasData => Data != null;

    Action<SaveSlotRowView> onHovered;
    Action<SaveSlotRowView> onClicked;
    bool isFocused;
    bool allowCreate; // 새로 시작하러 온 화면인지. 불러오기 화면에서 [새 기록 만들기]가 뜨면 누를 수 없는 버튼이 된다.
    bool isReady; // 연결이 온전한지. 어긋난 채로 갱신하면 NullReference 가 쏟아지므로 Awake 에서 한 번만 검사한다.

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        isReady = ValidateReferences();
    }

    // 프리팹을 손보다 참조를 끊었을 때 조용히 죽지 않도록 무엇이 비었는지 이름으로 찍어준다.
    bool ValidateReferences() {
        List<string> missing = new();

        if (background == null) missing.Add(nameof(background));
        if (focusBorder == null) missing.Add(nameof(focusBorder));
        if (numberLabel == null) missing.Add(nameof(numberLabel));
        if (filledGroup == null) missing.Add(nameof(filledGroup));
        if (regionLabel == null) missing.Add(nameof(regionLabel));
        if (checkpointLabel == null) missing.Add(nameof(checkpointLabel));
        if (egoTrack == null) missing.Add(nameof(egoTrack));
        if (egoFill == null) missing.Add(nameof(egoFill));
        if (egoValueLabel == null) missing.Add(nameof(egoValueLabel));
        if (shardValueLabel == null) missing.Add(nameof(shardValueLabel));
        if (savedAtLabel == null) missing.Add(nameof(savedAtLabel));
        if (emptyGroup == null) missing.Add(nameof(emptyGroup));
        if (emptySubLabel == null) missing.Add(nameof(emptySubLabel));

        if (missing.Count == 0) return true;

        Debug.LogError($"[SaveSlotRowView] '{name}' 의 프리팹 연결이 비어 있습니다 — {string.Join(", ", missing)}. " +
            "Prefabs/UI/SaveSlotRow 프리팹을 쓰세요. 없으면 Tools ▸ FCC ▸ Build Save Slot Prefabs 로 만들 수 있습니다.", this);
        return false;
    }

    #endregion
    #region 표시 갱신

    public void Bind(Action<SaveSlotRowView> hovered, Action<SaveSlotRowView> clicked) {
        onHovered = hovered;
        onClicked = clicked;
    }

    // region · checkpoint 는 화면이 표시 이름으로 풀어서 넘긴다. data 가 null 이면 빈자리로 그린다.
    // canCreate 는 새로 시작 화면일 때 true — 빈자리를 골라 새 기록을 만들 수 있는지.
    public void SetSlot(int slotIndex, SaveData data, string region, string checkpoint, bool canCreate) {
        SlotIndex = slotIndex;
        Data = data;
        allowCreate = canCreate;
        if (!isReady) return;

        numberLabel.text = string.Format(numberFormat, slotIndex + 1);

        filledGroup.SetActive(HasData);
        emptyGroup.SetActive(!HasData);
        emptySubLabel.text = canCreate ? emptyCreateText : emptyLoadText;

        if (HasData) {
            regionLabel.text = region;
            checkpointLabel.text = checkpoint;
            shardValueLabel.text = data.memoryShardCount.ToString();
            savedAtLabel.text = FormatSavedAt(data.savedAt);
            RefreshEgo(data);
            RefreshSkills(data);
        }

        ApplyFocus();
    }

    public void SetFocused(bool focused) {
        isFocused = focused;
        if (isReady) ApplyFocus();
    }

    void ApplyFocus() {
        background.color = isFocused ? focusedColor : normalColor;
        focusBorder.SetActive(isFocused);
        numberLabel.color = isFocused ? focusedNumberColor : numberColor;

        // 빈자리의 [새 기록 만들기]는 골라진 자리에만 띄운다. 세 자리에 모두 떠 있으면 무엇이 눌릴지 흐려진다.
        if (createButton != null) createButton.SetActive(isFocused && !HasData && allowCreate);
    }

    // maxHealth 가 0 인 구버전 세이브는 체력을 기록하지 않았으므로 게이지를 비우고 수치를 가린다.
    // (SaveManager.Apply 가 같은 기준으로 체력 복원을 건너뛴다.)
    void RefreshEgo(SaveData data) {
        bool known = data.maxHealth > 0;
        float ratio = known ? Mathf.Clamp01((float)data.currentHealth / data.maxHealth) : 0f;

        egoFill.sizeDelta = new Vector2(egoTrack.rect.width * ratio, egoFill.sizeDelta.y);
        egoValueLabel.text = known ? string.Format(egoFormat, data.currentHealth, data.maxHealth) : unknownEgoText;
    }

    // 장착 칸은 빈 문자열로 자리를 채워 저장된다(SaveData.equippedSkillIds). 목록이 짧은 구버전 세이브는 모자란 칸을 빈칸으로 본다.
    void RefreshSkills(SaveData data) {
        if (skillSlots == null) return;

        for (int i = 0; i < skillSlots.Length; i++) {
            if (skillSlots[i] == null) continue;

            bool equipped = data.equippedSkillIds != null && i < data.equippedSkillIds.Count
                && !string.IsNullOrEmpty(data.equippedSkillIds[i]);
            skillSlots[i].color = equipped ? skillFilledColor : skillEmptyColor;
        }
    }

    // SaveManager 는 "yyyy-MM-dd HH:mm:ss" 로 적는다. 그 형식이 아닌 값(손으로 고친 파일 등)은 그대로 보여준다.
    string FormatSavedAt(string raw) {
        if (string.IsNullOrEmpty(raw)) return "";

        if (DateTime.TryParseExact(raw, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime at)) {
            return at.ToString(savedAtFormat, CultureInfo.InvariantCulture);
        }

        return raw;
    }

    #endregion
    #region 마우스

    public void OnPointerEnter(PointerEventData eventData) {
        if (onHovered != null) onHovered(this);
    }

    public void OnPointerClick(PointerEventData eventData) {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (onClicked != null) onClicked(this);
    }

    #endregion
}
