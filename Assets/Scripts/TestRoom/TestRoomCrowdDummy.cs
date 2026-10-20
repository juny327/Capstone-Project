using System.Collections;
using UnityEngine;

/// <summary>
/// 무리 표적 하나 (상위 무기 시험 — 처치 연쇄 · 범위 · 끌어오기).
/// 훈련용 해골과 달리 체력이 있어 죽는다 — 죽으면 그 자리에서 바로 꺼지고(처치 판정이 나도록) 잠시 뒤 제자리에 다시 선다.
/// 적 AI 는 없다. 중력장에는 IPullable 로 끌린다.
/// </summary>
[RequireComponent(typeof(HitFlashController))]
public class TestRoomCrowdDummy : MonoBehaviour, IDamageable, IPullable
{
    [SerializeField] float maxHp = 40f;

    HitFlashController hitFlash;
    TestRoomCrowd owner;
    float hp;
    Coroutine pull;

    public Vector3 Home { get; set; }
    public float Hp => hp;
    public float MaxHp => maxHp;

    void Awake() => hitFlash = GetComponent<HitFlashController>();

    public void Bind(TestRoomCrowd crowd, float health)
    {
        owner = crowd;
        maxHp = health;
        Revive();
    }

    public void Revive()
    {
        if (pull != null) { StopCoroutine(pull); pull = null; }
        hp = maxHp;
        transform.position = Home;
        gameObject.SetActive(true);
    }

    public void TakeDamage(DamageInfo info)
    {
        if (!isActiveAndEnabled || hp <= 0f || info.damage <= 0 || float.IsNaN(info.damage) || float.IsInfinity(info.damage)) return;

        float dealt = Mathf.Min(info.damage, hp);
        hp -= info.damage;
        if (owner != null) owner.Record(info, dealt);

        if (hitFlash != null) hitFlash.HitFlash();
        if (DamageTextManager.Instance != null)
            DamageTextManager.Instance.ShowDamage((int)Mathf.Min(info.damage, int.MaxValue - 128f), transform.position + Vector3.up * 2f, info.isCritical);

        if (hp > 0f) return;

        // 같은 프레임에 꺼야 무기가 "이 피해로 죽었다"(WeaponHit.Killed)를 안다
        if (owner != null) owner.OnKilled(this);
        gameObject.SetActive(false);
    }

    public void PullTo(Vector3 point, float duration)
    {
        if (!isActiveAndEnabled) return;
        if (pull != null) StopCoroutine(pull);
        pull = StartCoroutine(PullRoutine(point, Mathf.Max(0.05f, duration)));
    }

    IEnumerator PullRoutine(Vector3 point, float duration)
    {
        Vector3 from = transform.position;
        point.y = from.y;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(from, point, t / duration);
            yield return null;
        }
        transform.position = point;
        pull = null;
    }
}
