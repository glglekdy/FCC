using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.UI;

// 메인 로비 메뉴 한 줄이 하는 일. MainLobbyView가 이 값만 보고 분기하므로,
// 항목 순서를 인스펙터에서 바꿔도 동작은 따라오지 않는다 (역할은 여기 붙은 값이 결정한다).
public enum MainLobbyAction {
    Continue,   // 이어하기.
    NewGame,    // 새로 시작.
    MemoryRoom, // 기억의 방 — 수집 갤러리.
    Settings,   // 설정.
    Quit,       // 종료.
}

// 메인 로비(커튼콜 정면)의 메뉴 한 줄. 자기 줄의 문구·색·테두리만 알고, 지금 골라져 있는지는 밖에서 받아온다.
//
// 로비 메뉴는 항목 수가 5개로 고정이라 목록 프리팹을 따로 두지 않고 로비 프리팹 안에 5개를 그대로 박아두었다
// (개수가 변하는 SkillLoadoutRow·ObjectiveItemRow 와 다른 점). 대신 줄 하나의 표시 책임은 여기로 떼어둔다.
//
// **Prefabs/UI/MainLobby.prefab 의 Menu/Item0~4 에 붙어 있습니다.**
public class MainLobbyItemView : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler {
    #region 인스펙터 변수

    [Header("역할")]
    public MainLobbyAction action; // 이 줄을 고르면 무엇을 하는지.

    [Header("연결")]
    public Image background; // 골라졌을 때만 칠해지는 배경. **투명해도 raycastTarget은 켜두세요 — 마우스를 이 이미지가 받습니다.**
    public GameObject frame; // 골라진 줄을 감싸는 굵은 테두리. 켜고 끄기만 한다. 비워도 된다(지금 로비 디자인에는 없다).
    public GameObject underline; // 항목 사이 구분선. 비워도 된다(지금 로비 디자인에는 없다).
    public TMP_Text label; // "이어하기" 같은 항목 이름. 고정 문구라 프리팹에 직접 적혀 있다.
    public TMP_Text suffixLabel; // "CH1 · 3일차" 처럼 오른쪽에 붙는 보조 표기. 비워도 된다(지금 로비 디자인에는 없다).

    // 기본값은 UiTheme 토큰이지만, 실제로 화면에 나가는 값은 프리팹에 박힌 쪽이다.
    // UiTheme 을 고쳤다면 Tools ▸ FCC ▸ UI ▸ Apply Theme Colors 을 한 번 돌려야 프리팹까지 맞춰진다.
    [Header("색상")]
    public Color normalBackground = UiTheme.Transparent; // 평소에는 칠하지 않는다 (무대 배경이 그대로 비쳐야 한다).
    public Color selectedBackground = UiTheme.With(UiTheme.PanelRaised, 0.92f);
    public Color labelColor = UiTheme.TextBody;
    public Color selectedLabelColor = UiTheme.TextHigh;
    public Color suffixColor = UiTheme.TextMuted;
    public Color lockedColor = UiTheme.TextDim; // 아직 못 여는 항목(기억의 방 등)은 통째로 흐리게.

    [Header("선택 표시")]
    // 골라진 줄을 굵기로도 구분할지. Figma MainLobby_new 는 테두리 없이 글자 밝기만으로 고른 줄을 보여줘서 기본은 끈다.
    // 대표 폰트가 이미 Bold 라서 켜면 SDF 를 한 번 더 부풀린 가짜 굵기가 된다.
    public bool boldWhenSelected = false;

    #endregion
    #region 런타임 변수

    Action<MainLobbyItemView> onHovered;
    Action<MainLobbyItemView> onClicked;

    // 프리팹에 적혀 있던 원래 문구. 번역이 비어 있을 때 쓸 대체 문구다.
    // RefreshLabel 이 label.text 를 덮어쓰므로, 여기에 붙잡아 두지 않으면 두 번째 호출부터 대체 문구가 사라진다.
    string defaultLabel;

    // 잠긴 줄은 커서가 지나가도 골라지지 않고 클릭도 먹지 않는다.
    public bool IsUnlocked { get; private set; } = true;

