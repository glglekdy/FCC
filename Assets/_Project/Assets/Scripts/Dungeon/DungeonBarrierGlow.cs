using UnityEngine;

// 전투방 바리어(투명벽)에 붙이는 빛 연출. 잠겨 있는 동안 플레이어가 다가가면 벽이 빛나고, 멀어지면 서서히 사라진다.
//
// 벽이 늘 보이면 방 풍경을 가리고, 끝까지 안 보이면 "왜 못 가지?" 하고 벽에 몸을 비비게 된다. 부딪힐 만큼
// 가까이 왔을 때만 드러내 "몬스터를 다 잡아야 열린다"는 규칙을 그 자리에서 읽히게 한다.
//
// 잠금 여부는 DungeonRoom 이 콜라이더를 켜고 끄는 것을 그대로 읽는다. 방이 이 연출을 몰라도 되게 하려는 것으로,
// 연출을 빼도(이 컴포넌트를 지워도) 벽은 똑같이 막는다.
// **자식에 벽 크기로 늘린 SpriteRenderer 를 두고 glow 에 연결하세요.** (Tools ▸ FCC ▸ Dungeon ▸ Patch Combat Room Barriers)
public class DungeonBarrierGlow : MonoBehaviour {
    #region 인스펙터 변수

    [Header("연결")]
    public BoxCollider2D blocker; // 통행을 막는 콜라이더. 비우면 같은 오브젝트에서 찾는다.
    public SpriteRenderer glow;   // **벽 모양으로 늘린 빛 스프라이트(자식)를 연결하세요.** 색의 알파는 이 컴포넌트가 덮어쓴다.

    [Header("다가가면 빛나기")]
    public string playerTag = "Player";
    public float glowDistance = 3f; // 플레이어가 벽에서 이 거리(유닛) 안에 들어오면 빛난다.
    public float fadeInTime = 0.25f; // 다가갔을 때 다 밝아지기까지 걸리는 시간.
    public float fadeOutTime = 1.2f; // 멀어졌을 때 다 사라지기까지 걸리는 시간. 켜질 때보다 길게 두어 빛이 남는 느낌을 준다.
    [Range(0f, 1f)] public float maxAlpha = 0.85f; // 가장 밝을 때의 알파.

    [Header("일렁임")]
    public float pulseSpeed = 2.4f; // 밝기가 오르내리는 빠르기.
    [Range(0f, 1f)] public float pulseAmount = 0.25f; // 밝기가 최대에서 이 비율만큼 내려갔다 올라온다. 0이면 일렁이지 않는다.

    [Header("잠기는 순간")]
    // 벽이 켜지는 순간 한 번 빛났다가 사라진다. 입구 쪽 벽은 플레이어가 등진 채 잠기므로 이게 없으면 잠긴 줄 모른다. 0이면 끈다.
    public float lockFlashTime = 0.8f;

    #endregion
    #region 런타임 변수

    Transform player;
    float intensity;   // 다가감에 따른 밝기(0~1). 페이드가 적용된 값이다.
    float flashTimer;  // 0보다 크면 잠기는 순간의 번쩍임이 남아 있다.
    bool wasLocked;
    Color baseColor;

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        if (blocker == null) blocker = GetComponent<BoxCollider2D>();
        if (blocker == null) Debug.LogError($"[DungeonBarrierGlow] '{name}' — blocker(BoxCollider2D)가 연결되지 않아 잠금 여부를 알 수 없습니다.", this);
        if (glow == null) Debug.LogError($"[DungeonBarrierGlow] '{name}' — glow(SpriteRenderer)가 연결되지 않아 빛 연출이 보이지 않습니다.", this);

        if (glow != null) {
            baseColor = glow.color;
            glow.enabled = false; // 평소에는 그리지 않는다. 알파 0 으로 두어도 그리기 비용은 든다.
        }

        wasLocked = IsLocked;
    }

    void Update() {
        if (glow == null || blocker == null) return;

        bool locked = IsLocked;
        if (locked && !wasLocked && lockFlashTime > 0f) flashTimer = lockFlashTime;
        wasLocked = locked;

        // 열린 채로 빛도 다 꺼졌으면 할 일이 없다. 플레이어를 찾지도 않는다.
        if (!locked && intensity <= 0f && flashTimer <= 0f) {
            if (glow.enabled) glow.enabled = false;
            return;
        }

        float target = locked && IsPlayerNear() ? 1f : 0f;
        float fadeTime = target > intensity ? fadeInTime : fadeOutTime;
        intensity = fadeTime <= 0f ? target : Mathf.MoveTowards(intensity, target, Time.deltaTime / fadeTime);

        float flash = 0f;
        if (flashTimer > 0f) {
            flashTimer -= Time.deltaTime;
            flash = Mathf.Clamp01(flashTimer / lockFlashTime);
        }

        ApplyStrength(Mathf.Max(intensity, flash));
    }

    #endregion
    #region 조회

    public bool IsLocked => blocker != null && blocker.enabled;

    #endregion
    #region 빛

    void ApplyStrength(float strength) {
        bool visible = strength > 0.001f;
        if (glow.enabled != visible) glow.enabled = visible;
        if (!visible) return;

        // 일렁임은 최대 밝기에서 아래로만 내려간다. 위로도 흔들면 maxAlpha 를 넘어 인스펙터 값이 의미를 잃는다.
        float wave = pulseAmount > 0f ? 1f - pulseAmount * 0.5f * (1f + Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f)) : 1f;

        Color c = baseColor;
        c.a = baseColor.a * maxAlpha * strength * wave;
        glow.color = c;
    }

    // 벽 콜라이더 사각형의 가장 가까운 점까지 잰다. 벽이 세로로 길어서 중심점 거리로 재면 벽 끝 쪽에 붙어도 안 빛난다.
    bool IsPlayerNear() {
        if (player == null) {
            GameObject found = GameObject.FindWithTag(playerTag);
            if (found == null) return false;
            player = found.transform;
        }

        Vector2 center = transform.TransformPoint(blocker.offset);
        Vector3 scale = transform.lossyScale;
        Vector2 half = new(Mathf.Abs(blocker.size.x * scale.x) * 0.5f, Mathf.Abs(blocker.size.y * scale.y) * 0.5f);

        Vector2 p = player.position;
        Vector2 closest = new(Mathf.Clamp(p.x, center.x - half.x, center.x + half.x),
                              Mathf.Clamp(p.y, center.y - half.y, center.y + half.y));

        return (p - closest).sqrMagnitude <= glowDistance * glowDistance;
    }

    #endregion
}
