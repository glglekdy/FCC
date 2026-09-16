using UnityEngine;

// F키 상호작용의 대상이 되는 오브젝트가 구현하는 인터페이스.
// PlayerInteractor는 이 인터페이스만 알면 되므로, 거울·NPC·조사 오브젝트를 새로 만들 때
// 여기만 구현하면 탐지·프롬프트·입력 전달은 자동으로 따라온다.
public interface IInteractable {
    // 프롬프트에 표시할 문구 ("정비하기", "조사하기" 등).
    // 대상 상태에 따라 문구가 바뀔 수 있어 필드가 아니라 프로퍼티로 둔다.
    string InteractLabel { get; }

    // 지금 상호작용할 수 있는지. false면 탐지 대상에서 빠져 프롬프트도 뜨지 않는다.
    // (이미 조사한 오브젝트, 연출 재생 중인 거울 등)
    bool CanInteract { get; }

    // 프롬프트를 띄울 월드 좌표. 콜라이더 중심이 아니라 오브젝트 머리 위로 올리고 싶을 때
    // 각자 오프셋을 더해서 돌려준다.
    Vector3 PromptAnchor { get; }

    // interactor: 상호작용을 시도한 플레이어 오브젝트. Health 등 플레이어 컴포넌트가 필요할 때 쓴다.
    void Interact(GameObject interactor);
}
