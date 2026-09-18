using UnityEngine;
using UnityEngine.Localization;

// 상호작용하면 화면이 페이드아웃되며 지정한 씬으로 넘어가는 문. 페이드아웃 → 로드 → 페이드인은
// ScreenFader.LoadScene 한 곳에 맡기므로 여기서는 대상 씬 이름만 넘긴다.
//
// **Is Trigger 콜라이더를 붙이세요.** (PlayerInteractor 가 트리거만 탐지합니다.)
[RequireComponent(typeof(Collider2D))]
public class SceneDoor : MonoBehaviour, IInteractable {
    #region 인스펙터 변수

    [Header("이동")]
    public string sceneName; // 넘어갈 씬 이름. **Build Settings 에 등록된 이름과 정확히 같아야 합니다.**

    [Header("프롬프트")]
    public LocalizedString label = new("Ui", "interact.move"); // 플레이어에게 뜨는 문구.
    public Vector3 promptOffset = new(0f, 1.6f, 0f); // 문 위쪽에 프롬프트를 띄울 오프셋.

    #endregion
    #region 런타임 변수

    bool isTransitioning; // 페이드가 도는 동안 재상호작용으로 두 번 넘어가는 것을 막는다.

    #endregion
    #region IInteractable

    public string InteractLabel => LocalizationText.Resolve(label, "이동하기");
    public bool CanInteract => !isTransitioning;
    public Vector3 PromptAnchor => transform.position + promptOffset;

    public void Interact(GameObject interactor) {
        if (isTransitioning) return;

        if (string.IsNullOrEmpty(sceneName)) {
            Debug.LogError($"[SceneDoor] '{name}' 의 Scene Name 칸이 비어 있어 씬을 넘길 수 없습니다.", this);
            return;
        }

        isTransitioning = true;

        // 씬이 완전히 넘어가기 전(페이드아웃 중)에 플레이어가 돌아다니지 않도록 잠근다.
        // PlayerInteractor 가 플레이어 자식에 붙어 있어도 찾아지도록 부모 쪽으로 찾는다(SaveMirror 와 같은 이유).
        Player_move move = interactor.GetComponentInParent<Player_move>();
        if (move != null) move.isMovementLocked = true;

        ScreenFader.LoadScene(sceneName);
    }

    #endregion
}
