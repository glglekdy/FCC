#if UNITY_EDITOR
using UnityEngine;

// 던전 특수 발판(이동 · 승강 · 붕괴 · 가시)의 색 한 벌.
//
// UiTheme 에 토큰을 늘리지 않고 따로 둔 이유: UiTheme 은 「퇴락한 빈티지 극장」 UI 팔레트라 "색을 새로
// 늘리지 않는다"가 규칙으로 박혀 있는데, 이쪽은 화면이 아니라 레벨에서 "밟을 것인가 / 피할 것인가"를
// 달리는 도중에 0.1초 만에 가려내야 하는 신호다. 분위기보다 대비가 먼저라 팔레트를 나눴다.
//
// 방 빌더(DungeonRoomPrefabBuilder)와 타일맵 변환기(DungeonTilemapConverter) 두 곳이 같은 색을 칠하므로
// 여기 한 곳에서만 값을 들고 있는다. 양쪽에 따로 적어 두면 한쪽만 고쳤을 때 방마다 색이 갈라진다.
//
// **여기 값만 고쳐도 이미 찍어 둔 방 프리팹은 바뀌지 않는다** — 화면에 나가는 값은 프리팹의
// SpriteRenderer 에 박혀 있다. 기존 방까지 맞추려면 프리팹의 색을 직접 바꿔야 한다.
public static class DungeonGimmickPalette {
    // 밟을 수 있는 특수 발판(이동 · 승강 · 붕괴)은 전부 흰색. 지형 타일보다 훨씬 밝아야
    // 배경에 묻히지 않고 "손댄 발판"으로 읽힌다.
    public static readonly Color Platform = Color.white;

    // 닿으면 자아 게이지가 깎이는 것(가시)은 빨강. 발판과 밝기가 아니라 색상 자체가 달라야
    // 급하게 뛰는 중에도 밟을 것과 피할 것이 헷갈리지 않는다.
    public static readonly Color Hazard = new(1f, 0f, 0f, 1f);
}
#endif
