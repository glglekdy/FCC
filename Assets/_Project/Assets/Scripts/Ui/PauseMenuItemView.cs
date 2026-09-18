using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.UI;

// 일시정지 메뉴 한 줄이 하는 일. PauseMenuView 가 이 값만 보고 분기하므로,
// 줄 순서를 인스펙터에서 바꿔도 동작은 따라오지 않는다 (역할은 여기 붙은 값이 결정한다).
public enum PauseMenuAction {
    Resume,   // 계속하기.
    Settings, // 설정.
    MainMenu, // 메뉴로 돌아가기.
}

// 일시정지 메뉴의 한 줄. 자기 줄의 면 · 포인트 막대 · 글자색만 알고, 지금 골라져 있는지는 밖에서 받아온다.
//
// 줄이 3개로 고정이라 목록 프리팹을 따로 두지 않고 PauseMenu 프리팹 안에 그대로 박아두었다 (MainLobbyItemView 와 같은 이유).
//
// **Prefabs/UI/PauseMenu.prefab 의 Window/Column/Menu/Item_* 에 붙어 있습니다.**
public class PauseMenuItemView : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler {
    #region 인스펙터 변수

    [Header("역할")]
    public PauseMenuAction action; // 이 줄을 고르면 무엇을 하는지.

    [Header("연결")]
    public Image background; // 골라졌을 때만 칠해지는 면. **투명해도 raycastTarget은 켜두세요 — 마우스를 이 이미지가 받습니다.**
    public GameObject focusMarker; // 골라진 줄 왼쪽의 3px 포인트 막대. 켜고 끄기만 한다.
    public TMP_Text label; // "계속하기" 같은 항목 이름. 고정 문구라 프리팹에 직접 적혀 있다.

    // 기본값은 UiTheme 토큰이지만, 실제로 화면에 나가는 값은 프리팹에 박힌 쪽이다.
    // UiTheme 을 고쳤다면 Tools ▸ FCC ▸ UI ▸ Apply Theme Colors 을 한 번 돌려야 프리팹까지 맞춰진다.
    [Header("색상")]
    public Color normalBackground = UiTheme.Transparent; // 평소에는 칠하지 않는다 (뒤의 바탕이 그대로 비쳐야 한다).
    public Color selectedBackground = UiTheme.With(UiTheme.PanelRaised, 0.75f); // Figma 「일시중단 메뉴」의 FocusedMark.
    // Figma 에는 세 줄이 같은 밝기로 그려져 있지만 그건 한 순간을 찍은 정지 화면이다. 고른 줄을 면과 막대에 더해
    // 밝기로도 가르는 것이 아트 디렉션의 규칙(색을 늘리지 않고 밝기 단계로 구분)이라 평소 줄은 한 단계 낮춘다.
    public Color labelColor = UiTheme.TextBody;
    public Color selectedLabelColor = UiTheme.TextHigh;
    public Color lockedColor = UiTheme.TextDim; // 설정창이 연결되지 않은 씬의 [설정]처럼 지금 열 수 없는 줄.

    #endregion
    #region 런타임 변수

    Action<PauseMenuItemView> onHovered;
    Action<PauseMenuItemView> onClicked;

    // 프리팹에 적혀 있던 원래 문구. 번역이 비어 있을 때 쓸 대체 문구다.
    // RefreshLabel 이 label.text 를 덮어쓰므로, 여기에 붙잡아 두지 않으면 두 번째 호출부터 대체 문구가 사라진다.
    string defaultLabel;

    // 잠긴 줄은 커서가 지나가도 골라지지 않고 클릭도 먹지 않는다.
    public bool IsUnlocked { get; private set; } = true;

    #endregion
    #region 초기화

    // 프리팹 안에 이미 놓여 있는 줄들이라 Instantiate 시점이 따로 없다. PauseMenuView 가 Awake 에서 한 번 물린다.
    public void Bind(Action<PauseMenuItemView> hovered, Action<PauseMenuItemView> clicked) {
        onHovered = hovered;
        onClicked = clicked;

        // 항목 이름은 프리팹에 한국어로 직접 적혀 있었다. action이 곧 어느 줄인지를 정하므로
        // 여기서 키를 골라 덮어써야 새 프리팹을 만들지 않고도 번역이 붙는다.
        if (label != null) defaultLabel = label.text;
        RefreshLabel();
    }

    // 항목 이름을 지금 언어로 다시 적는다. 언어가 바뀌면 PauseMenuView 가 다시 부른다.
    // Bind 에서 한 번만 적으면 설정에서 언어를 바꿔도 이 줄들만 옛 언어로 남는다(MainLobbyItemView 와 같은 이유).
    public void RefreshLabel() {
        if (label == null) return;
        label.text = LocalizationText.Resolve(LabelKeyFor(action), defaultLabel);
    }

    static LocalizedString LabelKeyFor(PauseMenuAction action) {
        return action switch {
            PauseMenuAction.Resume => new LocalizedString("Ui", "pause.resume"),
            PauseMenuAction.Settings => new LocalizedString("Ui", "common.settings"),
            PauseMenuAction.MainMenu => new LocalizedString("Ui", "pause.return_to_menu"),
            _ => null,
        };
    }

    #endregion
    #region 표시 갱신

    public void SetSelected(bool selected) {
        // 잠긴 줄에 커서가 머무는 일은 없지만, 밖에서 잘못 부르더라도 골라진 것처럼 보이지 않게 막는다.
        bool on = selected && IsUnlocked;

        if (background != null) background.color = on ? selectedBackground : normalBackground;
        if (focusMarker != null) focusMarker.SetActive(on);
        if (label != null) label.color = IsUnlocked ? (on ? selectedLabelColor : labelColor) : lockedColor;
    }

    // 잠긴 줄도 지우지 않고 흐리게 남긴다. 메뉴 줄 위치가 씬마다 달라지면 손이 기억한 자리가 어긋난다.
    public void SetUnlocked(bool unlocked) {
        IsUnlocked = unlocked;
        SetSelected(false);
    }

    #endregion
    #region 입력 처리

    // 커서가 올라오면 그 줄이 곧 선택이다. 키보드 커서와 마우스 커서를 따로 두면 Enter 를 눌렀을 때
    // 어느 쪽이 실행될지 헷갈리므로 하나로 합쳤다 (MainLobbyItemView 와 같은 방식).
    public void OnPointerEnter(PointerEventData eventData) {
        if (!IsUnlocked) return;
        if (onHovered != null) onHovered(this);
    }

    public void OnPointerClick(PointerEventData eventData) {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (!IsUnlocked) return;
        if (onClicked != null) onClicked(this);
    }

    #endregion
}
