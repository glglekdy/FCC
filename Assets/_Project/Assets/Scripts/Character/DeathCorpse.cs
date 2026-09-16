using System.Collections;
using UnityEngine;

// 죽은 자리에 남아 사망 모션을 재생하는 잔해.
// Health.Die() 가 이벤트를 발행한 직후 곧바로 Destroy(gameObject) 를 호출하므로 몬스터 본인은
// 쓰러지는 모습을 보여 줄 시간이 없다. 그래서 사망 모션만 이 오브젝트가 대신 이어받는다.
// HitReactor 가 OnDeath 를 받아 이 프리팹을 찍어 낸다.
public class DeathCorpse : MonoBehaviour {
    #region 인스펙터 변수

    [Header("남는 시간")]
    public float holdDuration = 2f; // 쓰러진 뒤 그대로 누워 있는 시간.
    public float fadeDuration = 0.8f; // 서서히 사라지는 시간.

    #endregion
    #region 컴포넌트 변수

    SpriteRenderer[] renderers;

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    void Start() {
        StartCoroutine(FadeRoutine());
    }

    #endregion
    #region 소멸 연출

    // 전부 실시간(unscaled) 기준으로 센다. 처치 순간에는 히트스톱으로 timeScale 이 거의 0까지 떨어지는데,
    // 스케일 시간으로 세면 그동안 시체가 얼어붙은 채로 멈춰 있다.
    IEnumerator FadeRoutine() {
        yield return new WaitForSecondsRealtime(holdDuration);

        float elapsed = 0f;
        while (elapsed < fadeDuration) {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(1f - Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        Destroy(gameObject);
    }

    void SetAlpha(float alpha) {
        for (int i = 0; i < renderers.Length; i++) {
            if (renderers[i] == null) continue;

            Color color = renderers[i].color;
            color.a = alpha;
            renderers[i].color = color;
        }
    }

    #endregion
}
