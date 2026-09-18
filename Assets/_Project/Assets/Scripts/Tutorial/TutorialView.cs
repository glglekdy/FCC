using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 튜토리얼 창 UI 한 묶음을 제어한다: 패널 루트 + 이름(부제) + 멘트 + 이미지.
// 재생 측(TutorialPlayer)에서 Show(entry, subtitle, message)로 한 칸씩 표시한다.
// 제목("튜토리얼")과 힌트 문구("Enter 다음   Esc 스킵")는 프리팹의 TMP에 고정으로 적혀 있다
// (UI 구현 규칙 — 고정 문구는 프리팹에 직접 입력).
//
// **Prefabs/UI/TutorialUI.prefab 의 루트에 붙어 있다.** (DialogueView와 같은 역할, ObjectiveItemView와 같은 구조)
public class TutorialView : MonoBehaviour {
    #region 인스펙터 변수

    [Header("연결 — 프리팹이 채워 둔 값입니다")]
    public TMP_Text subtitleText; // 튜토리얼 이름.
    public TMP_Text messageText; // 안내 멘트.
    public Image image; // 안내 이미지. 없으면 칸을 숨긴다.

    #endregion
    #region 런타임 변수

    bool isReady; // 연결이 온전한지. 어긋난 채로 갱신하면 NullReference가 쏟아지므로 Awake에서 한 번만 검사한다.

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        isReady = ValidateReferences();
    }

    // 프리팹을 손보다 참조를 끊었을 때 조용히 죽지 않도록 무엇이 비었는지 이름으로 찍어준다.
    // (ObjectiveItemView.ValidateReferences와 같은 방식)
    bool ValidateReferences() {
        List<string> missing = new();

        if (subtitleText == null) missing.Add(nameof(subtitleText));
        if (messageText == null) missing.Add(nameof(messageText));
        if (image == null) missing.Add(nameof(image));

        if (missing.Count == 0) return true;

        Debug.LogError($"[TutorialView] '{name}' 의 프리팹 연결이 비어 있습니다 — {string.Join(", ", missing)}. " +
            "Prefabs/UI/TutorialUI 프리팹을 쓰세요. 없으면 Tools ▸ FCC ▸ Build Tutorial Prefab 으로 만들 수 있습니다.", this);
        return false;
    }

    #endregion
    #region 표시

    // 튜토리얼 창을 켜고 한 칸(이름 + 멘트 + 이미지)을 표시한다.
    // 문자열을 entry에서 직접 꺼내지 않고 밖에서 받는 이유는 DialogueView.Show와 같다 —
    // String Table 조회가 비동기라 코루틴(TutorialPlayer) 쪽에서만 기다릴 수 있기 때문이다.
    public void Show(TutorialEntry entry, string subtitle, string message) {
        gameObject.SetActive(true);
        if (!isReady) return;

        subtitleText.text = subtitle;
        messageText.text = message;

        bool hasImage = entry.Image != null;
        image.gameObject.SetActive(hasImage);
        if (hasImage) image.sprite = entry.Image;
    }

    public void Hide() {
        gameObject.SetActive(false);
    }

    #endregion
}
