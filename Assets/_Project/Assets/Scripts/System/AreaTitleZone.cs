using UnityEngine;
using UnityEngine.Localization;

// 플레이어가 이 영역(Collider2D 트리거)에 들어오면 화면 위에서 지역 이름이 내려온다.
// ObjectiveZoneTrigger 와 동일한 트리거 골격이다.
//
// **Is Trigger 콜라이더를 함께 붙이세요.**
public class AreaTitleZone : MonoBehaviour {
    #region 인스펙터 변수

    [Header("표시할 지역")]
    // **지역 이름. 비우면 아무 일도 하지 않는다.** Ui 테이블에 이 지역의 키를 만들어 연결하세요
    // (예: area.circus_theater). 아직 값을 연결하지 않은 칸은 빈 문자열과 같게 취급된다.
    public LocalizedString title;
    public LocalizedString subtitle; // 부제(선택).

    [Header("트리거")]
    public bool playOnce = true; // 껐다 켜지는 구역이면 끄세요.
    public string playerTag = "Player";

    #endregion
    #region 런타임 변수

    bool played;

    #endregion
    #region 유니티 라이프 사이클

    void OnTriggerEnter2D(Collider2D other) {
        if (!other.CompareTag(playerTag)) return;
        if (playOnce && played) return;
        if (title.IsEmpty) return;

        played = true;
        AreaTitleView.Announce(LocalizationText.Resolve(title), LocalizationText.Resolve(subtitle));
    }

    #endregion
    #region 에디터 표시

#if UNITY_EDITOR
    void OnDrawGizmos() {
        if (!TryGetComponent(out Collider2D col)) return;

        Gizmos.color = new Color(0.6f, 0.5f, 1f, 0.25f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        Gizmos.color = new Color(0.6f, 0.5f, 1f, 0.8f);
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
#endif

    #endregion
}
