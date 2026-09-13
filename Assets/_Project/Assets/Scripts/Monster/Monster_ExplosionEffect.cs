using UnityEngine;

// 자폭 몬스터가 터진 자리에 남는 폭발 링. 스스로 퍼지며 옅어지다 사라진다.
//
// 터뜨린 몬스터는 Health.Die() 가 곧바로 Destroy 하므로 연출을 그 오브젝트에 얹을 수 없다.
// 그래서 부모 없는 독립 오브젝트로 띄우고, 이 컴포넌트가 자기 수명을 직접 관리한다.
// (HitVfx 처럼 싱글턴으로 두지 않은 이유는 폭발 링이 반경마다 크기가 다르고 동시에 여러 개가
//  겹칠 수 있어, 하나를 돌려쓰는 것보다 그때그때 만들고 버리는 편이 단순하기 때문이다.)
public class Monster_ExplosionEffect : MonoBehaviour {
    #region 컴포넌트 변수

    SpriteRenderer ring;
    float diameter;
    float duration;
    float elapsed;
    Color startColor;

    #endregion
    #region 생성

    // center 를 중심으로 radius 판정 반경까지 퍼지는 링을 띄운다.
    public static void Play(Vector2 center, float radius, Color color, float duration = 0.35f) {
        SpriteRenderer ring = AttackRangeIndicator.Create(radius, color);
        ring.gameObject.name = "MonsterExplosionEffect";
        ring.transform.position = center;

        // Create() 는 판정 반경 그대로의 크기로 만들어 둔다. 아래 Update() 가 도는 것은 다음 프레임부터라,
        // 여기서 시작 크기를 미리 맞춰 두지 않으면 첫 프레임만 전체 크기로 번쩍 나타났다가 줄어든다.
        ring.transform.localScale = new Vector3(radius, radius, 1f);
        ring.gameObject.SetActive(true); // AttackRangeIndicator 는 꺼진 채로 만들어진다.

        Monster_ExplosionEffect effect = ring.gameObject.AddComponent<Monster_ExplosionEffect>();
        effect.ring = ring;
        effect.diameter = radius * 2f;
        effect.duration = Mathf.Max(0.01f, duration);
        effect.startColor = color;
    }

    #endregion
    #region 유니티 라이프 사이클

    void Update() {
        // 히트스톱(timeScale 0.02) 중에 폭발이 얼어붙지 않도록 프로젝트의 다른 연출과 같이 unscaled 기준으로 돈다.
        elapsed += Time.unscaledDeltaTime;

        float t = elapsed / duration;
        if (t >= 1f) {
            Destroy(gameObject);
            return;
        }

        // 처음부터 판정 반경의 절반 크기로 시작해 살짝 넘겨서 멈춘다.
        // 0에서 키우면 퍼지는 도중의 링 크기가 실제 판정보다 작아, 이미 맞은 플레이어가 "안 닿았는데 맞았다"고 느낀다.
        float eased = 1f - (1f - t) * (1f - t);
        float scale = Mathf.Lerp(diameter * 0.5f, diameter * 1.05f, eased);
        transform.localScale = new Vector3(scale, scale, 1f);

        Color c = startColor;
        c.a = startColor.a * (1f - t);
        ring.color = c;
    }

    #endregion
}
