using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대화창 UI 한 묶음을 제어합니다: 패널 루트 + 본문(DialogueEffect) + 이름표 + 좌/우 초상화 + AUTO 버튼.
/// 재생 측(DialoguePlayer)에서 Show(entry)로 한 칸씩 표시하고,
/// 타자기 출력 스킵은 IsTyping / CompleteReveal 로 처리합니다.
/// 이름표·초상화·AUTO 참조는 전부 선택 사항(비워도 동작)입니다.
/// </summary>
public class DialogueView : MonoBehaviour
{
    [SerializeField] private GameObject     _root;           // 켜고 끌 패널 루트 (비우면 이 오브젝트)
    [SerializeField] private DialogueEffect _effect;         // 본문 텍스트 엔진
    [SerializeField] private TMP_Text       _nameText;       // 화자 이름표 (선택)
    [SerializeField] private GameObject     _nameBox;        // 이름 없을 때 통째로 숨길 박스 (선택)
    [SerializeField] private Image          _portraitLeft;   // 왼쪽 초상화 (선택)
    [SerializeField] private Image          _portraitRight;  // 오른쪽 초상화 (선택)
    [SerializeField] private AudioSource    _sfxSource;      // 칸별 Sound 재생 (비우면 무음)

    [Header("자동 진행")]
    [SerializeField] private Button   _autoButton;               // AUTO 토글 버튼 (선택)
    [SerializeField] private TMP_Text _autoLabel;                // 켜짐/꺼짐을 색으로 표시 (선택)
    [SerializeField] private float    _autoAdvanceDelay = 1.5f;  // 출력 완료 후 다음 칸까지 대기(초)

    // 금색·순백은 「퇴락한 빈티지 극장」 팔레트 밖의 색이라 UiTheme 토큰으로 바꿨습니다.
    [SerializeField] private Color    _autoOnColor  = UiTheme.AccentBright;
    [SerializeField] private Color    _autoOffColor = UiTheme.TextMuted;

    [SerializeField] private Button   _skipButton;               // 대화 전체 건너뛰기 (선택)

    private bool _autoAdvance;
    private int  _uiClickFrame = -10;

    /// <summary>본문 타자기 출력이 진행 중인지 여부.</summary>
    public bool IsTyping => _effect != null && _effect.IsTyping;

    /// <summary>AUTO(자동 진행)가 켜져 있는지 여부.</summary>
    public bool IsAutoAdvance => _autoAdvance;

    /// <summary>출력이 끝난 뒤 자동으로 다음 칸으로 넘어가기까지의 대기 시간(초).</summary>
    public float AutoAdvanceDelay => _autoAdvanceDelay;

    /// <summary>이번 프레임의 "다음 칸" 입력을 무시해야 하는지 여부.
    /// NextDialogue가 마우스 좌클릭에도 묶여 있어, AUTO 버튼을 누른 클릭이
    /// 대사까지 한 칸 넘겨버리는 것을 막습니다.</summary>
    public bool BlockAdvanceThisFrame => Time.frameCount - _uiClickFrame <= 1;

    // 컴포넌트를 처음 붙였을 때 본문 엔진을 자동으로 찾아 채웁니다.
    private void Reset() => _effect = GetComponentInChildren<DialogueEffect>(true);

    private void Awake()
    {
        // _effect가 비어 있으면 대사가 한 글자도 출력되지 않으면서 에러도 안 납니다.
        // 자식에서 찾아 스스로 복구하고, 그래도 없으면 확실히 알립니다.
        if (_effect == null)
        {
            _effect = GetComponentInChildren<DialogueEffect>(true);
            if (_effect != null)
                Debug.LogWarning($"[DialogueView] '{name}' 의 Effect 칸이 비어 있어 자식의 " +
                                 $"'{_effect.name}' 에서 자동으로 찾았습니다. 인스펙터에서 직접 연결해 두세요.", this);
            else
                Debug.LogError($"[DialogueView] '{name}' 에 본문 엔진(DialogueEffect)이 없습니다. " +
                               $"대사가 출력되지 않습니다 — Effect 칸을 연결하세요.", this);
        }

        if (_autoButton != null) _autoButton.onClick.AddListener(ToggleAutoAdvance);
        if (_skipButton != null) _skipButton.onClick.AddListener(RequestSkip);
        RefreshAutoLabel();
    }

