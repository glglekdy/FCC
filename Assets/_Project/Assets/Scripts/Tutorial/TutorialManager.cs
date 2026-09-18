using System.Collections.Generic;
using UnityEngine;

// 이미 본 튜토리얼 id를 들고 있는 싱글턴. NpcDialogueManager와 같은 형태(런타임 집합 +
// CaptureState/RestoreState)를 따르되, 사전 등록할 기획 에셋 목록이 없다 — TutorialTriggerZone이
// 스스로 tutorialId를 들고 조회/기록을 요청하는 구조라 Build() 단계가 필요 없다.
public class TutorialManager : MonoBehaviour {
    public static TutorialManager Instance;

    #region 런타임 변수

    readonly HashSet<string> shownIds = new();

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null); // DontDestroyOnLoad는 루트 오브젝트에서만 동작 (다른 싱글턴과 동일한 이유)
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy() {
        if (Instance == this) Instance = null;
    }

    #endregion
    #region 조회 · 갱신

    // 기록이 없는 튜토리얼은 아직 안 본 것으로 취급한다.
    public bool HasShown(string tutorialId) {
        return !string.IsNullOrEmpty(tutorialId) && shownIds.Contains(tutorialId);
    }

    public void MarkShown(string tutorialId) {
        if (!string.IsNullOrEmpty(tutorialId)) shownIds.Add(tutorialId);
    }

    #endregion
    #region 세이브

    public List<string> CaptureState() {
        return new List<string>(shownIds);
    }

    // 세이브에서 읽은 상태로 되돌린다. 스냅샷에 없는 기록이 남으면 구버전 세이브를 불렀을 때
    // 최근 진행이 섞이므로(NpcDialogueManager.RestoreState와 같은 이유) 항상 통째로 비우고 다시 채운다.
    public void RestoreState(List<string> snapshot) {
        shownIds.Clear();
        if (snapshot == null) return;

        foreach (string id in snapshot) shownIds.Add(id);
    }

    #endregion
}
