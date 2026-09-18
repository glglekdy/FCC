using UnityEngine;
using UnityEngine.Localization;

// 튜토리얼 한 칸(한 화면)의 데이터: 이름(부제) + 안내 멘트 + 이미지.
// TutorialTriggerZone 인스펙터의 Entries 리스트에서 한 칸씩 채운다.
//
// 이름과 멘트는 원문을 직접 적지 않고 String Table 'Tutorial'의 키를 가리킨다. 인스펙터에서 키를
// 고르면 그 자리에서 언어별 원문까지 바로 편집할 수 있다 (DialogueEntry와 같은 방식).
//
// struct가 아니라 class인 이유는 DialogueEntry와 같다 — LocalizedString이 참조 타입이라 struct로
// 두면 기본값이 null인 칸이 생겨 조회하는 쪽마다 null 검사를 흩뿌려야 한다.
[System.Serializable]
public class TutorialEntry {
    /// <summary>튜토리얼 이름(부제) 키. 예: "점프".</summary>
    public LocalizedString Subtitle = new();

    /// <summary>안내 멘트 키.</summary>
    public LocalizedString Message = new();

    /// <summary>안내 이미지. 비우면 이미지 칸을 숨긴다.</summary>
    public Sprite Image;
}