    private void OnDestroy()
    {
        if (_autoButton != null) _autoButton.onClick.RemoveListener(ToggleAutoAdvance);
        if (_skipButton != null) _skipButton.onClick.RemoveListener(RequestSkip);
    }

    /// <summary>대화창을 켜고 한 칸(화자 + 초상화 + 본문 + 효과음)을 표시합니다.</summary>
    /// <param name="entry">초상화·좌우·효과음처럼 번역이 필요 없는 값을 읽어옵니다.</param>
    /// <param name="speaker">현재 언어로 해석된 화자 이름. 비어 있으면 이름표를 숨깁니다.</param>
    /// <param name="text">현재 언어로 해석된 본문. DialogueEffect의 마크업 태그가 들어 있습니다.</param>
    /// <remarks>
    /// 문자열을 entry에서 직접 꺼내지 않고 밖에서 받는 이유는, String Table 조회가 비동기라
    /// 코루틴(DialoguePlayer) 쪽에서만 기다릴 수 있기 때문입니다.
    /// </remarks>
    public void Show(DialogueEntry entry, string speaker, string text)
    {
        SetRootActive(true);
        SetSpeaker(speaker, entry.Portrait, entry.Side);
        if (_sfxSource != null && entry.Sound != null) _sfxSource.PlayOneShot(entry.Sound);
        if (_effect != null) _effect.SetText(text);
    }

    /// <summary>진행 중인 타자기 출력을 즉시 끝냅니다.</summary>
    public void CompleteReveal()
    {
        if (_effect != null) _effect.CompleteReveal();
    }

    /// <summary>대화창을 끕니다.</summary>
    public void Hide() => SetRootActive(false);

    /// <summary>남은 대사를 전부 건너뛰라는 요청이 들어와 있는지 여부.
    /// DialoguePlayer가 매 프레임 확인하고, 켜져 있으면 재생 루프를 빠져나옵니다.</summary>
    public bool SkipRequested { get; private set; }

    /// <summary>남은 대사를 전부 건너뜁니다. SKIP 버튼의 onClick에 자동으로 연결됩니다.</summary>
    /// <remarks>
    /// 여기서 재생을 직접 멈추지 않고 깃발만 세우는 이유는, 재생 루프가 DialoguePlayer의 코루틴이라
    /// 바깥에서 끊으면 뒷정리(입력 액션 해제·대화창 닫기)가 건너뛰어지기 때문입니다.
    /// </remarks>
    public void RequestSkip()
    {
        SkipRequested = true;
        _uiClickFrame = Time.frameCount; // 이 클릭이 "다음 칸"으로도 먹히지 않게 막습니다.
    }

    /// <summary>건너뛰기 요청을 지웁니다. 재생이 끝날 때 DialoguePlayer가 부릅니다.</summary>
    public void ClearSkipRequest() => SkipRequested = false;

    /// <summary>AUTO를 켜고 끕니다. AUTO 버튼의 onClick에 자동으로 연결됩니다.</summary>
    public void ToggleAutoAdvance()
    {
        _autoAdvance  = !_autoAdvance;
        _uiClickFrame = Time.frameCount;
        RefreshAutoLabel();
    }

    private void RefreshAutoLabel()
    {
        if (_autoLabel != null) _autoLabel.color = _autoAdvance ? _autoOnColor : _autoOffColor;
    }

    private void SetSpeaker(string speaker, Sprite portrait, PortraitSide side)
    {
        bool hasName = !string.IsNullOrEmpty(speaker);
        if (_nameText != null) _nameText.text = speaker;
        if (_nameBox  != null) _nameBox.SetActive(hasName);

        // 지정된 쪽에만 초상화를 띄우고 반대쪽은 숨깁니다.
        SetPortrait(_portraitLeft,  side == PortraitSide.Left  ? portrait : null);
        SetPortrait(_portraitRight, side == PortraitSide.Right ? portrait : null);
    }

    private static void SetPortrait(Image image, Sprite sprite)
    {
        if (image == null) return;
        image.sprite  = sprite;
        image.enabled = sprite != null;
    }

    private void SetRootActive(bool active)
    {
        GameObject target = _root != null ? _root : gameObject;
        target.SetActive(active);
    }
}
