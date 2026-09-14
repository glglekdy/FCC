using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 대사 뒤에 깔리는 장면 배경. DialogueView 가 칸을 띄울 때마다 Apply(entry) 로 불러준다.
//
// 전환 방식 — 뒤 장(Back)에 다음 배경을 불투명하게 미리 깔아두고 앞 장(Front)만 투명해지게 한다.
// 둘 다 반투명해지는 진짜 크로스페이드와 달리 중간에 화면이 한 번 옅어지는 구간이 없어서, 전환 내내
// 화면이 꽉 찬 채로 넘어간다. 앞 장이 다 걷히면 뒤 장의 그림을 앞 장으로 옮겨 담고 뒤 장을 비워
// 다음 교체를 위해 원위치시킨다.
//
// 검은 막(암전)도 같은 길을 탄다 — 스프라이트 없이 색만 칠한 한 장이라 "도착 상태"가 그림이든 검정이든
// 빈 화면이든 코드가 갈라지지 않는다.
//
// 화면 요소 자체는 InGameUi 프리팹의 StoryBackground 에 들어 있다 (Tools ▸ FCC ▸ UI ▸ Apply Dialogue Layout).
public class DialogueBackgroundView : MonoBehaviour {
    #region 인스펙터 변수

    [Header("연결")]
    public Image back;  // 다음 배경이 미리 대기하는 뒤 장. **StoryBackground/Back 을 연결하세요.**
    public Image front; // 지금 보이는 앞 장. **StoryBackground/Front 를 연결하세요.**

    [Header("연출")]
    public float fadeDuration = 0.8f; // 앞 장이 걷히는 데 걸리는 시간(초).
    public Color blackoutColor = UiTheme.Stage; // 암전에 쓰는 색. 순수 검정이 아니라 무대의 어둠이다.

    #endregion
    #region 컴포넌트 변수

    AspectRatioFitter frontFitter;
    AspectRatioFitter backFitter;

    #endregion
    #region 런타임 변수

    Sprite currentSprite; // 앞 장이 들고 있는 그림.
    bool currentBlack;    // 앞 장이 검은 막인지.

    Sprite pendingSprite; // 뒤 장에 깔아둔, 이번 전환이 끝나면 앞으로 옮겨질 그림.
    bool pendingBlack;

    Coroutine fadeRoutine;
    bool ready; // 연결이 갖춰졌는지. 비어 있는 채로 칠하려 들면 NullReference 로 대사까지 멈춘다.

    // 전환이 도는 중에는 "지금 보이는 것"이 아니라 "도착할 것"과 비교해야 한다. 그러지 않으면
    // 페이드 도중 같은 배경을 다시 지정했을 때 앞 장의 옛 그림과 비교해 헛되이 한 번 더 전환한다.
    Sprite TargetSprite => fadeRoutine != null ? pendingSprite : currentSprite;
    bool TargetBlack => fadeRoutine != null ? pendingBlack : currentBlack;

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        if (front == null || back == null) {
            Debug.LogError($"[DialogueBackgroundView] '{name}' 의 연결이 비어 있습니다 — " +
                $"{(front == null ? "Front " : "")}{(back == null ? "Back " : "")}칸을 채우세요. " +
                "배경이 표시되지 않습니다.", this);
            return;
        }

        ready = true;
        frontFitter = front.GetComponent<AspectRatioFitter>();
        backFitter = back.GetComponent<AspectRatioFitter>();

