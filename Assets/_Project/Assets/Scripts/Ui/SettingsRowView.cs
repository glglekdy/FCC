using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 설정창의 한 줄이 어떤 값을 만지는지. 프리팹 인스펙터에서 고르므로, 줄을 옮기거나 새로 넣어도
// 스크립트를 고칠 필요가 없다.
public enum SettingsField {
    Language,
    MasterVolume,
    BgmVolume,
    SfxVolume,
    ScreenShake,
    DamageNumbers,
    ScreenMode,
    Resolution,
    FrameLimit,
    VSync,
    Keybind, // 키 재지정 줄. 어떤 동작인지는 SettingsKeybindRow 의 순번이 정한다.
}

// SettingsField 와 GameSettings.Draft 사이를 잇는 유일한 통로.
//
// 줄마다 Draft 의 필드를 직접 만지게 두면 항목이 늘 때마다 여러 파일을 고쳐야 한다. 대응표를 여기 한 곳에
// 모아두면 설정 항목을 추가할 때 이 파일과 프리팹만 손대면 된다.
public static class SettingsAccess {
    #region 값 읽고 쓰기

    public static int GetInt(SettingsField field) {
        SettingsData d = GameSettings.Draft;

        switch (field) {
            case SettingsField.Language: return d.languageIndex;
            case SettingsField.MasterVolume: return d.masterVolume;
            case SettingsField.BgmVolume: return d.bgmVolume;
            case SettingsField.SfxVolume: return d.sfxVolume;
            case SettingsField.ScreenShake: return d.screenShake;
            case SettingsField.ScreenMode: return d.screenModeIndex;
            case SettingsField.Resolution: return d.resolutionIndex;
            case SettingsField.FrameLimit: return d.frameLimitIndex;
            default: return 0;
        }
    }

    public static void SetInt(SettingsField field, int value) {
        SettingsData d = GameSettings.Draft;

        switch (field) {
            case SettingsField.Language: d.languageIndex = value; break;
            case SettingsField.MasterVolume: d.masterVolume = value; break;
            case SettingsField.BgmVolume: d.bgmVolume = value; break;
            case SettingsField.SfxVolume: d.sfxVolume = value; break;
            case SettingsField.ScreenShake: d.screenShake = value; break;
            case SettingsField.ScreenMode: d.screenModeIndex = value; break;
            case SettingsField.Resolution: d.resolutionIndex = value; break;
            case SettingsField.FrameLimit: d.frameLimitIndex = value; break;
        }
    }

    public static bool GetBool(SettingsField field) {
        SettingsData d = GameSettings.Draft;

        switch (field) {
            case SettingsField.DamageNumbers: return d.damageNumbers;
            case SettingsField.VSync: return d.vSync;
            default: return false;
        }
    }

    public static void SetBool(SettingsField field, bool value) {
        SettingsData d = GameSettings.Draft;

        switch (field) {
            case SettingsField.DamageNumbers: d.damageNumbers = value; break;
            case SettingsField.VSync: d.vSync = value; break;
        }
    }

    #endregion
    #region 선택지 목록

    // 언어와 해상도는 PC 마다 달라 상수로 둘 수 없다. 그래서 개수와 문구를 물어보는 형태로 만들었다.
    public static int OptionCount(SettingsField field) {
        switch (field) {
            case SettingsField.Language: {
                var locales = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales;
                return locales == null ? 1 : Mathf.Max(1, locales.Locales.Count);
            }
            case SettingsField.Resolution: return Mathf.Max(1, GameSettings.Resolutions.Length);
            case SettingsField.ScreenMode: return GameSettings.ScreenModeLabels.Length;
            case SettingsField.FrameLimit: return GameSettings.FrameLimitLabels.Length;
            default: return 1;
        }
    }

    public static string OptionLabel(SettingsField field, int index) {
        switch (field) {
            case SettingsField.Language: {
                var locales = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales;
                if (locales == null || locales.Locales.Count == 0) return "한국어";

                index = Mathf.Clamp(index, 0, locales.Locales.Count - 1);
                // 영어 이름(Korean)이 아니라 그 언어로 쓴 이름(한국어)을 보여줘야 고를 수 있다.
                var locale = locales.Locales[index];
                string native = locale.Identifier.CultureInfo != null
                    ? locale.Identifier.CultureInfo.NativeName : locale.LocaleName;
                return string.IsNullOrEmpty(native) ? locale.LocaleName : native;
            }
            case SettingsField.Resolution: return GameSettings.ResolutionLabel(index);
            case SettingsField.ScreenMode: return GameSettings.ScreenModeLabel(index);
            case SettingsField.FrameLimit: return GameSettings.FrameLimitLabel(index);
            default: return string.Empty;
        }
    }

    #endregion
}

