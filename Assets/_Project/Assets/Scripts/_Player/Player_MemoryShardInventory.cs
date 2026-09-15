using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어가 보유한 기억 조각 수량. SaveManager가 이 값을 세이브 파일과 동기화한다.
// 회복 소비·스킬 포인트 전환 등 후속 기능은 아직 없고, 지금은 보유 수량만 들고 있다.
public class Player_MemoryShardInventory : MonoBehaviour {
    #region 인스펙터 변수

    [Header("기억 조각")]
    public int count; // 보유한 기억 조각 수.

    [Header("개발용")]
    // 에디터 · 개발 빌드에서만 동작한다(Player_move 의 F5/F9 퀵세이브와 같은 성격). 강화를 시험해 볼 조각을 바로 채운다.
    public Key debugAddKey = Key.F6;
    public int debugAddAmount = 10;

    #endregion
    #region 유니티 라이프 사이클

    void Update() {
        // 정식 빌드에서는 조각을 공짜로 얻는 길이 생기면 안 되므로 개발 빌드 여부부터 본다.
        if (!Debug.isDebugBuild || debugAddKey == Key.None || Keyboard.current == null) return;
        if (!Keyboard.current[debugAddKey].wasPressedThisFrame) return;

        Add(debugAddAmount);
        Debug.Log($"[MemoryShard] 개발용 단축키로 기억 조각 {debugAddAmount}개를 더했습니다. 보유 {count}");
    }

    #endregion
    #region 조회

    public int Count => count;

    #endregion
    #region 증감

    public void Add(int amount = 1) {
        count = Mathf.Max(0, count + amount);
    }

    // 스킬 강화 등으로 조각을 소비한다. 모자라면 아무것도 하지 않고 false를 돌려준다 —
    // 일부만 깎고 실패하면 부른 쪽이 되돌릴 방법이 없어, 차감과 판정을 한 곳에 묶어 둔다.
    public bool Spend(int amount) {
        if (amount <= 0 || count < amount) return false;

        count -= amount;
        return true;
    }

    // 세이브 복원 전용. 직접 증감이 아니라 값을 통째로 맞출 때 쓴다.
    public void SetCount(int value) {
        count = Mathf.Max(0, value);
    }

    #endregion
}
