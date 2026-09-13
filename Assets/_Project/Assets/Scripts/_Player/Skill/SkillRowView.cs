using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 정비 화면 하단 "보유 스킬" 목록의 한 줄. SkillLoadoutView가 해금된 스킬 수만큼 이 프리팹을 찍어낸다.
// 자기 줄의 문구·색만 알고, 지금 어느 슬롯에 끼워져 있는지는 밖에서 번호로 받아온다.
//
// **Prefabs/UI/SkillLoadoutRow.prefab 의 루트에 붙어 있습니다.** (ObjectiveItemView와 같은 구조)
public class SkillRowView : MonoBehaviour {
    #region 인스펙터 변수

    [Header("연결")]
    public Image background; // 커서가 올라갔는지에 따라 색이 바뀌는 배경.
    public TMP_Text nameLabel; // 스킬 이름.
    public TMP_Text stateLabel; // "슬롯 2 장착 중" 또는 쿨타임 표기.
    public Image iconImage; // 스킬 아이콘. 아이콘이 없는 스킬이면 Image만 꺼서 줄 높이는 유지한다.
    public Button button; // 마우스로 이 줄을 고르는 버튼. 비워두면 자기 오브젝트에서 찾는다.

    [Header("색상")]
    public Color normalColor = UiTheme.PanelRaised;
    public Color highlightColor = UiTheme.Line; // 커서가 올라간 줄. 면으로 쓰기엔 Accent 가 세서 경계선 색을 면으로 돌려 쓴다.
    public Color nameColor = UiTheme.TextHigh;
    public Color equippedColor = UiTheme.Accent; // 이 줄에서 포인트 컬러를 쓰는 곳은 "장착 중" 하나뿐이다.
    public Color cooldownColor = UiTheme.TextMuted; // 안 끼운 스킬의 쿨타임 안내는 흐리게.

    [Header("문구")]
    public string equippedFormat = "슬롯 {0} 장착 중"; // {0}은 1부터 세는 슬롯 번호.
    public string cooldownFormat = "쿨타임 {0:F1}초"; // {0}은 쿨타임 초.

    #endregion
    #region 런타임 변수

    // 이 줄이 대표하는 스킬. SkillLoadoutView가 장착·설명 표시에 다시 꺼내 쓴다.
    public SkillBase Skill { get; private set; }

    #endregion
    #region 초기화

    // 생성 직후 스킬을 물리고 클릭 연결까지 맡긴다.
    public void Bind(SkillBase skill, Action<SkillBase> onClicked) {
        Skill = skill;

        if (nameLabel != null) {
            nameLabel.text = skill != null ? skill.DisplayName : string.Empty;
            nameLabel.color = nameColor;
        }

        if (iconImage != null) {
            iconImage.sprite = skill != null ? skill.icon : null;

            // 스프라이트가 없는 Image는 흰 사각형이 된다. 자리는 남겨두고 Image만 끈다 (SkillSlotView와 같은 이유).
            iconImage.enabled = iconImage.sprite != null;
        }

        if (button == null) button = GetComponent<Button>();
        if (button == null || onClicked == null) return;

        SkillBase captured = skill; // 클로저가 필드를 붙잡지 않도록 복사.
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClicked(captured));
    }

    #endregion
    #region 표시 갱신

    // equippedSlot이 0 이상이면 장착 표기, -1이면 쿨타임 안내.
    public void Refresh(int equippedSlot) {
        if (stateLabel == null) return;

        if (equippedSlot >= 0) {
            stateLabel.text = string.Format(equippedFormat, equippedSlot + 1);
            stateLabel.color = equippedColor;
        }
        else {
            stateLabel.text = Skill != null ? string.Format(cooldownFormat, Skill.cooldown) : string.Empty;
            stateLabel.color = cooldownColor;
        }
    }

    public void SetHighlighted(bool highlighted) {
        if (background != null) background.color = highlighted ? highlightColor : normalColor;
    }

    #endregion
}
