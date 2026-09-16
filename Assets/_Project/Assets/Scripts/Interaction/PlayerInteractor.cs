using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어 주변의 IInteractable을 찾아 프롬프트를 띄우고, 상호작용 입력을 가장 가까운 대상에게 전달한다.
// 입력은 Player_Combat과 같은 PlayerInput의 SendMessage 방식이다 (Client ▸ Player ▸ Interact).
// 프롬프트 라벨은 DamagePopup처럼 이 컴포넌트가 직접 만들어 재사용한다 — 대상이 하나뿐이라 풀링은 필요 없다.
public class PlayerInteractor : MonoBehaviour {
    #region 인스펙터 변수

    [Header("탐지 범위")]
    // 발밑이 아니라 몸통 높이에서 재도록 올려준다. 좌우 반전(스케일 -1)의 영향을 받지 않으므로
    // x를 0이 아닌 값으로 주면 바라보는 방향과 무관하게 항상 같은 쪽으로 치우친다.
    public Vector2 detectOffset = new(0f, 0.5f);
    public float detectRadius = 1.6f;
    public LayerMask detectLayer = ~0; // 기본값은 전체 레이어. 상호작용 오브젝트가 많아지면 전용 레이어로 좁히세요.

    [Header("프롬프트 문구")]
    public TMP_FontAsset fontAsset; // **한글 문구를 쓰므로 SCDream SDF를 지정하세요.** 비워두면 TMP 기본 폰트라 한글이 깨진다.
    public string actionName = "Interact"; // 문구에 키 이름을 적을 입력 액션 (Client ▸ Player). 설정에서 키를 바꾸면 문구도 따라간다.
    public string keyLabel = "F"; // 액션을 찾지 못했을 때만 쓰는 키 이름.
    public float fontSize = 3f;
    public Color textColor = new(1f, 0.95f, 0.8f, 1f);
    public Color outlineColor = Color.black;
    [Range(0f, 1f)]
    public float outlineWidth = 0.25f; // 배경과 겹쳐도 읽히도록.
    public int sortingOrder = 110; // HealthBar(100) 위, DamagePopup(120) 아래.

    [Header("프롬프트 움직임")]
    public float bobHeight = 0.1f; // 위아래로 살짝 떠서 눈에 띄게 한다.
    public float bobSpeed = 3.5f;
    public float fadeSpeed = 12f; // 대상이 바뀔 때 툭 튀지 않도록 알파를 보간하는 속도.

    #endregion
    #region 컴포넌트 변수

    Player_move playerMove;
    PlayerInput playerInput; // 상호작용 키 이름을 읽어 올 곳. 입력 자체는 SendMessage(OnInteract)로 받는다.
    string shownKey; // 문구 앞에 적는 키 이름. 매 프레임 만들지 않도록 기억해 둔다.
    bool keyDirty = true;
    TextMeshPro prompt;
    IInteractable current; // 지금 프롬프트가 가리키고 있는 대상.
    float promptAlpha;

    // 매 프레임 도는 탐지라 할당이 남지 않도록 결과 버퍼와 필터를 재사용한다.
    readonly List<Collider2D> overlapResults = new();
    ContactFilter2D overlapFilter;

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        playerMove = GetComponent<Player_move>();
        playerInput = GetComponentInParent<PlayerInput>();

        // **static 이벤트라 OnDestroy 에서 반드시 해제해야 한다.**
        InputBindings.OnApplied += HandleBindingsApplied;

        overlapFilter = new ContactFilter2D {
            useTriggers = true, // 상호작용 오브젝트는 플레이어를 밀지 않도록 트리거 콜라이더로 두는 편이 편하다.
        };

