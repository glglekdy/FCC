using UnityEngine;

// 플레이어가 보유한 뒷세계 코인 수량. 뒷세계의 방을 클리어할 때마다 DungeonGate 가 지급하고,
// 뒷세계 상점(DungeonShopPedestal)에서만 쓴다. SaveManager 가 세이브 파일과 동기화한다.
//
// 기억 조각(Player_MemoryShardInventory)과 합치지 않은 이유: 기억 조각은 스킬 강화 비용이 보유량의 비율로
// 계산되는 본편 재화라, 뒷세계에서 쏟아지는 코인이 섞이면 강화 비용까지 함께 불어난다.
// 두 재화의 쓰임새를 갈라 두어야 뒷세계를 반복해 돌아도 본편 성장 곡선이 흔들리지 않는다.
//
// 뒷세계를 나가도 사라지지 않는다. 던전 한 판의 상태(몬스터 · 방 · 한정 강화)는 나갈 때 전부 치워지지만,
// 코인은 이 컴포넌트가 플레이어에 붙어 들고 있으므로 해체와 무관하게 남는다.
public class Player_DungeonCoinInventory : MonoBehaviour {
    #region 인스펙터 변수

    [Header("뒷세계 코인")]
    public int count; // 보유한 뒷세계 코인 수.

    #endregion
    #region 조회

    public int Count => count;

    #endregion
    #region 증감

    public void Add(int amount) {
        count = Mathf.Max(0, count + amount);
    }

    // 모자라면 아무것도 하지 않고 false 를 돌려준다. 차감과 판정을 한 곳에 묶어야 부른 쪽이 반쯤 깎인 상태를
    // 되돌릴 일이 생기지 않는다(Player_MemoryShardInventory.Spend 와 같은 이유).
    public bool Spend(int amount) {
        if (amount < 0 || count < amount) return false;

        count -= amount;
        return true;
    }

    // 세이브 복원 전용.
    public void SetCount(int value) {
        count = Mathf.Max(0, value);
    }

    #endregion
}
