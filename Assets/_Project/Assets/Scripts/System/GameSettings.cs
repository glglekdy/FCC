using System;
using UnityEngine;
using UnityEngine.Localization.Settings;

// 게임 설정 값 한 벌. 세이브 파일이 아니라 PlayerPrefs 에 따로 담는다 —
// 설정은 세이브 슬롯이 아니라 이 PC 에 붙는 값이라, 이어하기를 지우거나 새로 시작해도 남아야 하기 때문이다.
[Serializable]
public class SettingsData {
    // LocalizationSettings.AvailableLocales 의 순번. -1 이면 "아직 고른 적 없음" 이라 원문 언어(ko)로 시작한다.
    // 0 을 기본값으로 두었더니 목록 첫 항목이 en 이어서 새로 설치한 사람이 영어로 시작했다.
    public int languageIndex = -1;

    public int masterVolume = 80;
    public int bgmVolume = 70;
    public int sfxVolume = 85;

    public int screenShake = 100; // 흔들림 세기 %. 0이면 카메라가 아예 안 흔들린다.
    public bool damageNumbers = true;

    public int screenModeIndex; // 0 전체 화면 · 1 테두리 없는 창 · 2 창 모드.
    public int resolutionIndex = -1; // -1이면 "아직 고른 적 없음" 이라 현재 해상도를 그대로 쓴다.
    public int frameLimitIndex = 2; // 0:60 · 1:120 · 2:144 · 3:무제한.
    public bool vSync = true;

    // 키 이름을 그대로 담는다. Input System 리바인딩이 붙기 전까지는 화면 표시용이다.
    public string[] keyboardBindings;
    public string[] padBindings;

    public SettingsData Clone() {
        SettingsData copy = (SettingsData)MemberwiseClone();
        copy.keyboardBindings = keyboardBindings == null ? null : (string[])keyboardBindings.Clone();
        copy.padBindings = padBindings == null ? null : (string[])padBindings.Clone();
        return copy;
    }
}

// 설정 값의 보관·저장·실제 반영을 맡는다.
//
// 화면(SettingsPanelView)은 Draft 만 만지고, 실제 시스템에는 손대지 않는다. 설계상 「적용」을 눌러야
// 확정되는 구조이기 때문이다(해상도처럼 되돌리기 어려운 항목이 섞여 있어 즉시 반영하면 위험하다).
//   편집 중  → Draft
//   적용     → Draft 를 Current 로 확정하고 저장 + 시스템 반영
//   취소     → Current 를 Draft 로 되돌림
//   기본값   → 새 SettingsData 를 Draft 로
//
// 붙일 시스템이 아직 없는 항목(BGM·효과음 볼륨, 키 리바인딩)은 값만 보관한다. 나중에 AudioMixer 나
// 리바인딩이 생기면 ApplyToSystems() 안의 표시해둔 자리에 연결하면 된다.
public static class GameSettings {
    #region 상수

    const string PrefsKey = "FCC_Settings";

    // 화면에 보이는 문구와 실제 값의 대응표. 선택형 행(< 값 >)이 이 순서를 그대로 쓴다.
    public static readonly string[] ScreenModeLabels = { "전체 화면", "테두리 없는 창", "창 모드" };
    static readonly FullScreenMode[] ScreenModes = {
        FullScreenMode.ExclusiveFullScreen,
        FullScreenMode.FullScreenWindow,
        FullScreenMode.Windowed,
    };

    public static readonly string[] FrameLimitLabels = { "60", "120", "144", "무제한" };
    static readonly int[] FrameLimits = { 60, 120, 144, -1 };

    // 컨트롤 탭의 10줄. 화면에 보이는 이름과 초기 키가 여기 한 곳에 있어야, 프리팹 빌더와 실행 중 표시가 어긋나지 않는다.
    // **지금은 표시용 문구입니다** — Client.inputactions 의 실제 바인딩과 맞춰 적어두었고,
    // Input System 리바인딩이 붙으면 이 표가 초기값 역할을 하게 된다.
    public static readonly string[] ActionNames = {
        "왼쪽 이동", "오른쪽 이동", "점프", "공격", "상호작용", "스킬 1", "스킬 2", "스킬 3", "대사 넘김", "일시정지",
    };
    public static readonly string[] DefaultKeyboard = {
        "A", "D", "Space", "마우스 좌클릭", "E", "1", "2", "3", "Space", "ESC",
    };
    public static readonly string[] DefaultPad = {
        "L스틱 왼쪽", "L스틱 오른쪽", "A 버튼", "X 버튼", "Y 버튼", "LB", "RB", "RT", "A 버튼", "Start",
    };

