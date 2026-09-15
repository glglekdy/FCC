using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 정비 화면 왼쪽 위의 장착 슬롯 한 칸. 자기 칸의 표시만 담당하고, 눌리면 슬롯 번호를 SkillLoadoutView에 돌려준다.
//
// 슬롯 번호를 인스펙터에 적게 하지 않은 이유는, SkillLoadoutView.slotViews 배열 순서와 어긋나는 순간
// "슬롯 2를 눌렀는데 3번에 들어가는" 사고가 나기 때문이다. 번호는 배열 순서에서만 나온다.
//
// **SkillLoadout 프리팹의 Slot0~2 오브젝트에 붙어 있습니다.**
public class SkillSlotView : MonoBehaviour {
    #region 인스펙터 변수

    [Header("연결")]
    public Image background; // 칸 바탕. 마우스를 받는 면이다.
    public GameObject focusBorder; // 지금 고른 슬롯에만 켜지는 2px 포인트 테두리. 장착 · 해제가 이 칸으로 향한다.
    public TMP_Text skillLabel; // 장착된 스킬 이름. "슬롯 1" 같은 고정 문구는 프리팹에 적어두고 코드가 건드리지 않는다.
    public TMP_Text levelLabel; // "Lv 1". 비워도 된다.
    public Image iconImage; // 스킬 아이콘. 아이콘이 없는 스킬이면 Image만 꺼서 칸 크기는 유지한다. 비워도 된다.
    public Button button; // 마우스로 슬롯을 고르는 버튼. 비워두면 자기 오브젝트에서 찾는다.

    [Header("색상")]
    public Color skillTextColor = UiTheme.TextHigh;
    public Color emptyTextColor = UiTheme.TextDim; // "비어 있음"은 잠긴 항목과 같은 밝기로 낮춘다.

    [Header("문구")]
    public string levelFormat = "Lv {0}"; // {0} = 지금 레벨.

    #endregion
    #region 초기화

    // SkillLoadoutView가 배열 순서를 슬롯 번호로 넘겨 클릭 연결까지 맡긴다.
    public void Bind(int slotIndex, Action<int> onClicked) {
        if (button == null) button = GetComponent<Button>();
        if (button == null || onClicked == null) return;

        int captured = slotIndex; // 클로저가 반복 변수를 붙잡지 않도록 복사.

        // 프리팹에 남아 있던 연결이나 재초기화로 인한 중복 호출을 지운다.
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClicked(captured));
    }

    #endregion
    #region 표시 갱신

    // 슬롯에 들어있는 스킬을 표시한다. skill이 null이면 빈 칸 표기. level 은 skill 이 있을 때만 쓴다.
    public void Set(SkillBase skill, int level, string emptyText) {
        if (skillLabel != null) {
            skillLabel.text = skill != null ? skill.DisplayName : emptyText;
            skillLabel.color = skill != null ? skillTextColor : emptyTextColor;
        }

        if (levelLabel != null) levelLabel.text = skill != null ? string.Format(levelFormat, level) : string.Empty;

        if (iconImage != null) {
            iconImage.sprite = skill != null ? skill.icon : null;

            // 스프라이트가 없는 Image는 흰 사각형으로 보인다. 그렇다고 오브젝트를 끄면 레이아웃이
            // 스킬 유무에 따라 흔들리므로, 자리는 남겨두고 Image만 끈다.
            iconImage.enabled = iconImage.sprite != null;
        }
    }

    public void SetSelected(bool selected) {
        if (focusBorder != null) focusBorder.SetActive(selected);
    }

    #endregion
}
