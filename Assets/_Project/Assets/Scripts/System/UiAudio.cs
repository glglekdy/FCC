using FMODUnity;
using UnityEngine;

// UI 공통 효과음(호버 · 클릭)을 재생하는 싱글턴. 화면마다 FMOD 이벤트를 따로 들고 있으면 이벤트 경로가
// 여러 파일에 흩어지므로, 재생만 한 곳(PlayHover · PlayClick)에서 맡는다.
public class UiAudio : MonoBehaviour {
    #region 인스펙터 변수

    [Header("이벤트")]
    [EventRef] public string hoverEvent; // event:/SFX/Ui/Setting_Hover
    [EventRef] public string clickEvent; // event:/SFX/Ui/Setting_Click
    [EventRef] public string equipEvent; // event:/SFX/Ui/Equip
    [EventRef] public string unequipEvent; // event:/SFX/Ui/Unequip

    #endregion
    #region 싱글턴

    public static UiAudio Instance { get; private set; }

    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject); // 에디트 모드에서 부르면 씬 밖(고스트 씬)으로 옮겨져 저장되지 않는다.
    }

    #endregion
    #region 재생

    public static void PlayHover() {
        if (Instance == null || string.IsNullOrEmpty(Instance.hoverEvent)) return;
        RuntimeManager.PlayOneShot(Instance.hoverEvent);
    }

    public static void PlayClick() {
        if (Instance == null || string.IsNullOrEmpty(Instance.clickEvent)) return;
        RuntimeManager.PlayOneShot(Instance.clickEvent);
    }

    public static void PlayEquip() {
        if (Instance == null || string.IsNullOrEmpty(Instance.equipEvent)) return;
        RuntimeManager.PlayOneShot(Instance.equipEvent);
    }

    public static void PlayUnequip() {
        if (Instance == null || string.IsNullOrEmpty(Instance.unequipEvent)) return;
        RuntimeManager.PlayOneShot(Instance.unequipEvent);
    }

    #endregion
}
