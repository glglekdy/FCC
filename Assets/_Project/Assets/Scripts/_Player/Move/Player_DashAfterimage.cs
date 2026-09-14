using System.Collections.Generic;
using UnityEngine;

// 대시하는 동안 플레이어의 현재 스프라이트를 그 자리에 몇 장 찍어 두고, 각 장을 천천히 흐리게
// 지워 잔상처럼 보이게 한다. 스킬과 무관한 기본 대시(Player_move)에만 반응한다.
//
// 잔상 한 장은 부모 없이 월드에 남겨 둔다 — 플레이어가 앞으로 미끄러져 나가도 잔상은 찍힌 자리에
// 그대로 머물러야 궤적이 보이기 때문이다. 플레이어가 파괴돼도 남은 잔상이 씬에 떠 있지 않도록
// OnDestroy 에서 전부 정리한다 (HealthBar 가 자기 바를 정리하는 것과 같은 이유).
public class Player_DashAfterimage : MonoBehaviour {
    #region 인스펙터 변수

    [Header("연결")]
    public Player_move move; // 비워두면 같은 오브젝트에서 찾는다.
    // 잔상으로 복제할 본체 스프라이트. **Player_Renderer 의 SpriteRenderer 를 연결하세요.**
    public SpriteRenderer sourceRenderer;

    [Header("잔상")]
    [Min(1)] public int ghostsPerDash = 5;                 // 대시 한 번에 남길 잔상 장수.
    public float ghostLifetime = 0.28f;                    // 한 장이 완전히 사라지기까지의 시간(초).
    // 잔상 색과 시작 투명도. 흰색 플레이스홀더 스프라이트에 곱해져 옅은 푸른 잔상이 된다.
    public Color ghostTint = new(0.55f, 0.75f, 1f, 0.5f);
    public int sortingOrderOffset = -1;                    // 본체보다 이만큼 뒤에 그린다.
    // 비워두면 본체 머티리얼을 그대로 쓴다. 가산 블렌드 머티리얼을 넣으면 더 빛나는 궤적이 된다.
    public Material ghostMaterial;

    #endregion
    #region 런타임 변수

    readonly List<Ghost> ghosts = new();
    float spawnTimer;
    int ghostsLeft;
    bool wasDashing;

    struct Ghost {
        public SpriteRenderer renderer;
        public float elapsed;
        public float startAlpha;
    }

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        if (move == null) move = GetComponent<Player_move>();
    }

    // 플레이어는 FixedUpdate 에서 움직이고 Rigidbody2D Interpolation 이 걸려 있어, 화면에 보이는
    // 위치는 LateUpdate 시점의 transform 이 가장 정확하다. Player_Animator 의 프레임 교체도 Update 에서
    // 끝나므로 여기서 읽어야 그 순간의 포즈가 잔상에 담긴다.
    void LateUpdate() {
        TrackSpawning();
        FadeGhosts();
    }

    void OnDestroy() {
        foreach (Ghost g in ghosts) {
            if (g.renderer != null) Destroy(g.renderer.gameObject);
        }
        ghosts.Clear();
    }

    #endregion
    #region 잔상 생성

    void TrackSpawning() {
        bool dashing = move != null && move.IsDashing;

        if (dashing && !wasDashing) {
            // 대시가 막 시작됐다. 첫 장을 즉시 찍고 나머지를 대시 시간에 걸쳐 균등하게 뿌린다.
            ghostsLeft = Mathf.Max(1, ghostsPerDash);
            spawnTimer = 0f;
            SpawnGhost();
            ghostsLeft--;
        }
        else if (dashing && ghostsLeft > 0) {
            spawnTimer += Time.deltaTime;
            float interval = move.dashDuration / Mathf.Max(1, ghostsPerDash);
            while (spawnTimer >= interval && ghostsLeft > 0) {
                spawnTimer -= interval;
                SpawnGhost();
                ghostsLeft--;
            }
        }

        wasDashing = dashing;
    }

    void SpawnGhost() {
        if (sourceRenderer == null || sourceRenderer.sprite == null) return;

        var obj = new GameObject("DashGhost");
        obj.layer = sourceRenderer.gameObject.layer;

        Transform src = sourceRenderer.transform;
        obj.transform.SetPositionAndRotation(src.position, src.rotation);
        obj.transform.localScale = src.lossyScale; // 좌우 반전(스케일 -1)까지 그대로 따라간다.

        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sourceRenderer.sprite;
        sr.flipX = sourceRenderer.flipX;
        sr.flipY = sourceRenderer.flipY;
        sr.sharedMaterial = ghostMaterial != null ? ghostMaterial : sourceRenderer.sharedMaterial;
        sr.sortingLayerID = sourceRenderer.sortingLayerID;
        sr.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
        sr.color = ghostTint;

        ghosts.Add(new Ghost { renderer = sr, elapsed = 0f, startAlpha = ghostTint.a });
    }

    #endregion
    #region 잔상 소멸

    // 슬로모(GameSpeedController) 중에는 잔상도 함께 느리게 사라지도록 스케일 시간을 쓴다.
    void FadeGhosts() {
        for (int i = ghosts.Count - 1; i >= 0; i--) {
            Ghost g = ghosts[i];

            if (g.renderer == null) {
                ghosts.RemoveAt(i);
                continue;
            }

            g.elapsed += Time.deltaTime;
            float t = ghostLifetime > 0f ? Mathf.Clamp01(g.elapsed / ghostLifetime) : 1f;

            Color c = g.renderer.color;
            c.a = Mathf.Lerp(g.startAlpha, 0f, t);
            g.renderer.color = c;

            if (t >= 1f) {
                Destroy(g.renderer.gameObject);
                ghosts.RemoveAt(i);
            }
            else {
                ghosts[i] = g;
            }
        }
    }

    #endregion
}