    #endregion
    #region 상태

    public static SettingsData Current { get; private set; }
    public static SettingsData Draft { get; private set; }

    // 해상도 목록은 PC 마다 다르므로 상수로 둘 수 없다. 처음 물어볼 때 한 번 받아둔다.
    static Resolution[] resolutions;

    public static Resolution[] Resolutions {
        get {
            if (resolutions == null || resolutions.Length == 0) resolutions = Screen.resolutions;
            return resolutions;
        }
    }

    public static string ResolutionLabel(int index) {
        Resolution[] list = Resolutions;
        if (list.Length == 0) return Screen.width + " x " + Screen.height;

        index = Mathf.Clamp(index, 0, list.Length - 1);
        Resolution r = list[index];

        // 에디터에서는 refreshRateRatio 가 0/0 으로 와서 value 가 NaN 이 된다. 그대로 찍으면 "NaNHz" 가 화면에 나온다.
        double hz = r.refreshRateRatio.value;
        if (double.IsNaN(hz) || hz <= 0d) return r.width + " x " + r.height;

        return r.width + " x " + r.height + " (" + hz.ToString("0") + "Hz)";
    }

    // 저장된 값이 없을 때의 출발점.
    //
    // Screen.width/height 가 아니라 Screen.currentResolution 과 비교해야 한다 — 앞의 둘은 창(에디터에서는 게임 뷰)의
    // 크기라, 창 모드나 에디터에서는 모니터 해상도와 전혀 다른 값이 나온다. 실제로 에디터에서 640×480 이 잡혔다.
    public static int CurrentResolutionIndex() {
        Resolution[] list = Resolutions;
        if (list.Length == 0) return 0;

        Resolution now = Screen.currentResolution;
        for (int i = 0; i < list.Length; i++) {
            if (list[i].width == now.width && list[i].height == now.height) return i;
        }

        return list.Length - 1; // 목록은 오름차순이라 못 찾으면 가장 큰 것이 안전하다.
    }

    #endregion
    #region 불러오기 · 저장

    // 화면을 열기 전에 한 번 불러둔다. 여러 번 불러도 안전하다.
    public static void Load() {
        if (Current != null) return;

        string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
        Current = new SettingsData();

        if (!string.IsNullOrEmpty(json)) {
            try {
                JsonUtility.FromJsonOverwrite(json, Current);
            } catch (Exception e) {
                // 설정이 깨졌다고 게임을 못 켜면 안 된다. 기본값으로 시작하고 알리기만 한다.
                Debug.LogWarning("[GameSettings] 저장된 설정을 읽지 못해 기본값으로 시작합니다 — " + e.Message);
                Current = new SettingsData();
            }
        }

        if (Current.resolutionIndex < 0) Current.resolutionIndex = CurrentResolutionIndex();
        if (Current.languageIndex < 0) Current.languageIndex = SourceLanguageIndex();
        Normalize(Current);

        Draft = Current.Clone();
    }

    // 아직 언어를 고른 적이 없을 때의 출발점. 원문 언어인 한국어를 먼저 찾고, 없으면 목록의 첫 항목을 쓴다.
    // AvailableLocales 의 순서는 알파벳순이라 en 이 앞에 오므로, 0 을 그냥 쓰면 한국어 게임이 영어로 시작한다.
    static int SourceLanguageIndex() {
        var locales = LocalizationSettings.AvailableLocales;
        if (locales == null) return 0;

        for (int i = 0; i < locales.Locales.Count; i++) {
            if (locales.Locales[i].Identifier.Code == "ko") return i;
        }
        return 0;
    }

    // 키 목록이 비어 있거나(처음 실행) 길이가 안 맞을 때(동작을 새로 추가했을 때) 기본값으로 메운다.
    // 이걸 안 하면 컨트롤 탭의 칸이 통째로 빈 채로 나온다.
    static void Normalize(SettingsData data) {
        data.keyboardBindings = Fill(data.keyboardBindings, DefaultKeyboard);
        data.padBindings = Fill(data.padBindings, DefaultPad);
    }

