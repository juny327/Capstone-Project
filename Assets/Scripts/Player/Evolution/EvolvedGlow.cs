using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상위 무기 발광 (상위무기-계획.md 14-4) — 발광 재질(Emission 이 켜진 재질)의 밝기를 숨 쉬듯 바꾼다.
/// 무지개면 색상환을 돈다 (프리즘 노바). 재질은 그대로 두고 MaterialPropertyBlock 으로만 바꾼다 — 에셋이 바뀌지 않는다.
/// </summary>
public class EvolvedGlow : MonoBehaviour
{
    [Tooltip("발광 색 (HDR 배율은 intensity)")]
    [SerializeField] Color color = Color.white;
    [Min(0f)] [SerializeField] float intensity = 2.5f;
    [Range(0f, 1f)] [SerializeField] float pulse = 0.35f;
    [Min(0.1f)] [SerializeField] float period = 1.5f;
    [Tooltip("색상환을 돈다 (color 대신)")]
    [SerializeField] bool rainbow;
    [Min(0.01f)] [SerializeField] float rainbowPeriod = 2.5f;

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    readonly List<(Renderer renderer, int index)> targets = new List<(Renderer, int)>();
    MaterialPropertyBlock block;

    void Awake()
    {
        block = new MaterialPropertyBlock();

        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is LineRenderer || r is TrailRenderer || r is ParticleSystemRenderer) continue;

            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].IsKeywordEnabled("_EMISSION")) targets.Add((r, i));
        }
    }

    void LateUpdate()
    {
        if (targets.Count == 0) return;

        float wave = 1f - pulse + pulse * (0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / period));
        Color c = rainbow ? Color.HSVToRGB(Mathf.Repeat(Time.time / rainbowPeriod, 1f), 0.8f, 1f) : color;
        c *= intensity * wave;
        c.a = 1f;

        foreach ((Renderer r, int index) in targets)
        {
            if (r == null || !r.enabled) continue;
            r.GetPropertyBlock(block, index);
            block.SetColor(EmissionId, c);
            r.SetPropertyBlock(block, index);
        }
    }
}