        BuildPrompt();
    }

    void Update() {
        current = IsBlocked() ? null : FindNearest();
        UpdatePrompt();
    }

    void OnDestroy() {
        if (prompt != null) Destroy(prompt.gameObject); // 플레이어에 부모로 붙이지 않았으므로 직접 정리한다.
        InputBindings.OnApplied -= HandleBindingsApplied;
    }

    void HandleBindingsApplied() {
        keyDirty = true;
    }

    #endregion
    #region 입력 처리

    void OnInteract(InputValue value) {
        if (!value.isPressed) return;
        if (current == null || !current.CanInteract) return;

        current.Interact(gameObject);
    }

    #endregion
    #region 대상 탐지

    // 대사/컷씬 중에는 상호작용을 막는다. 대사 진행키와 겹쳐 대화가 끝나는 순간
    // 같은 입력이 상호작용으로 한 번 더 먹히는 것을 방지한다.
    bool IsBlocked() {
        return playerMove != null && playerMove.isMovementLocked;
    }

    IInteractable FindNearest() {
        Vector2 origin = (Vector2)transform.position + detectOffset;

        overlapFilter.SetLayerMask(detectLayer); // 플레이 중 인스펙터에서 레이어를 바꿔도 바로 반영되도록 매번 갱신.
        Physics2D.OverlapCircle(origin, detectRadius, overlapFilter, overlapResults);

        IInteractable nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider2D hit in overlapResults) {
            // 콜라이더가 자식에 달려 있어도 부모에 붙은 구현체를 찾아낸다 (Player_Combat의 Health 탐색과 같은 방식).
            IInteractable candidate = hit.GetComponentInParent<IInteractable>();
            if (candidate == null || !candidate.CanInteract) continue;

            // 콜라이더 위치가 아니라 프롬프트 기준점으로 거리를 잰다. 큰 오브젝트가 콜라이더 가장자리 때문에
            // 실제보다 가깝게 판정되는 것을 막는다.
            float distance = Vector2.SqrMagnitude((Vector2)candidate.PromptAnchor - origin);
            if (distance >= nearestDistance) continue;

            nearest = candidate;
            nearestDistance = distance;
        }

        return nearest;
    }

    #endregion
    #region 프롬프트 표시

    void BuildPrompt() {
        // TextMeshPro는 RectTransform을 전제로 하므로 생성 시점부터 붙여둔다 (없으면 rectTransform이 null).
        GameObject obj = new GameObject("InteractPrompt", typeof(RectTransform));

        prompt = obj.AddComponent<TextMeshPro>();
        if (fontAsset != null) prompt.font = fontAsset; // 비워두면 TMP_Settings의 기본 폰트가 그대로 쓰인다.
        prompt.fontSize = fontSize;
        prompt.fontStyle = FontStyles.Bold;
        prompt.alignment = TextAlignmentOptions.Center;
        prompt.rectTransform.sizeDelta = new Vector2(10f, 2f); // 줄바꿈 없이 "[F] 정비하기" 정도가 들어갈 폭.

        prompt.outlineColor = outlineColor;
        prompt.outlineWidth = outlineWidth;

        obj.GetComponent<MeshRenderer>().sortingOrder = sortingOrder;

        obj.SetActive(false);
    }

    void UpdatePrompt() {
        if (prompt == null) return;

        // 히트스톱 중에도 프롬프트는 정상 속도로 떠 있어야 하므로 unscaledDeltaTime 기준.
        float target = current != null ? 1f : 0f;
        promptAlpha = Mathf.MoveTowards(promptAlpha, target, fadeSpeed * Time.unscaledDeltaTime);

        // 완전히 사라진 뒤에는 꺼둬서, 대상이 없는 동안 매 프레임 메시를 다시 만들지 않게 한다.
        if (current == null && promptAlpha <= 0f) {
            if (prompt.gameObject.activeSelf) prompt.gameObject.SetActive(false);
            return;
        }

        if (!prompt.gameObject.activeSelf) prompt.gameObject.SetActive(true);

        // 대상이 사라진 뒤(페이드 아웃 중)에는 마지막 문구/위치를 그대로 두고 알파만 내린다.
        if (current != null) {
            if (keyDirty) RefreshKeyName();
            prompt.text = $"[{shownKey}] {current.InteractLabel}";
            prompt.transform.position = current.PromptAnchor + Vector3.up * (Mathf.Sin(Time.unscaledTime * bobSpeed) * bobHeight);
        }

        Color color = textColor;
        color.a *= promptAlpha;
        prompt.color = color;
    }

    void RefreshKeyName() {
        InputAction action = playerInput != null && playerInput.actions != null ? playerInput.actions.FindAction(actionName) : null;

        // 액션을 찾기 전에는 인스펙터에 적어둔 이름을 쓰고 다음 프레임에 다시 찾는다 (PlayerInput 이 늦게 준비되는 경우).
        if (action == null) {
            shownKey = keyLabel;
            return;
        }

        string name = InputBindings.ActionKeyName(action);
        shownKey = string.IsNullOrEmpty(name) ? "-" : name; // 설정에서 할당을 비웠으면 누를 키가 없다는 뜻이다.
        keyDirty = false;
    }

    #endregion
    #region 에디터 표시

#if UNITY_EDITOR
    void OnDrawGizmosSelected() {
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.8f);
        Gizmos.DrawWireSphere((Vector2)transform.position + detectOffset, detectRadius);
    }
#endif

    #endregion
}
