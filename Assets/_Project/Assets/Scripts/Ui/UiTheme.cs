using UnityEngine;

// 「퇴락한 빈티지 극장」 — FCC 의 UI 색 토큰 한 벌.
//
// 화면에 실제로 박히는 값은 여기가 아니라 각 프리팹의 인스펙터에 있다. 이 클래스가 쓰이는 곳은 두 군데뿐이다.
//   (1) 뷰 스크립트들이 인스펙터에 노출하는 색 필드의 기본값.
//   (2) Editor/UiThemeApplier 의 일괄 적용표.
// 그래서 여기 값만 고쳐서는 이미 만들어진 프리팹이 바뀌지 않는다. 프리팹까지 맞추려면
// Tools ▸ FCC ▸ UI ▸ Apply Theme Colors 을 한 번 실행해야 한다.
//
// **색을 새로 늘리지 않는다.** 포인트 컬러는 Accent 계열(바랜 극장 적색) 하나뿐이고 나머지는 전부
// 따뜻한 어둠과 바랜 상아다. 금색·보라·청록 같은 두 번째 포인트를 끼워 넣는 순간 화면이 게임이 아니라
// 웹 대시보드처럼 보이기 시작한다 — 실제로 이 팔레트를 만들기 전 UI 가 금색·보라·적색 세 갈래로 갈라져 있었다.
public static class UiTheme {
    #region 바탕

    public static readonly Color Stage = Hex(0x0E0B0C); // 화면 바탕. 무대의 어둠이라 검정이 아니라 살짝 붉은 기가 돈다.
    public static readonly Color Panel = Hex(0x171113); // 패널·창 배경.
    public static readonly Color PanelRaised = Hex(0x221A1C); // 골라진 줄·슬롯처럼 한 겹 올라온 면.
    public static readonly Color Dim = Hex(0x000000, 0.78f); // 모달 뒤를 덮는 막.
    public static readonly Color Curtain = Hex(0x1B0F11); // 무대 커튼. 벨벳이라 바탕보다 붉고, 반투명이 아니라 덮는다.
    public static readonly Color Transparent = new(0f, 0f, 0f, 0f);

    #endregion
    #region 경계

    // 테두리는 그림자나 발광이 아니라 1px 솔리드 선 하나로만 만든다.
    public static readonly Color Line = Hex(0x3A2C2E);

    #endregion
    #region 글자

    public static readonly Color TextHigh = Hex(0xF0E6D8); // 제목·골라진 항목. 바랜 상아.
    public static readonly Color TextBody = Hex(0xC2B4A6); // 본문·메뉴 라벨.
    public static readonly Color TextMuted = Hex(0x7C6F68); // 보조 표기·힌트·버전.
    public static readonly Color TextDim = Hex(0x4A3F42); // 잠긴 항목·꺼진 항목.

    #endregion
    #region 포인트

    public static readonly Color Accent = Hex(0x8E2B2B); // 선택 테두리처럼 가느다란 곳.
    public static readonly Color AccentBright = Hex(0xA83030); // 체력 게이지처럼 면적이 넓은 곳. 같은 색조의 한 단계 위일 뿐이다.

    #endregion
    #region 도우미

    // 알파만 갈아끼운 사본. 토큰을 복제해서 알파만 다른 상수를 또 만들지 않기 위해 둔다.
    public static Color With(Color color, float alpha) {
        color.a = alpha;
        return color;
    }

    // 0xRRGGBB 를 그대로 적을 수 있게 해준다. 0~1 실수로 적어두면 나중에 색을 대조할 때 눈으로 읽히지 않는다.
    static Color Hex(int rgb, float alpha = 1f) {
        return new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f,
            alpha);
    }

    #endregion
}
