using UnityEngine;

// 공격할 때 칼이 지나간 궤적(초승달 모양)을 공격 원점에 한 번 그린다.
// 아직 플레이어에게 공격 모션이 없어서, 이 궤적이 곧 "지금 휘둘렀다"는 유일한 신호다.
//
// 궤적 오브젝트는 프리팹에서 플레이어 자식으로 두지만, 처음 공격할 때 부모에서 떼어 월드에 둔다.
// Player_Combat 은 판정 위치를 공격을 시작한 순간의 좌표로 고정하는데, 궤적이 플레이어를 따라가면
// 선딜레이 동안 걸어간 만큼 실제로 맞는 자리와 어긋나 보이기 때문이다. 떼어낸 뒤로는 플레이어가
// 파괴돼도 함께 사라지지 않으므로, 연결된 Player_Combat 이 사라지면 스스로 정리한다.
//
// 시간은 스케일 시간을 쓴다. 히트스톱 동안 궤적이 그 자리에 멈춰야 타격이 박히는 느낌이 나고,
// 슬로모(GameSpeedController) 중에는 휘두르기도 같이 느려져야 어색하지 않다.
public class Player_AttackSlash : MonoBehaviour {
    #region 인스펙터 변수

    [Header("연결")]
    public Player_Combat combat; // 공격 시작을 알려줄 전투 컴포넌트. **Player 의 Combat_Manager 를 연결하세요.**
    public SpriteRenderer slashRenderer; // 궤적을 그릴 렌더러. 비워두면 같은 오브젝트에서 찾는다.

    [Header("크기")]
    // 궤적 바깥 반지름을 공격 범위(attackRange)의 몇 배로 그릴지. 1이면 판정 원의 테두리를 그대로 따라간다.
    // 절대 크기가 아니라 배율로 두는 이유는, 판정 범위를 조정하면 궤적도 저절로 따라가야 둘이 어긋나지 않기 때문이다.
    public float radiusScale = 1.1f;

    [Header("휘두르기")]
    public float sweepDuration = 0.12f; // 호가 쓸고 지나가는 시간(초).
    public float fadeDuration = 0.1f; // 다 휘두른 뒤 흐려지며 사라지는 시간(초).
    public float startAngle = 35f; // 휘두르기 시작 각도(도). 오른쪽을 볼 때 기준이며 양수가 위쪽이다.
    public float endAngle = -25f; // 휘두르기가 끝나는 각도(도).
    [Range(0f, 1f)] public float startScale = 0.7f; // 시작할 때의 크기 비율. 작게 시작해야 튀어나오듯 펼쳐진다.
    public float fadeGrow = 0.08f; // 사라지는 동안 추가로 커지는 비율. 궤적이 흩어지며 빠지는 느낌을 준다.

    [Header("연속 공격")]
    // 이 시간(초) 안에 다시 공격하면 내려 베기와 올려 베기를 번갈아 그린다. 같은 궤적만 반복되면 연타가 단조롭다.
    public float alternateWindow = 0.6f;

    [Header("색")]
    // 바랜 상아색(UiTheme.TextHigh). 순백은 어두운 극장 톤에서 혼자 튀어 보인다.
    public Color slashColor = new(0.941f, 0.902f, 0.847f, 0.9f);

    #endregion
    #region 런타임 변수

    bool playing;
    float elapsed;
    float facing = 1f; // +1 오른쪽, -1 왼쪽.
    float swingSign = 1f; // +1 내려 베기, -1 올려 베기.
    float lastAttackTime = float.NegativeInfinity;
    float baseScale = 1f; // 스프라이트 반지름을 공격 범위에 맞추는 배율. 공격마다 다시 계산해 플레이 중 범위 조정도 반영한다.

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        if (slashRenderer == null) slashRenderer = GetComponent<SpriteRenderer>();

        if (combat == null) Debug.LogError("[Player_AttackSlash] 'combat'(Player_Combat) 이 연결되지 않았습니다.", this);
        if (slashRenderer == null) Debug.LogError("[Player_AttackSlash] 'slashRenderer'(SpriteRenderer) 를 찾지 못했습니다.", this);

        if (slashRenderer != null) slashRenderer.enabled = false;
    }

    void OnEnable() {
        if (combat != null) combat.OnAttackStarted += Play;
    }

    void OnDisable() {
        if (combat != null) combat.OnAttackStarted -= Play;
    }

    void Update() {
        // 부모에서 떼어냈으므로 플레이어가 파괴돼도 이 오브젝트는 남는다. 주인이 사라지면 같이 치운다.
        if (combat == null) {
            Destroy(gameObject);
            return;
        }

        if (!playing) return;

        elapsed += Time.deltaTime;
        if (elapsed >= sweepDuration + fadeDuration) {
            playing = false;
            slashRenderer.enabled = false;
            return;
        }

        ApplyPose(elapsed);
    }

    #endregion
    #region 궤적 재생

    void Play(Vector2 origin, float facingSign) {
        if (slashRenderer == null || slashRenderer.sprite == null) return;

        bool chained = Time.time - lastAttackTime <= alternateWindow;
        swingSign = chained ? -swingSign : 1f; // 끊겼다가 다시 치면 항상 내려 베기부터 시작한다.
        lastAttackTime = Time.time;

        // 판정 위치에 고정해 그리려면 플레이어의 이동·좌우 반전을 물려받으면 안 된다 (맨 위 설명 참고).
        // Awake 가 아니라 첫 공격 때 떼는 이유: 플레이어가 SetActive/Instantiate 로 켜지는 도중에는
        // 유니티가 부모 변경을 막아 오류를 낸다. 한 번도 공격하지 않았다면 플레이어와 함께 정상적으로 파괴된다.
        if (transform.parent != null) transform.SetParent(null, true);

        facing = facingSign;
        transform.position = new Vector3(origin.x, origin.y, transform.position.z);

        // 스프라이트는 피벗(원 중심)에서 오른쪽 끝까지가 호의 바깥 반지름이 되도록 그려 두었다.
        float spriteRadius = slashRenderer.sprite.bounds.max.x;
        baseScale = spriteRadius > 0f ? combat.attackRange * radiusScale / spriteRadius : 1f;

        elapsed = 0f;
        playing = true;
        slashRenderer.enabled = true;
        ApplyPose(0f);
    }

    void ApplyPose(float time) {
        // 칼은 출발할 때 가장 빠르고 끝에서 느려지므로 ease-out 으로 쓸어낸다.
        float sweepT = sweepDuration > 0f ? Mathf.Clamp01(time / sweepDuration) : 1f;
        float inv = 1f - sweepT;
        float eased = 1f - inv * inv * inv;

        float fadeT = fadeDuration > 0f ? Mathf.Clamp01((time - sweepDuration) / fadeDuration) : (time >= sweepDuration ? 1f : 0f);

        float angle = Mathf.Lerp(startAngle, endAngle, eased);
        float scale = baseScale * Mathf.Lerp(startScale, 1f, eased) * (1f + fadeGrow * fadeT);

        // 왼쪽을 보면 X 로, 올려 베기면 Y 로 뒤집는다. 거울에 비치면 회전 방향도 반대가 되므로 각도에 두 부호를 모두 곱한다.
        transform.localScale = new Vector3(scale * facing, scale * swingSign, 1f);
        transform.rotation = Quaternion.Euler(0f, 0f, angle * facing * swingSign);

        // 제곱으로 빼면 처음엔 또렷하게 남아 있다가 끝에서 빠르게 사라진다.
        Color c = slashColor;
        c.a *= 1f - fadeT * fadeT;
        slashRenderer.color = c;
    }

    #endregion
}
