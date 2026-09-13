using UnityEngine;

// 스프라이트 한 장을 지금 그 자리에 그대로 찍어 두고 서서히 지워 잔상으로 남기는 공용 유틸리티.
// 플레이어·몬스터 어느 쪽이든 "여기를 지나갔다"를 보여줘야 할 때 한 장씩 호출해서 쓴다.
//
// 찍힌 장은 부모 없이 월드에 남긴다 — 본체가 앞으로 나아가도 잔상은 찍힌 자리에 머물러야
// 궤적으로 읽히기 때문이다. 본체가 파괴돼도 각 장이 스스로 수명을 관리하므로 씬에 남지 않는다.
// (AttackRangeIndicator·AimLineIndicator 와 같은 자리의 연출 유틸리티다.)
public class AfterimageGhost : MonoBehaviour {
    #region 컴포넌트 변수

    SpriteRenderer ghostRenderer;
    float lifetime;
    float elapsed;
    float startAlpha;

    #endregion
    #region 생성

    // source 의 현재 포즈(스프라이트·반전·스케일·정렬)를 그대로 복제한 잔상 한 장을 남긴다.
    public static void Spawn(SpriteRenderer source, Color tint, float lifetime, int sortingOrderOffset = -1, Material material = null) {
        if (source == null || source.sprite == null) return;

        GameObject obj = new GameObject("AfterimageGhost");
        obj.layer = source.gameObject.layer;

        Transform src = source.transform;
        obj.transform.SetPositionAndRotation(src.position, src.rotation);
        obj.transform.localScale = src.lossyScale; // 좌우 반전(스케일 -1)까지 그대로 따라간다.

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = source.sprite;
        sr.flipX = source.flipX;
        sr.flipY = source.flipY;
        sr.sharedMaterial = material != null ? material : source.sharedMaterial;
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = source.sortingOrder + sortingOrderOffset;

        // 본체의 색이 아니라 tint 를 그대로 쓴다. 투명해진 채로 이동하는 위저드처럼
        // 본체 알파가 0인 동안에도 잔상만은 보여야 하는 경우가 있기 때문이다.
        sr.color = tint;

        AfterimageGhost ghost = obj.AddComponent<AfterimageGhost>();
        ghost.ghostRenderer = sr;
        ghost.lifetime = Mathf.Max(0.01f, lifetime);
        ghost.startAlpha = tint.a;
    }

    #endregion
    #region 유니티 라이프 사이클

    // 슬로모(GameSpeedController) 중에는 잔상도 함께 느리게 사라지도록 스케일 시간을 쓴다.
    void Update() {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / lifetime);

        Color c = ghostRenderer.color;
        c.a = Mathf.Lerp(startAlpha, 0f, t);
        ghostRenderer.color = c;

        if (t >= 1f) Destroy(gameObject);
    }

    #endregion
}
