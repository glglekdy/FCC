using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 설정창의 한 줄이 어떤 값을 만지는지. 프리팹 인스펙터에서 고르므로, 줄을 옮기거나 새로 넣어도
// 스크립트를 고칠 필요가 없다.
public enum SettingsField {
    Language,
    MasterVolume,
    BgmVolume,
    SfxVolume,
    DialogueSpeed,
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
            case SettingsField.DialogueSpeed: return d.dialogueSpeedIndex;
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
            case SettingsField.DialogueSpeed: d.dialogueSpeedIndex = value; break;
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
            case SettingsField.DialogueSpeed: return GameSettings.DialogueSpeedLabels.Length;
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
            case SettingsField.DialogueSpeed: return Pick(GameSettings.DialogueSpeedLabels, index);
            case SettingsField.ScreenMode: return Pick(GameSettings.ScreenModeLabels, index);
            case SettingsField.FrameLimit: return Pick(GameSettings.FrameLimitLabels, index);
            default: return string.Empty;
        }
    }

    static string Pick(string[] labels, int index) {
        if (labels == null || labels.Length == 0) return string.Empty;
        return labels[Mathf.Clamp(index, 0, labels.Length - 1)];
    }

    #endregion
}

// 설정창의 한 줄. 배경·포커스 표시·라벨처럼 모든 줄이 똑같이 하는 일만 여기서 맡고,
// 값을 어떻게 보여주고 어떻게 바꾸는지는 파생 클래스가 정한다.
//
// **Prefabs/UI/SettingsPanel.prefab 의 각 Row 오브젝트에 파생 클래스가 붙어 있습니다.**
public abstract class SettingsRowView : MonoBehaviour {
    #region 인스펙터 변수

    [Header("역할")]
    public SettingsField field; // 이 줄이 만지는 설정 항목.

    [Header("연결")]
    public Image background; // 포커스가 왔을 때만 칠해지는 줄 배경.
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

    #endregion
}
