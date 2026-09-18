using System.Collections.Generic;
using UnityEngine;

// 저글러 {기본 공}처럼 던지면 중력을 받아 물수제비 하듯 바닥에 통통 튀며 나아가는 투사체.
// 일직선으로 날아가는 SkillProjectile과 달리 Rigidbody2D 물리 시뮬레이션으로 움직인다 — 그래서 바닥과는
// 실제 충돌(OnCollisionEnter2D, PhysicsMaterial2D 반발력으로 튕김)로 부딪히고, 몬스터의 Hurtbox(트리거)와는
// 닿으면 즉시 피해를 준다. 콜라이더 하나가 두 역할(물리 충돌 + 트리거 판정)을 동시에 한다 — Hurtbox 쪽이
// 트리거이므로 유니티가 알아서 OnTriggerEnter2D로 보내주고, 트리거가 아닌 바닥과는 OnCollisionEnter2D로 온다.
//
// **공은 항상 폭발한다** — maxBounces만큼 튕기면 그 자리에서, 몬스터에 닿으면 즉시 그 자리에서 터진다.
// 폭발 강도(반경·데미지)는 던지는 쪽(Skill_CycleOfFate)이 Launch()로 넘겨주는 값을 그대로 따른다.
//
// **프리팹 구성: Rigidbody2D(Dynamic) + 트리거가 아닌 Collider2D + 반발력 있는 PhysicsMaterial2D가 필요합니다.**
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SkillBouncingProjectile : MonoBehaviour {
    #region 인스펙터 변수

    [Header("튕김")]
    public int maxBounces = 2; // 바닥에 이 횟수만큼 튕기면 터진다.
    public LayerMask groundLayer; // 이 레이어와 부딪힌 것만 "튕김"으로 센다. **ground 레이어를 지정하세요.**

    [Header("사운드")]
    [FMODUnity.EventRef] public string bounceSoundEvent; // event:/SFX/Skill/Cycle of fate_BBIK
    [FMODUnity.EventRef] public string explodeSoundEvent; // event:/SFX/Skill/Cycle of fate_Explode

    #endregion
    #region 런타임 변수

    Rigidbody2D body;
    LayerMask targetLayer;
    int damage;
    float remainingLifetime;
    Health ownerHealth; // 발사한 본인을 오폭하지 않기 위한 검사용.
    int bounceCount;
    SkillProjectileExplosion explosion;

    // 폭발 범위 피해 대상 중복 방지용. 명중당 한 번만 쓰고 비우므로 매번 새로 할당하지 않는다.
    static readonly HashSet<Health> explosionHitTargets = new();

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        body = GetComponent<Rigidbody2D>();
    }

    void Update() {
        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f) Destroy(gameObject); // 아무것도 못 맞히고 튕기지도 못했을 때의 안전장치.
    }

    #endregion
    #region 발사

    // launchVelocity: 던지는 순간의 초기 속도 벡터(방향 × 속력). 이후 움직임은 전부 중력·반발력에 맡긴다.
    public void Launch(Vector2 launchVelocity, LayerMask hitLayer, int hitDamage, float lifetime, Health owner, SkillProjectileExplosion explosionSettings = default) {
        targetLayer = hitLayer;
        damage = hitDamage;
        remainingLifetime = lifetime;
        ownerHealth = owner;
        explosion = explosionSettings;
        bounceCount = 0;

        body.linearVelocity = launchVelocity;
    }

    #endregion
    #region 충돌 (바닥 튕김)

    void OnCollisionEnter2D(Collision2D collision) {
        if (((1 << collision.gameObject.layer) & groundLayer) == 0) return;

        bounceCount++;
        if (!string.IsNullOrEmpty(bounceSoundEvent)) FMODUnity.RuntimeManager.PlayOneShot(bounceSoundEvent, transform.position);
        if (bounceCount < maxBounces) return;

        Explode();
        Destroy(gameObject);
    }

    #endregion
    #region 트리거 (피해 판정)

    void OnTriggerEnter2D(Collider2D other) {
        // 레이어로 먼저 걸러 관계없는 트리거마다 GetComponent를 태우지 않는다.
        if (((1 << other.gameObject.layer) & targetLayer) == 0) return;
        if (!other.TryGetComponent(out Hurtbox hurtbox) || hurtbox.OwnerHealth == null) return;
        if (hurtbox.OwnerHealth == ownerHealth) return; // 레이어가 겹치더라도 발사자 본인은 맞지 않는다.

        hurtbox.OwnerHealth.TakeDamage(damage, transform.position);

        if (HitVfx.Instance != null) HitVfx.Instance.PlaySpark(transform.position, body.linearVelocity.normalized);

        // 몬스터에 닿으면 튕긴 횟수와 상관없이 바로 터진다.
        Explode();
        Destroy(gameObject);
    }

    #endregion
    #region 폭발

    void Explode() {
        if (!string.IsNullOrEmpty(explodeSoundEvent)) FMODUnity.RuntimeManager.PlayOneShot(explodeSoundEvent, transform.position);

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosion.radius, explosion.layer);
        if (hits.Length > 0) {
            explosionHitTargets.Clear();
            foreach (Collider2D hit in hits) {
                if (!hit.TryGetComponent(out Hurtbox hurtbox) || hurtbox.OwnerHealth == null) continue;
                if (hurtbox.OwnerHealth == ownerHealth) continue;
                if (!explosionHitTargets.Add(hurtbox.OwnerHealth)) continue; // 명중 대상과 겹쳐도 두 번 맞지 않게.

                hurtbox.OwnerHealth.TakeDamage(explosion.damage, transform.position);
            }
        }

        // 전용 폭발 이펙트가 아직 없어 기존 사망 파열(원형으로 사방에 퍼지는 연출)을 빌려 쓴다.
        if (HitVfx.Instance != null) HitVfx.Instance.PlayDeath(transform.position);
    }

    #endregion
}
