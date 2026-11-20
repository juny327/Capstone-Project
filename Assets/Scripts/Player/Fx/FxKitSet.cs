using UnityEngine;

/// <summary>
/// 연출 공통 재료 설정 (2026-11-19 상위 무기 연출 개선 1) — Resources/Fx/FxKitSet 한 곳에서 정한다.
/// 에셋이 없으면 공통 재료(FxKit)를 쓰는 연출은 모두 예전 방식으로 돌아간다.
///
/// 재질은 도구 `Tools/연출/공통 재료 만들기` 가 채운다. 그림은 Kenney Particle Pack(CC0)을 채널별로 묶은 것 +
/// 직접 만든 것 둘(그을음 · 불꽃 줄기) — Logs/fxkit/tools/make_kenney_sheets.ps1 · make_textures.py.
///
/// 화려함의 기준 (2026-11-20 사용자): <b>과하다 싶을 만큼 화려하고 압도적으로.</b> 처음 시안의 "적과 탄이 가려지지 않는 선" 은 거뒀다.
/// 다만 여럿이 한꺼번에 터질 때 화면이 통째로 하얗게 뜨는 것만은 막는다 (FxKit 의 동시 폭발 예산).
/// </summary>
[CreateAssetMenu(menuName = "Fx/Fx Kit Set", fileName = "FxKitSet")]
public class FxKitSet : ScriptableObject
{
    public const string ResourcePath = "Fx/FxKitSet";

    [Header("재질 — 더하기 (빛나는 것)")]
    public Material fire;
    public Material flame;
    public Material spark;
    public Material glow;
    public Material ring;
    public Material wave;
    public Material flare;
    public Material burst;
    public Material twig;
    public Material bolt;
    [Tooltip("2026-11-20 나머지 10종으로 넓히며 더한 채널 — 베기 호 · 소용돌이 · 빛기둥 · 반짝임 · 바닥의 빛 원반 · 일렁이는 기운 · 할퀸 자국")]
    public Material slash;
    public Material twirl;
    public Material beam;
    public Material star;
    public Material magic;
    public Material wisp;
    public Material claw;

    [Header("재질 — 섞기 (가리는 것)")]
    public Material smoke;
    public Material scorch;
    public Material shard;
    public Material shade;

    [Header("양")]
    [Tooltip("입자 수 배율 (그래픽 품질 '높음' 기준)")]
    [Range(0.2f, 3f)] public float amount = 1f;
    [Tooltip("그래픽 품질 '중간' · '낮음' 에서 입자 수에 더 곱한다")]
    [Range(0.1f, 1f)] public float mediumAmount = 0.6f;
    [Range(0.1f, 1f)] public float lowAmount = 0.35f;
    [Tooltip("크기 배율 — 불 덩어리 · 섬광 · 충격파 · 연기 전부")]
    [Range(0.5f, 2f)] public float scale = 1f;

    [Header("가리는 것")]
    [Tooltip("연기 진하기 (0 = 없음)")]
    [Range(0f, 1f)] public float smokeAlpha = 0.6f;
    [Tooltip("연기 수명 배율")]
    [Range(0.3f, 2f)] public float smokeLife = 1.15f;
    [Tooltip("바닥 그을음 진하기 (0 = 없음)")]
    [Range(0f, 1f)] public float scorchAlpha = 0.6f;
    [Tooltip("바닥 그을음이 남는 시간(초). 그래픽 품질 '낮음' 에서는 그리지 않는다")]
    [Min(0f)] public float scorchLife = 4f;

    [Header("밝기 배율 (블룸)")]
    [Min(0f)] public float fireGlow = 1.4f;
    [Min(0f)] public float flameGlow = 1.6f;
    [Min(0f)] public float sparkGlow = 2.6f;
    [Min(0f)] public float emberGlow = 2.2f;
    [Min(0f)] public float ringGlow = 1.6f;
    [Min(0f)] public float waveGlow = 1.2f;
    [Min(0f)] public float flareGlow = 2f;
    [Min(0f)] public float burstGlow = 2.2f;
    [Min(0f)] public float twigGlow = 2.8f;
    [Min(0f)] public float boltGlow = 3f;
    [Min(0f)] public float slashGlow = 2.4f;
    [Min(0f)] public float twirlGlow = 1.6f;
    [Min(0f)] public float beamGlow = 2.6f;
    [Min(0f)] public float starGlow = 2.6f;
    [Min(0f)] public float magicGlow = 1.6f;
    [Min(0f)] public float wispGlow = 1.7f;
    [Min(0f)] public float clawGlow = 2.6f;

    public bool Complete => fire != null && flame != null && spark != null && glow != null && ring != null && wave != null && flare != null
                            && burst != null && twig != null && bolt != null && smoke != null && scorch != null && shard != null && shade != null
                            && slash != null && twirl != null && beam != null && star != null && magic != null && wisp != null && claw != null;
}
