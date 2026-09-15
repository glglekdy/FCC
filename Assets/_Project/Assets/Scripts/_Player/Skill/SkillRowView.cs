using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 정비 화면 "보유 스킬" 목록의 한 줄. SkillLoadoutView가 게임에 있는 스킬 수만큼 이 프리팹을 찍어낸다.
// 아직 되찾지 못한 스킬은 이름을 가린 "잠긴 줄" 로 그린다 — 앞으로 무엇이 더 있는지는 보여주되, 이름은 해금 순간의 보상으로 남긴다.
// 자기 줄의 문구 · 색만 알고, 지금 레벨이나 장착 슬롯은 밖에서 받아온다.
//
// **Prefabs/UI/SkillLoadoutRow.prefab 의 루트에 붙어 있습니다.** (ObjectiveItemView와 같은 구조)
public class SkillRowView : MonoBehaviour, IPointerEnterHandler {
    #region 인스펙터 변수

    [Header("연결")]
    public Image background; // 커서가 올라왔을 때만 칠해지는 바탕. **투명해도 raycastTarget 은 켜두세요 — 마우스를 이 이미지가 받습니다.**
    public GameObject focusMarker; // 커서가 올라온 줄 왼쪽의 3px 포인트 선.
    public TMP_Text nameLabel; // 스킬 이름. 잠긴 줄이면 가린 이름.
    public TMP_Text stateLabel; // "장착 중 · Lv 1 / 2" 같은 오른쪽 표기.
    public Image iconImage; // 스킬 아이콘. 비워도 된다(아이콘 없는 스킬은 Image만 꺼서 줄 높이는 유지한다).
    public Button button; // 마우스로 이 줄을 누르는 버튼. 비워두면 자기 오브젝트에서 찾는다.

    [Header("색상")]
    public Color normalColor = UiTheme.Transparent;
    public Color highlightColor = UiTheme.PanelRaised;
    public Color nameColor = UiTheme.TextBody;
    public Color highlightNameColor = UiTheme.TextHigh;
    public Color stateColor = UiTheme.TextMuted;
    public Color equippedColor = UiTheme.AccentBright; // 이 줄에서 포인트 컬러를 쓰는 곳은 "장착 중" 하나뿐이다.
    public Color lockedColor = UiTheme.TextDim;

    [Header("문구")]
    public string lockedName = "? ? ?";
    public string lockedStateText = "아직 떠올리지 못한 기억";
    public string equippedText = "장착 중";
    public string levelFormat = "Lv {0} / {1}"; // {0} 지금, {1} 최대.
    public string noLevelText = "강화 없음"; // 강화 단계가 없는 스킬.

    #endregion
    #region 런타임 변수

    // 이 줄이 대표하는 스킬. SkillLoadoutView가 장착 · 설명 표시에 다시 꺼내 쓴다.
    public SkillBase Skill { get; private set; }
    public bool IsLocked { get; private set; }

    Action<SkillRowView> onHovered;
    bool isHighlighted;

    #endregion
    #region 초기화

    // 생성 직후 스킬을 물리고 마우스 연결까지 맡긴다. 잠긴 줄은 눌러도 반응하지 않는다.
    public void Bind(SkillBase skill, bool locked, Action<SkillRowView> clicked, Action<SkillRowView> hovered) {
        Skill = skill;
        IsLocked = locked;
        onHovered = hovered;

        if (nameLabel != null) nameLabel.text = locked || skill == null ? lockedName : skill.DisplayName;

        if (iconImage != null) {
            iconImage.sprite = !locked && skill != null ? skill.icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }

        if (button == null) button = GetComponent<Button>();
        if (button != null) {
            button.onClick.RemoveAllListeners();
            button.interactable = !locked;
            if (!locked && clicked != null) button.onClick.AddListener(() => clicked(this));
        }

        ApplyColors();
    }

    #endregion
    #region 표시 갱신

    // equippedSlot 이 0 이상이면 장착 표기를 앞에 붙인다. 잠긴 줄은 무엇을 넘겨도 가린 문구 그대로다.
    public void Refresh(int equippedSlot, int level, int maxLevel) {
        if (stateLabel == null) return;

        if (IsLocked) {
            stateLabel.text = lockedStateText;
            return;
        }

        string levelText = maxLevel > 0 ? string.Format(levelFormat, level, maxLevel) : noLevelText;
        if (equippedSlot < 0) {
            stateLabel.text = levelText;
            return;
        }

        // 장착 표기만 포인트 컬러로 칠한다. 줄 전체를 칠하면 커서가 올라온 줄과 구분이 안 된다.
        string accent = ColorUtility.ToHtmlStringRGB(equippedColor);
        stateLabel.text = $"<color=#{accent}>{equippedText}</color>  ·  {levelText}";
    }

    public void SetHighlighted(bool highlighted) {
        isHighlighted = highlighted && !IsLocked;
        ApplyColors();
    }

    void ApplyColors() {
        if (background != null) background.color = isHighlighted ? highlightColor : normalColor;
        if (focusMarker != null) focusMarker.SetActive(isHighlighted);
        if (nameLabel != null) nameLabel.color = IsLocked ? lockedColor : (isHighlighted ? highlightNameColor : nameColor);
        if (stateLabel != null) stateLabel.color = IsLocked ? lockedColor : stateColor;
    }

    #endregion
    #region 마우스

    // 커서가 올라오면 그 줄이 곧 선택이다. 키보드 커서와 마우스 커서를 따로 두면 E(강화)가 어느 줄에 먹힐지 헷갈린다.
    public void OnPointerEnter(PointerEventData eventData) {
        if (IsLocked) return;
        if (onHovered != null) onHovered(this);
    }

    #endregion
}
