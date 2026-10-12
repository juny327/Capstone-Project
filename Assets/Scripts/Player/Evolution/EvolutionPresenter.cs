using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 진화 연출 · 상위 무기 표시 (상위무기-계획.md 8-1 · 14-4).
///
///  · 진화 순간 — 화면 번쩍 · 배너 "상위 무기 — 처형자" · 충격파(주변 적을 밀어낸다) · 소리 · 흔들림
///  · 상위 무기를 든 동안 — 발밑에 테마 색 고리
///
/// 보상 카드 · 정비 창은 시간을 멈춘 채 열린다. 그 사이에 진화하면 **시간이 다시 흐를 때까지 기다렸다가** 연출한다
/// (카드 → 정비 → 다음 스테이지로 이어지므로, 보통 다음 스테이지가 시작될 때 터진다).
/// 시간 배율은 건드리지 않는다 — 카드 · 정비 · 설정창이 쓰는 시간 정지와 엉키지 않게.
///
/// 프리팹: Resources/Evolution/EvolutionFx (EvolutionSetup 이 만든다). 플레이어 아래에 붙어 씬이 바뀌어도 따라간다.
/// </summary>
public class EvolutionPresenter : MonoBehaviour
{
    public const string ResourcePath = "Evolution/EvolutionFx";

    [Header("화면")]
    [SerializeField] Image flash;
    [SerializeField] CanvasGroup banner;
    [SerializeField] TMP_Text bannerLabel;
    [SerializeField] TMP_Text bannerName;

    [Header("충격파")]
    [SerializeField] Material ringMaterial;
    [Min(0f)] [SerializeField] float pushRadius = 4.5f;
    [Min(0f)] [SerializeField] float pushDistance = 2.5f;
    [Min(0f)] [SerializeField] float stunSeconds = 0.6f;
    [Min(0f)] [SerializeField] float cameraShake = 1.2f;

    [Header("발밑 고리")]
    [SerializeField] LineRenderer aura;
    [Min(0.1f)] [SerializeField] float auraRadius = 0.95f;

    [Header("시간")]
    [Min(0f)] [SerializeField] float delayAfterResume = 0.35f;
    [Min(0.1f)] [SerializeField] float bannerHold = 1.6f;

    readonly Queue<WeaponEvolutionData> pending = new Queue<WeaponEvolutionData>();
    readonly List<Transform> targets = new List<Transform>();
    Coroutine routine;
    WeaponController weapons;

    /// <summary>재생한 연출 수 (테스트).</summary>
    public int PlayedCount { get; private set; }

    /// <summary>진화 연출이 진행 중이거나 기다리는 중인가 — 작은 조합 알림이 배너와 겹치지 않게 기다린다.</summary>
    public bool IsPlaying => routine != null;

