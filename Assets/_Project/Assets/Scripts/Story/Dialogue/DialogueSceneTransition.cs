using UnityEngine;

// 대사 한 묶음이 끝나면 지정한 씬으로 넘어가는 컴포넌트. DialogueTriggerZone.OnDialogueEnd는 어떤 트리거가
// 끝났는지 구분하지 않는 static 이벤트라, 씬에 대사 오브젝트가 여럿이면 엉뚱한 대사 뒤에도 씬이 넘어간다 —
// 강제 시작형 오프닝 대사처럼 씬에 대사 트리거가 하나뿐일 때만 붙인다.
public class DialogueSceneTransition : MonoBehaviour {
    #region 인스펙터 변수

    [Header("연결")]
    public string sceneName; // 대사가 끝나면 넘어갈 씬 이름. **Build Settings 에 등록된 이름과 정확히 같아야 합니다.**

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        DialogueTriggerZone.OnDialogueEnd += HandleDialogueEnd;
    }

    void OnDestroy() {
        DialogueTriggerZone.OnDialogueEnd -= HandleDialogueEnd;
    }

    #endregion
    #region 전환

    void HandleDialogueEnd() {
        if (string.IsNullOrEmpty(sceneName)) {
            Debug.LogError($"[DialogueSceneTransition] '{name}' 의 Scene Name 칸이 비어 있어 씬을 넘길 수 없습니다.", this);
            return;
        }

        ScreenFader.LoadScene(sceneName);
    }

    #endregion
}
