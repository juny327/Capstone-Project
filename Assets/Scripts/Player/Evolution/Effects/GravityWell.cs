using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>실행 중에 생기는 중력장 하나. 끝나면 스스로 지운다.</summary>
public class GravityWell : MonoBehaviour
{
    GravityWellEffect cfg;
    float damage;
    MeshRenderer disc;
    Transform hole;          // 팀원 블랙홀 이펙트 (있으면 원판 · 고리 대신)
    Vector3 holeScale;
    AudioSource holeAudio;   // 블랙홀 반복음 — 커질 때 들어오고 오므라들 때 빠진다
    float holeVolume;
    readonly HashSet<Transform> pulled = new HashSet<Transform>();
    readonly List<Transform> found = new List<Transform>();

    // 보조 칸의 중력 대검이 연 중력장은 흔들지 않는다 (스스로 자주 내려찍는다)
    bool shake = true;

    public static GravityWell Spawn(GravityWellEffect cfg, Vector3 at, float collapseDamage, bool shakeCamera = true)
    {
        var go = new GameObject("GravityWell");
        go.transform.position = at;

        var well = go.AddComponent<GravityWell>();
        well.cfg = cfg;
        well.damage = collapseDamage;
        well.shake = shakeCamera;
        well.BuildBlackHole();
        if (well.hole == null) well.BuildDisc();
        well.StartCoroutine(well.Run());
        return well;
    }

    /// <summary>
    /// 팀원 블랙홀 스킬과 같은 이펙트를 띄운다. 크기만 맞추고 판정 · 시간은 중력 대검 값 그대로 —
    /// 소용돌이 끝이 끌어당기는 반경(pullRadius × blackHoleFill)에 오게 키운다.
    /// </summary>
    void BuildBlackHole()
    {
        if (cfg.blackHolePrefab == null) return;

        GameObject fx = Instantiate(cfg.blackHolePrefab, transform.position + Vector3.up * 0.05f, Quaternion.identity, transform);

        // 원본 프리팹에는 단단한 충돌체가 붙어 있다 — 끌려오는 적 · 지나가는 총알을 막지 않게 뗀다
        foreach (Collider c in fx.GetComponentsInChildren<Collider>(true)) Destroy(c);

        // 반복음은 하나만 남겨 효과음 그룹 · 음량에 맞춘다. 원본은 음량 1 로 바로 울리므로 0 에서 시작한다
        foreach (AudioSource a in fx.GetComponentsInChildren<AudioSource>(true))
        {
            a.volume = 0f;
            if (holeAudio != null || cfg.blackHoleVolume <= 0f) { a.enabled = false; continue; }

            holeAudio = a;
            holeVolume = cfg.blackHoleVolume;
            if (SoundManager.Instance != null)
            {
                a.outputAudioMixerGroup = SoundManager.Instance.SfxGroup;
                holeVolume *= SoundManager.Instance.SfxVolume;
            }
        }

        float radius = 0f;
        foreach (MeshRenderer r in fx.GetComponentsInChildren<MeshRenderer>(true))
            radius = Mathf.Max(radius, r.bounds.extents.x, r.bounds.extents.z);

        float k = radius > 0.01f ? cfg.pullRadius * cfg.blackHoleFill / radius : 1f;
        hole = fx.transform;
        holeScale = hole.localScale * k;
        hole.localScale = holeScale * 0.2f;
    }

    void BuildDisc()
    {
        if (cfg.swirlMaterial == null) return;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "Swirl";
        quad.transform.SetParent(transform, false);
        quad.transform.localPosition = Vector3.up * 0.08f;
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = Vector3.one * 0.1f;

        disc = quad.GetComponent<MeshRenderer>();
        disc.sharedMaterial = cfg.swirlMaterial;
        disc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        disc.receiveShadows = false;
    }