// 설정창의 한 줄. 배경·포커스 표시·라벨처럼 모든 줄이 똑같이 하는 일만 여기서 맡고,
// 값을 어떻게 보여주고 어떻게 바꾸는지는 파생 클래스가 정한다.
//
// 마우스도 여기서 받는다. 줄 배경 이미지가 커서를 받고, 커서가 올라오면 그 줄이 곧 포커스다 — 키보드 포커스와
// 마우스 포커스를 따로 두면 Enter 를 눌렀을 때 어느 줄이 반응할지 헷갈리기 때문이다(메인 로비 메뉴와 같은 방식).
//
// **Prefabs/UI/SettingsPanel.prefab 의 각 Row 오브젝트에 파생 클래스가 붙어 있습니다.**
public abstract class SettingsRowView : MonoBehaviour,
    IPointerEnterHandler, IPointerDownHandler, IDragHandler, IPointerClickHandler {
    #region 인스펙터 변수

    [Header("역할")]
    public SettingsField field; // 이 줄이 만지는 설정 항목.

    [Header("연결")]
    public Image background; // 포커스가 왔을 때만 칠해지는 줄 배경. **투명해도 raycastTarget 은 켜두세요 — 마우스를 이 이미지가 받습니다.**
    public GameObject focusMarker; // 줄 왼쪽의 3px 세로 막대. 켜고 끄기만 한다.
    public TMP_Text label; // 항목 이름. 고정 문구라 프리팹에 직접 적혀 있다.

    [Header("색상")]
    public Color normalBackground = UiTheme.Transparent;
    public Color focusedBackground = UiTheme.PanelRaised;
    public Color labelColor = UiTheme.TextBody;
    public Color focusedLabelColor = UiTheme.TextHigh; // 포커스된 줄만 라벨을 한 단계 밝힌다.

    #endregion
    #region 상태

    public bool IsFocused { get; private set; }

    // 포커스를 다른 줄에 넘기면 안 되는 상태인지. 키 재지정 대기 중에 커서가 스치기만 해도 대기가 풀리면 안 된다.
    public virtual bool HoldsFocus => false;

    Action<SettingsRowView> onHovered;
    Action<SettingsRowView> onPressed;

    #endregion
    #region 초기화

    // SettingsPanelView 가 Awake 에서 한 번 물린다. 프리팹 안에 이미 놓여 있는 줄들이라 생성 시점이 따로 없다.
    // 커서가 스친 것(hovered)과 누른 것(pressed)을 나눈 이유: 스친 것은 HoldsFocus 에 막히지만, 누른 것은 사용자가
    // 분명히 그 줄을 고른 것이라 대기 중인 줄이 있어도 넘어가야 한다.
    public void Bind(Action<SettingsRowView> hovered, Action<SettingsRowView> pressed) {
        onHovered = hovered;
        onPressed = pressed;
    }

    #endregion
    #region 표시

    public virtual void SetFocused(bool on) {
        IsFocused = on;

        if (background != null) background.color = on ? focusedBackground : normalBackground;
        if (focusMarker != null) focusMarker.SetActive(on);
        if (label != null) label.color = on ? focusedLabelColor : labelColor;
    }

    // Draft 의 현재 값을 화면에 다시 그린다. 탭을 열 때와 값이 바뀔 때마다 불린다.
    public abstract void Refresh();

    #endregion
    #region 입력

    // 좌우 입력. dir 은 -1 또는 +1 이며, 값이 실제로 바뀌었으면 true 를 돌려준다
    // (바뀐 경우에만 효과음을 내거나 「적용」 버튼을 활성화하기 위해서다).
    public abstract bool Adjust(int dir);

    // Enter 입력. 대부분의 줄은 할 일이 없고, 키 재지정 줄만 여기서 입력 대기로 들어간다.
    public virtual bool Submit() {
        return false;
    }

    // Backspace 입력. 키 재지정 줄만 할당을 비우는 데 쓴다.
    public virtual bool Clear() {
        return false;
    }

    #endregion
    #region 마우스

    public void OnPointerEnter(PointerEventData eventData) {
        if (onHovered != null) onHovered(this);
    }

    public void OnPointerDown(PointerEventData eventData) {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (onPressed != null) onPressed(this);
        PointerPressed(eventData);
    }

    public void OnDrag(PointerEventData eventData) {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        PointerDragged(eventData);
    }

    public void OnPointerClick(PointerEventData eventData) {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        PointerClicked(eventData);
        UiAudio.PlayClick();
    }

    // 파생 클래스가 필요한 것만 골라 덮어쓴다. 슬라이더는 누르는 순간·끄는 동안, 나머지는 뗐을 때 반응한다.
    protected virtual void PointerPressed(PointerEventData eventData) { }
    protected virtual void PointerDragged(PointerEventData eventData) { }
    protected virtual void PointerClicked(PointerEventData eventData) { }

    // 커서가 target 의 가로 범위(좌우로 slack 만큼 넓힌) 안에 있는지. 세로는 보지 않는다 — 이벤트가 이미 이 줄에서
    // 왔으니 줄 높이 안이고, 막대(4px)·화살표 글자(20px)는 너무 얇아 정확히 맞히라고 할 수 없다.
    protected static bool IsWithinX(RectTransform target, PointerEventData eventData, float slack) {
        if (target == null) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(target, eventData.position, eventData.pressEventCamera, out Vector2 p)) return false;

        Rect r = target.rect;
        return p.x >= r.xMin - slack && p.x <= r.xMax + slack;
    }

    #endregion
}
