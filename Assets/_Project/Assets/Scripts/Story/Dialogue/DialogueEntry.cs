using UnityEngine;
using UnityEngine.Localization;

/// <summary>초상화를 띄울 쪽. 반대쪽 초상화는 숨겨집니다.</summary>
public enum PortraitSide {
    [InspectorName("왼쪽")] Left,
    [InspectorName("오른쪽")] Right,
}

// 대화 한 칸(한 화면)의 데이터: 화자 이름 + 초상화 + 본문 + 효과음.
// DialogueTriggerZone / DialogueStep 인스펙터의 Entries 리스트에서 한 칸씩 채운다.
//
// 화자와 본문은 원문을 직접 적지 않고 String Table 'Dialogue'의 키를 가리킨다. 인스펙터에서 키를
// 고르면 그 자리에서 언어별 원문까지 바로 편집할 수 있고, 전체 목록과 번역 진행 상황은
// Window ▸ Asset Management ▸ Localization Tables 에서 한 번에 본다.
// **키를 비워두면** 이름표(Speaker)나 본문(Text)이 빈 칸으로 처리된다 — 이름 없는 독백이 그 경우다.
//
// struct가 아니라 class인 이유: LocalizedString이 참조 타입이라 struct로 두면 기본값이 null인 칸이
// 생겨 조회하는 쪽마다 null 검사를 흩뿌려야 한다.
[System.Serializable]
public class DialogueEntry {
    /// <summary>화자 이름 키. 비우면 이름표를 표시하지 않습니다.</summary>
    public LocalizedString Speaker = new();

    /// <summary>화자 초상화. 비우면 초상화를 표시하지 않습니다.</summary>
    public Sprite Portrait;

    /// <summary>초상화를 띄울 쪽. 반대쪽 초상화는 숨겨집니다.</summary>
    public PortraitSide Side;

    /// <summary>본문 키. 원문에는 DialogueEffect의 마크업 태그(&lt;shake&gt; 등)를 그대로 씁니다.</summary>
    public LocalizedString Text = new();

    /// <summary>이 칸을 띄울 때 한 번 재생할 효과음. 비우면 무음.</summary>
    public AudioClip Sound;

    /// <summary>이 칸에서 바꿀 배경. **비우면 이전 배경이 그대로 남습니다.**</summary>
    /// <remarks>
    /// 초상화(Portrait)는 비우면 숨기는데 배경은 비우면 유지하는 이유: 초상화는 칸마다 바뀌는 것이
    /// 정상이지만, 배경은 한 번 깔면 여러 칸이 함께 쓰는 것이 정상이라 칸마다 다시 지정하게 하면
    /// 한 칸만 빠뜨려도 화면이 통째로 비어버립니다.
    /// </remarks>
    public Sprite Background;

    /// <summary>이 칸에서 배경을 검은 막으로 덮습니다. Background 칸보다 우선합니다.</summary>
    public bool BlackoutBackground;
}