        // 씬 뷰에서 확인하려고 알파를 올려둔 채 저장했더라도 플레이가 시작되면 되돌린다.
        // 배경은 전체 화면을 덮으므로 남아 있으면 게임 화면이 통째로 가려진다.
        Paint(front, frontFitter, null, false, 0f);
        Paint(back, backFitter, null, false, 0f);
    }

    #endregion
    #region 배경 전환

    /// <summary>대사 한 칸의 배경 지시를 반영합니다. DialogueView.Show 가 칸마다 부릅니다.</summary>
    public void Apply(DialogueEntry entry) {
        if (!ready || entry == null) return;

        if (entry.BlackoutBackground) {
            Blackout(fadeDuration);
            return;
        }

        // **비워두면 이전 배경을 그대로 둔다** — 바꿀 칸에만 그림을 넣는 방식이다.
        if (entry.Background == null) return;

        ChangeTo(entry.Background, fadeDuration);
    }

    /// <summary>진행 중인 전환이 끝날 때까지 기다립니다. 전환 중이 아니면 곧바로 빠져나옵니다.</summary>
    /// <remarks>대화창을 배경이 다 깔린 뒤에 띄우기 위해 DialoguePlayer 가 재생 직전에 기다립니다.</remarks>
    public IEnumerator WaitUntilSettled() {
        // 오브젝트가 꺼지면 전환 코루틴이 끝을 못 보고 멈춰 fadeRoutine 이 남는다. 그때 대사까지
        // 영영 시작되지 않는 일이 없도록 꺼진 순간 기다림을 푼다.
        while (fadeRoutine != null && isActiveAndEnabled) yield return null;
    }

    /// <summary>배경을 다른 그림으로 바꿉니다. sprite 가 비어 있으면 배경을 걷어냅니다.</summary>
    public void ChangeTo(Sprite sprite, float duration) {
        if (!ready) return;
        if (sprite == null) {
            Clear(duration);
            return;
        }
        if (sprite == TargetSprite && !TargetBlack) return; // 같은 배경이 칸마다 다시 페이드되는 것을 막는다.

        BeginFade(sprite, false, duration);
    }

    /// <summary>화면을 검은 막으로 덮습니다.</summary>
    /// <remarks>**덮은 쪽이 Clear() 로 걷어주지 않으면 검은 화면에 갇힙니다.**</remarks>
    public void Blackout(float duration) {
        if (!ready || TargetBlack) return;

        BeginFade(null, true, duration);
    }

    /// <summary>배경을 전부 걷어 게임 화면을 드러냅니다. 암전에서 빠져나오는 길이기도 합니다.</summary>
    public void Clear(float duration) {
        if (!ready) return;
        if (TargetSprite == null && !TargetBlack) return;

        BeginFade(null, false, duration);
    }

    #endregion
    #region 내부 처리

    void BeginFade(Sprite sprite, bool black, float duration) {
        // 진행 중이던 전환은 그 자리에서 끝낸 것으로 확정한다. 대사를 빠르게 넘기면 전환 요청이
        // 겹쳐 들어오는데, 반투명하게 굳은 앞 장이 남으면 다음 전환이 그 위에서 시작돼 얼룩진다.
        if (fadeRoutine != null) {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
            Commit();
        }

        pendingSprite = sprite;
        pendingBlack = black;

        bool hasDestination = sprite != null || black;
        Paint(back, backFitter, sprite, black, hasDestination ? 1f : 0f);

        // 앞 장이 비어 있으면 걷을 것이 없다 — 첫 배경은 페이드 없이 곧바로 뜬다.
        bool nothingToFade = currentSprite == null && !currentBlack;
        if (nothingToFade || duration <= 0f) {
            Commit();
            return;
        }

        fadeRoutine = StartCoroutine(FadeFrontOut(duration));
    }

    IEnumerator FadeFrontOut(float duration) {
        Color color = front.color;
        float elapsed = 0f;

        // unscaled 가 아니라 Time.deltaTime 인 이유: 대사 재생(DialoguePlayer 의 AUTO 타이머,
        // DialogueEffect 의 타자기)이 전부 스케일 시간 기준이라, 일시정지로 대사가 멈춰 있는데
        // 배경만 계속 넘어가면 둘이 어긋난다.
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(1f, 0f, elapsed / duration);
            front.color = color;
            yield return null;
        }

        fadeRoutine = null;
        Commit();
    }

    // 뒤 장에 깔아둔 도착 상태를 앞 장으로 옮기고 뒤 장을 비운다. 다음 전환도 똑같이
    // "뒤에 깔고 앞을 걷는" 한 가지 방식으로 돌게 하기 위한 원위치다.
    void Commit() {
        currentSprite = pendingSprite;
        currentBlack = pendingBlack;

        bool hasBackground = currentSprite != null || currentBlack;
        Paint(front, frontFitter, currentSprite, currentBlack, hasBackground ? 1f : 0f);
        Paint(back, backFitter, null, false, 0f);

        pendingSprite = null;
        pendingBlack = false;
    }

    // 한 장을 원하는 상태로 칠한다. sprite 가 있으면 그림, 없고 black 이면 검은 막, 둘 다 아니면 빈 장이다.
    // 빈 장은 Image 를 꺼둔다 — 전체 화면 크기라 투명해도 켜져 있으면 매 프레임 그려진다.
    void Paint(Image image, AspectRatioFitter fitter, Sprite sprite, bool black, float alpha) {
        image.sprite = sprite;
        image.color = sprite != null
            ? new Color(1f, 1f, 1f, alpha)
            : UiTheme.With(blackoutColor, black ? alpha : 0f);
        image.enabled = alpha > 0.001f && (sprite != null || black);

        // EnvelopeParent 는 화면을 꽉 채우고 남는 쪽을 잘라낸다. Image.preserveAspect 는 반대로
        // 여백을 남기는(contain) 방식이라 배경에는 쓸 수 없다. 그림마다 비율이 다를 수 있어
        // 넣을 때마다 갱신하고, 그림이 없는 장(검은 막)은 정사각으로 두어 화면을 넉넉히 덮는다.
        if (fitter == null) return;

        fitter.aspectRatio = sprite != null && sprite.rect.height > 0f
            ? sprite.rect.width / sprite.rect.height
            : 1f;
    }

    #endregion
}
