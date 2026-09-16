using UnityEngine;

// 아래에서 위로는 통과하고, 위에 올라선 뒤에는 그대로 떠받쳐 주는 한 방향 지형.
//
// 통과 판정 자체는 유니티의 PlatformEffector2D 가 처리한다. 이 컴포넌트를 따로 둔 이유는
// "이펙터 설정 + 콜라이더의 Used By Effector" 를 짝으로 맞춰야 동작하는데, 둘 중 하나만 켜 놓으면
// 그냥 막히는 지형이 되어 원인을 찾기 어렵기 때문이다. 발판을 만들 때 이 컴포넌트 하나만 붙이면
// 나머지 배선은 여기서 대신 잡아 준다.
//
// 아래로 내려가는 하강 통과(↓+점프)는 일부러 넣지 않았다. "밟고 있는 동안에는 내려갈 수 없다" 가
// 현재 기획이므로, 필요해지면 이 클래스에 입력 처리를 추가하는 방식으로 확장한다.
[RequireComponent(typeof(PlatformEffector2D))]
[DisallowMultipleComponent]
public class OneWayPlatform : MonoBehaviour {
    #region 인스펙터 변수

    [Header("통과 각도")]
    [Range(1f, 180f)]
    public float surfaceArc = 170f; // 떠받쳐 주는 각도. 180 으로 두면 옆면까지 막혀서 아래에서 뚫고 올라갈 때 모서리에 걸린다.
    public float rotationalOffset = 0f; // 발판의 "윗면" 방향을 돌린다. 기울어진 지형이나 천장형 발판에 사용.

    [Header("접촉 묶음")]
    // 발판이 여러 콜라이더로 쪼개져 있을 때 이음매에 걸리지 않도록, 한 군데라도 통과 판정이면 전부 통과로 본다.
    // **타일맵 층에서는 반드시 꺼야 합니다.** 층 전체가 CompositeCollider2D 하나로 합쳐지는 탓에 방 안의
    // 모든 발판이 한 덩어리가 되는데, 그 상태로 묶으면 위 발판의 아랫면에 몸이 닿는 순간 지금 밟고 있는
    // 발판의 접촉까지 함께 무시되어 발밑이 꺼진다. 쪼개진 이음매가 애초에 없으니 묶을 이유도 없다.
    public bool groupContacts = true;

    #endregion
    #region 컴포넌트 변수

    PlatformEffector2D effector;

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        Apply();
    }

    // 플레이 중에 각도를 만져 보며 감각을 잡을 수 있도록 인스펙터 변경을 즉시 반영한다.
    void OnValidate() {
        Apply();
    }

    #endregion
    #region 설정 적용

    public void Apply() {
        if (effector == null) effector = GetComponent<PlatformEffector2D>();
        if (effector == null) return;

        effector.useOneWay = true;
        effector.useOneWayGrouping = groupContacts;
        effector.useColliderMask = false;  // 플레이어·몬스터·투사체 구분 없이 같은 규칙을 적용한다.
        effector.surfaceArc = surfaceArc;
        effector.rotationalOffset = rotationalOffset;

        Collider2D[] cols = GetComponents<Collider2D>();
        for (int i = 0; i < cols.Length; i++) {
            if (cols[i].isTrigger) continue; // 같은 오브젝트에 붙은 판정용 트리거까지 지형으로 만들면 안 된다.
            cols[i].usedByEffector = true;
        }
    }

    #endregion
}