    #endregion
    #region 초기화

    // 생성 직후가 아니라 MainLobbyView가 Awake에서 한 번 물린다. 프리팹 안에 이미 놓여 있는 줄들이라
    // Instantiate 시점이 따로 없기 때문.
    public void Bind(Action<MainLobbyItemView> hovered, Action<MainLobbyItemView> clicked) {
        onHovered = hovered;
        onClicked = clicked;

        // 항목 이름은 프리팹에 한국어로 직접 적혀 있었다. action이 곧 어느 줄인지를 정하므로
        // 여기서 키를 골라 덮어써야 새 프리팹을 만들지 않고도 번역이 붙는다.
        if (label != null) defaultLabel = label.text;
        RefreshLabel();
    }

    // 항목 이름을 지금 언어로 다시 적는다. 언어가 바뀌면 MainLobbyView 가 다시 부른다.
    //
    // Bind 에서 한 번만 적으면 설정에서 언어를 바꿔도 이 줄들만 옛 언어로 남는다(프리팹에 붙은
    // LocalizeStringEvent 는 알아서 따라오지만, 코드가 적는 문구는 다시 적어주지 않으면 그대로다).
    // 부팅 직후에도 필요하다 — 저장된 언어를 반영하는 GameSettings.ApplyOnBoot 은 AfterSceneLoad 라,
    // 이 Awake 보다 늦게 돌면 Bind 에서 적은 문구가 시작 언어(보통 한국어) 그대로 남는다.
    public void RefreshLabel() {
        if (label == null) return;
        label.text = LocalizationText.Resolve(LabelKeyFor(action), defaultLabel);
    }

    static LocalizedString LabelKeyFor(MainLobbyAction action) {
        return action switch {
            MainLobbyAction.Continue => new LocalizedString("Ui", "lobby.continue"),
            MainLobbyAction.NewGame => new LocalizedString("Ui", "lobby.new_game"),
            MainLobbyAction.MemoryRoom => new LocalizedString("Ui", "lobby.memory_room"),
            MainLobbyAction.Settings => new LocalizedString("Ui", "common.settings"),
            MainLobbyAction.Quit => new LocalizedString("Ui", "lobby.quit"),
            _ => null,
        };
    }

    #endregion
    #region 표시 갱신

    public void SetSelected(bool selected) {
        // 잠긴 줄에 커서가 머무는 일은 없지만, 밖에서 잘못 부르더라도 골라진 것처럼 보이지 않게 막는다.
        bool on = selected && IsUnlocked;

        if (background != null) background.color = on ? selectedBackground : normalBackground;
        if (frame != null) frame.SetActive(on);

        if (label != null) {
            label.color = IsUnlocked ? (on ? selectedLabelColor : labelColor) : lockedColor;
            label.fontStyle = on && boldWhenSelected ? FontStyles.Bold : FontStyles.Normal;
        }

        if (suffixLabel != null) suffixLabel.color = IsUnlocked ? suffixColor : lockedColor;
    }

    // 보조 표기를 갈아끼운다. 빈 문자열을 넘기면 라벨 오브젝트째 꺼서 줄 높이만 남긴다.
    public void SetSuffix(string text) {
        if (suffixLabel == null) return;

        suffixLabel.text = text;
        suffixLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    // 잠금 여부. 잠긴 줄도 화면에서 지우지 않고 흐리게 남긴다 — 앞으로 무엇이 생길지 보여주는 편이 낫기 때문.
    public void SetUnlocked(bool unlocked) {
        IsUnlocked = unlocked;
        SetSelected(false);
    }

    #endregion
    #region 입력 처리

    // 커서가 올라오면 그 줄이 곧 선택이다. 키보드 커서와 마우스 커서를 따로 두면 Enter를 눌렀을 때
    // 어느 쪽이 실행될지 헷갈리므로 하나로 합쳤다.
    public void OnPointerEnter(PointerEventData eventData) {
        if (!IsUnlocked) return;
        onHovered?.Invoke(this);
    }

    public void OnPointerClick(PointerEventData eventData) {
        if (!IsUnlocked) return;
        onClicked?.Invoke(this);
    }

    #endregion
}
