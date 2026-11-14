using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 무기 연출의 빛 설정 (2026-11-14 연출 개선 2 — 공통 빛). Resources/Fx/FxGlowSettings.asset.
/// 세기를 한 곳에서 바꾼다: 빛 번짐(연출 재질 밝기) · 바닥 빛 · 큰 순간의 점 조명.
/// 에셋이 없으면 세 가지 모두 꺼진다 (예전 모습 그대로).
///
/// 빛 번짐 세기 기준 — 약: 심지 1.6 · 몸통 1.2 / 중: 2.5 · 1.5 / 강: 4 · 2
/// 스테이지의 블룸은 문턱 1.1 · 세기 0.28 이라, 색이 1.1 을 넘는 부분만 은은하게 번진다.
/// </summary>
public class FxGlowSettings : ScriptableObject
{
    public const string ResourcePath = "Fx/FxGlowSettings";

    [Header("빛 번짐 — 연출 재질의 밝기 배율 (1 이면 끔)")]
    [Tooltip("흰 심지 · 총구 섬광 · 광선 심지처럼 흰색에 가까운 부분. 약 1.6 · 중 2.5 · 강 4")]
    [Min(1f)] public float coreGlow = 2.5f;

    [Tooltip("무기 색 몸통 (탄 궤적 · 검격 · 줄기 바깥 겹). 약 1.2 · 중 1.5 · 강 2")]
    [Min(1f)] public float bodyGlow = 1.5f;

    [Header("바닥 빛 — 쏘는 자리 · 맞는 자리에 잠깐 깔리는 빛")]
    [Tooltip("더하기 재질 (Assets/Prefabs/Attachment/FxLine.mat). 비우면 바닥 빛 없음. 바닥 셰이더가 점 조명을 받지 않아 빛을 따로 그린다")]
    public Material groundMaterial;

    [Tooltip("바닥 빛 세기. 0 이면 끔")]
    [Range(0f, 1f)] public float groundStrength = 0.5f;

    [Header("캐릭터 조명 — 큰 순간(폭발 · 붕괴 · 큰 광선)에만 켜는 점 조명")]
    [Tooltip("동시에 켜지는 조명 수. 0 이면 끔")]
    [Range(0, 6)] public int maxLights = 4;

    [Tooltip("조명 세기 (켜지는 순간의 값 — 바로 줄어든다)")]
    [Min(0f)] public float lightIntensity = 6f;

    [Header("테스트 룸")]
    [Tooltip("테스트 룸의 후처리를 스테이지와 같게 덮는 프로필 (스테이지 1 의 Atmosphere). 비우면 테스트 룸은 예전 그대로 — 블룸이 스테이지의 약 10배라 빛이 실제 게임보다 훨씬 세게 보인다")]
    public VolumeProfile stageLook;
}