    static string[] Fill(string[] saved, string[] defaults) {
        string[] result = new string[defaults.Length];

        for (int i = 0; i < defaults.Length; i++) {
            bool hasSaved = saved != null && i < saved.Length && !string.IsNullOrEmpty(saved[i]);
            result[i] = hasSaved ? saved[i] : defaults[i];
        }

        return result;
    }

    static void Save() {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Current));
        PlayerPrefs.Save();
    }

    #endregion
    #region 적용 · 취소 · 기본값

    public static void Apply() {
        Load();

        Current = Draft.Clone();
        Save();
        ApplyToSystems();
    }

    public static void Cancel() {
        Load();
        Draft = Current.Clone();
    }

    public static void ResetToDefaults() {
        Load();

        Draft = new SettingsData();
        Draft.resolutionIndex = CurrentResolutionIndex(); // 해상도 기본값은 "지금 쓰는 것" 이 맞다.
        Draft.languageIndex = SourceLanguageIndex();
        Normalize(Draft);
    }

    // 게임이 켜질 때 저장된 설정을 자동으로 반영한다.
    //
    // 씬 어딘가의 컴포넌트가 부르게 하지 않는 이유: 설정은 어느 씬에서 시작하든(메인 메뉴든 테스트 씬이든)
    // 똑같이 적용돼야 하는데, 그러려면 모든 씬에 같은 오브젝트를 놓아야 하고 하나만 빠뜨려도 조용히 어긋난다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void ApplyOnBoot() {
        Load();
        ApplyToSystems();
    }

    #endregion
    #region 시스템 반영

    static void ApplyToSystems() {
        ApplyLanguage();
        ApplyAudio();
        ApplyPresentation();
        ApplyGraphics();
    }

    static void ApplyLanguage() {
        var locales = LocalizationSettings.AvailableLocales;
        if (locales == null || locales.Locales.Count == 0) return;

        int index = Mathf.Clamp(Current.languageIndex, 0, locales.Locales.Count - 1);
        LocalizationSettings.SelectedLocale = locales.Locales[index];
    }

    static void ApplyAudio() {
        // 마스터는 AudioListener 로 바로 줄일 수 있다. 배경음·효과음을 따로 나누려면 AudioMixer 가 필요한데
        // 아직 프로젝트에 믹서가 없어서 값만 보관한다.
        // **AudioMixer 를 만들면 여기서 exposed parameter 두 개를 설정하면 됩니다.**
        AudioListener.volume = Mathf.Clamp01(Current.masterVolume / 100f);
    }

    static void ApplyPresentation() {
        // 배율이라 원본 수치를 건드리지 않는다. 인스펙터에서 맞춰둔 감각을 설정이 덮어쓰면 안 되기 때문이다.
        HitFeedback.ShakeScale = Mathf.Clamp01(Current.screenShake / 100f);

        // 데미지 수치 표시는 붙일 곳(DamagePopup)이 아직 없어 값만 보관한다.
        // **DamagePopup 이 생기면 여기서 표시 여부를 넘기면 됩니다.**
    }

    static void ApplyGraphics() {
        QualitySettings.vSyncCount = Current.vSync ? 1 : 0;

        // vSync 가 켜져 있으면 targetFrameRate 는 무시된다. 굳이 끄지 않고 값만 넣어둔다
        // (사용자가 vSync 를 끄는 순간 바로 먹히도록).
        Application.targetFrameRate = FrameLimits[Mathf.Clamp(Current.frameLimitIndex, 0, FrameLimits.Length - 1)];

        Resolution[] list = Resolutions;
        if (list.Length == 0) return;

        Resolution target = list[Mathf.Clamp(Current.resolutionIndex, 0, list.Length - 1)];
        FullScreenMode mode = ScreenModes[Mathf.Clamp(Current.screenModeIndex, 0, ScreenModes.Length - 1)];

        // 같은 값을 다시 넣으면 화면이 한 번 깜빡이므로 달라졌을 때만 건드린다.
        if (Screen.width != target.width || Screen.height != target.height || Screen.fullScreenMode != mode) {
            Screen.SetResolution(target.width, target.height, mode, target.refreshRateRatio);
        }
    }

    #endregion
}