    /// <summary>플레이어에 붙은 연출기. 없으면 Resources 프리팹으로 만든다 (없으면 화면 없이 충격파만).</summary>
    public static EvolutionPresenter Ensure(GameObject player)
    {
        EvolutionPresenter presenter = player.GetComponentInChildren<EvolutionPresenter>(true);
        if (presenter != null) return presenter;

        GameObject prefab = Resources.Load<GameObject>(ResourcePath);
        GameObject go = prefab != null ? Instantiate(prefab, player.transform) : new GameObject("EvolutionFx");
        go.name = "EvolutionFx";
        go.transform.SetParent(player.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        presenter = go.GetComponent<EvolutionPresenter>();
        if (presenter == null) presenter = go.AddComponent<EvolutionPresenter>();
        return presenter;
    }

    void Awake()
    {
        weapons = GetComponentInParent<WeaponController>();
        if (flash != null) SetAlpha(flash, 0f);
        if (banner != null) banner.alpha = 0f;
        BuildAura();
    }

    public void Play(WeaponEvolutionData evolution)
    {
        if (evolution == null) return;

        pending.Enqueue(evolution);
        if (routine == null) routine = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        while (pending.Count > 0)
        {
            // 카드 · 정비 창이 닫히고 시간이 다시 흐를 때까지
            while (Time.timeScale < 0.99f) yield return null;
            yield return new WaitForSecondsRealtime(delayAfterResume);
            while (Time.timeScale < 0.99f) yield return null;

            yield return Sequence(pending.Dequeue());
        }

        routine = null;
    }

    IEnumerator Sequence(WeaponEvolutionData evolution)
    {
        PlayedCount++;
        Color color = evolution.color;

        if (bannerLabel != null) bannerLabel.text = "상위 무기";
        if (bannerName != null)
        {
            bannerName.text = evolution.Title;
            bannerName.color = color;
        }

        Shockwave(color);

        if (evolution.sound != null && SoundManager.Instance != null)
            SoundManager.Instance.PlayClip(evolution.sound, evolution.soundVolume, 1f);

        // 번쩍 → 배너가 커졌다 줄어들며 자리 잡는다 → 잠깐 머문다 → 흐려진다
        float t = 0f;
        const float pop = 0.25f;
        while (t < pop)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / pop);
            if (flash != null) SetAlpha(flash, 0.55f * (1f - k));
            if (banner != null)
            {
                banner.alpha = k;
                banner.transform.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, 1f - (1f - k) * (1f - k));
            }
            yield return null;
        }

        if (flash != null) SetAlpha(flash, 0f);

        yield return new WaitForSecondsRealtime(bannerHold);

        t = 0f;
        const float fade = 0.4f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            if (banner != null) banner.alpha = 1f - Mathf.Clamp01(t / fade);
            yield return null;
        }

        if (banner != null) banner.alpha = 0f;
    }

    /// <summary>주변 적을 밀어내고 고리 두 겹을 퍼뜨린다 — 숨 돌릴 틈 + 볼거리.</summary>
    void Shockwave(Color color)
    {
        Vector3 at = transform.position;

        FxLine.Ring(ringMaterial, at, 0.4f, pushRadius, color, 0.35f, 0.45f);
        FxLine.Ring(ringMaterial, at, 0.2f, pushRadius * 0.6f, Color.white, 0.18f, 0.3f);

        CombatFx.FindEnemies(at, pushRadius, targets);
        foreach (Transform enemy in targets)
        {
            Vector3 dir = enemy.position - at;
            dir.y = 0f;
            CombatFx.Stun(enemy, stunSeconds);
            CombatFx.Push(this, enemy, dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward, pushDistance, 0.25f);
        }

        if (CameraShakeManager.Instance != null && cameraShake > 0f)
            CameraShakeManager.Instance.Shake(cameraShake);
    }

    // ───────── 발밑 고리 ─────────

    void BuildAura()
    {
        if (aura == null)
        {
            var go = new GameObject("Aura");
            go.transform.SetParent(transform, false);
            aura = go.AddComponent<LineRenderer>();
            aura.sharedMaterial = ringMaterial;
            aura.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            aura.receiveShadows = false;
        }

        aura.useWorldSpace = false;
        aura.loop = true;
        aura.alignment = LineAlignment.TransformZ;
        aura.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 바닥에 눕힌다
        aura.transform.localPosition = new Vector3(0f, 0.06f, 0f);

        const int n = 40;
        aura.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            aura.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * auraRadius);
        }

        aura.enabled = false;
    }

    void Update()
    {
        if (aura == null) return;

        if (weapons == null) weapons = GetComponentInParent<WeaponController>();

        IWeapon active = weapons != null ? weapons.ActiveWeapon : null;
        WeaponData data = active != null ? active.Data : null;
        bool show = data != null && data.IsEvolved && data.HasThemeColor;

        aura.enabled = show;
        if (!show) return;

        // 은은하게 숨 쉬듯 — 너무 밝으면 적 탄을 가린다
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
        Color c = data.themeColor;
        c.a = 0.35f + 0.25f * pulse;
        aura.startColor = c;
        aura.endColor = c;
        aura.widthMultiplier = 0.06f + 0.03f * pulse;
    }

    static void SetAlpha(Graphic g, float a)
    {
        Color c = g.color;
        c.a = a;
        g.color = c;
    }
}