    IEnumerator Run()
    {
        CombatFx.PlaySound(cfg.openSound);
        CombatFx.PlaySound(cfg.openRumbleSound);

        float t = 0f;
        float scan = 0f;
        float ring = 0f;
        float size = cfg.pullRadius * 2f;

        while (t < cfg.duration)
        {
            float dt = Time.deltaTime;
            t += dt;
            scan -= dt;
            ring -= dt;

            // 원판이 커지며 돈다
            if (disc != null)
            {
                float grow = Mathf.Clamp01(t / 0.2f);
                disc.transform.localScale = Vector3.one * size * Mathf.Lerp(0.2f, 1f, grow) * (1f + 0.04f * Mathf.Sin(t * 20f));
                disc.transform.Rotate(0f, 0f, -540f * dt, Space.Self);
            }

            // 블랙홀 — 처음 0.2초 커지고, 붕괴 직전 0.15초 오므라든다
            if (hole != null)
            {
                float grow = Mathf.Clamp01(t / 0.2f);
                float left = Mathf.Clamp01((cfg.duration - t) / 0.15f);
                float shrink = Mathf.Lerp(0.35f, 1f, left);
                hole.localScale = holeScale * Mathf.Lerp(0.2f, 1f, grow) * shrink;
                if (holeAudio != null) holeAudio.volume = holeVolume * grow * left;
            }

            // 안으로 빨려드는 고리 (블랙홀 이펙트가 없을 때만)
            if (hole == null && ring <= 0f)
            {
                ring = 0.22f;
                FxLine.Ring(cfg.ringMaterial, transform.position, cfg.pullRadius, 0.3f, cfg.color, 0.12f, 0.35f);
            }

            if (scan <= 0f)
            {
                scan = cfg.scanInterval;
                Pull(cfg.duration - t);
            }

            yield return null;
        }

        Collapse();
        Destroy(gameObject);
    }

    void Pull(float remaining)
    {
        CombatFx.FindEnemies(transform.position, cfg.pullRadius, found);

        foreach (Transform enemy in found)
        {
            CombatFx.Stun(enemy, remaining + 0.25f);
            if (!pulled.Add(enemy)) continue;

            Vector2 jitter = Random.insideUnitCircle * cfg.gatherRadius;
            Vector3 point = transform.position + new Vector3(jitter.x, 0f, jitter.y);
            float travel = Mathf.Clamp(remaining * 0.6f, 0.2f, 0.6f);

            if (enemy.TryGetComponent(out Enemy e)) EnemyShove.MoveTo(this, e, point, travel);
            else if (enemy.TryGetComponent(out IPullable pullable)) pullable.PullTo(point, travel);   // 테스트 룸 무리 표적
            else continue;   // 보스는 끌지 않는다

            FxLine.Line(cfg.ringMaterial, CombatFx.Body(enemy), transform.position + Vector3.up * 0.5f, cfg.color, 0.08f, 0.25f);
        }
    }

    void Collapse()
    {
        Vector3 at = transform.position;

        CombatFx.FindEnemies(at, cfg.collapseRadius, found);
        foreach (Transform enemy in found)
        {
            Vector3 dir = enemy.position - at;
            dir.y = 0f;
            CombatFx.Damage(enemy, damage, CombatFx.Body(enemy), dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward);
            CombatFx.Stun(enemy, cfg.collapseStun);
        }

        FxLine.Ring(cfg.ringMaterial, at, 0.3f, cfg.collapseRadius * 1.2f, cfg.color, 0.4f, 0.35f);
        FxLine.Ring(cfg.ringMaterial, at, 0.2f, cfg.collapseRadius * 0.7f, Color.white, 0.2f, 0.22f);

        if (cfg.collapsePrefab != null)
        {
            GameObject fx = Instantiate(cfg.collapsePrefab, at + Vector3.up * 0.3f, Quaternion.identity);
            fx.transform.localScale = Vector3.one * cfg.collapseScale;
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(cfg.color, Color.white, 0.2f));
            }
            Destroy(fx, 2.5f);
        }

        CombatFx.PlaySound(cfg.collapseSound);
        CombatFx.PlaySound(cfg.collapseBassSound);
        if (shake && cfg.cameraShake > 0f && CameraShakeManager.Instance != null)
            CameraShakeManager.Instance.Shake(cfg.cameraShake);
    }
}
