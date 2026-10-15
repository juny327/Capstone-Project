using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상위 무기들이 같이 쓰는 한 방 연출 + 피해 — 낙뢰(폭풍 코일 · 뇌운) · 폭발(공성포 · 처치 연쇄 밖의 폭발).
/// 한 프레임에 너무 많이 터지지 않게 Take 로 수를 막는다.
/// </summary>
public static class EvoStrikes
{
    static readonly List<Transform> found = new List<Transform>();
    static int frameStamp = -1;
    static int frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        frameStamp = -1;
        frameCount = 0;
    }

    /// <summary>이번 프레임에 하나 더 터뜨려도 되는가 (max 개까지).</summary>
    public static bool Take(int max)
    {
        if (frameStamp != Time.frameCount)
        {
            frameStamp = Time.frameCount;
            frameCount = 0;
        }

        if (frameCount >= max) return false;
        frameCount++;
        return true;
    }

    /// <summary>from(하늘 · 구름)에서 at 으로 떨어지는 번개 — 반경 안 적에게 피해 · 경직.</summary>
    public static int Lightning(Material material, Color color, Vector3 from, Vector3 at, float radius, float damage, float stun, GameObject vfx = null)
    {
        FxLine.Bolt(material, from, at + Vector3.up * 0.2f, color, 0.24f, 0.2f, 0.55f);
        FxLine.Bolt(material, from, at + Vector3.up * 0.2f, Color.white, 0.1f, 0.14f, 0.35f);
        FxLine.Ring(material, at, 0.2f, radius, color, 0.2f, 0.22f);
        Spawn(vfx, at, color, 0.8f);

        CombatFx.FindEnemies(at, radius, found);
        foreach (Transform t in found)
        {
            CombatFx.Damage(t, damage, CombatFx.Body(t), Vector3.down);
            CombatFx.Stun(t, stun);
        }

        return found.Count;
    }

    /// <summary>at 에서 터지는 폭발 — 반경 안 적에게 피해 · 바깥으로 밀기. exclude 는 빼고 센다.</summary>
    public static int Blast(MonoBehaviour runner, Material material, Color color, Vector3 at, float radius, float damage,
        float push, Transform exclude = null, GameObject vfx = null, float vfxScale = 1f, float shake = 0f)
    {
        FxLine.Ring(material, at, 0.3f, radius, color, 0.32f, 0.3f);
        FxLine.Ring(material, at, 0.1f, radius * 0.55f, Color.white, 0.16f, 0.2f);
        Spawn(vfx, at, color, vfxScale);

        CombatFx.FindEnemies(at, radius, found, exclude);
        foreach (Transform t in found)
        {
            Vector3 dir = t.position - at;
            dir.y = 0f;
            Vector3 away = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            CombatFx.Damage(t, damage, CombatFx.Body(t), away);
            if (push > 0f && runner != null) CombatFx.Push(runner, t, away, push, 0.2f);
        }

        if (shake > 0f && CameraShakeManager.Instance != null) CameraShakeManager.Instance.Shake(shake);
        return found.Count;
    }

    /// <summary>point 까지 duration 초에 끌어온다 — 적은 내비메시 위로(EnemyShove), 테스트 룸 무리 표적은 IPullable 로. 보스는 끌지 않는다.</summary>
    // 지금 쓰는 곳 없음 — 보관한 천공창 · 사슬검이 쓰던 끌어오기. 되살릴 때를 위해 남긴다 (2026-10-13)
    public static bool PullTo(MonoBehaviour runner, Transform target, Vector3 point, float duration)
    {
        if (runner == null || target == null) return false;

        if (target.TryGetComponent(out Enemy enemy)) return EnemyShove.MoveTo(runner, enemy, point, duration) != null;
        if (target.TryGetComponent(out IPullable pullable))
        {
            pullable.PullTo(point, duration);
            return true;
        }

        return false;
    }

    /// <summary>바닥에서 본 수평 방향 (길이 0 이면 fallback).</summary>
    public static Vector3 Flat(Vector3 v, Vector3 fallback)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : fallback;
    }

    /// <summary>파티클 효과를 잠깐 띄운다 (색을 맞춰서). 없으면 아무것도 안 한다.</summary>
    public static void Spawn(GameObject prefab, Vector3 at, Color color, float scale)
    {
        if (prefab == null) return;

        GameObject fx = Object.Instantiate(prefab, at + Vector3.up * 0.3f, Quaternion.identity);
        fx.transform.localScale = Vector3.one * scale;
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(color, Color.white, 0.25f));
        }
        Object.Destroy(fx, 2.5f);
    }
}
