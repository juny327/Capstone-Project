using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 설정 '카메라 거리' (2026-11-26, 남은 작업 2-6 · 7-2) — 게임 카메라가 플레이어를 따라가는 거리에 배율을 곱한다.
///  · 가까이 ×1    = 지금 (11-24 에 ×0.85 당긴 거리 — 연출이 크게 보인다)
///  · 보통   ×1.18 = 그 전 거리 (0, 17, −10) — 뒤 · 옆에서 오는 적이 일찍 보인다
///  · 멀리   ×1.35
/// 대상은 각 씬의 게임 카메라 (CameraController 가 붙은 시네머신 카메라 — 스테이지 1 · 2 · 3 · 보스 · 테스트 룸).
/// 보스 등장 · 엔딩처럼 잠깐 만드는 카메라는 건드리지 않는다. 씬의 원래 거리는 처음 볼 때 기억해 두고 거기에 곱한다.
/// 씬이 바뀔 때마다 · 설정을 바꿀 때 GameSettings 가 부른다.
/// </summary>
public static class CameraDistance
{
    static readonly float[] Factors = { 1f, 17f / 14.45f, 1.35f };
    static readonly string[] Names = { "가까이", "보통", "멀리" };

    // 시네머신 카메라마다 씬에 적힌 원래 거리 (씬을 다시 불러오면 새 오브젝트라 다시 기억한다)
    static readonly Dictionary<int, Vector3> baseOffsets = new Dictionary<int, Vector3>();

    public static int Count => Factors.Length;
    public static float Factor => Factors[Mathf.Clamp(GameSettings.CameraDistance, 0, Factors.Length - 1)];
    public static string Name(int level) => Names[Mathf.Clamp(level, 0, Names.Length - 1)];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => baseOffsets.Clear();

    public static void Apply()
    {
        float factor = Factor;
        foreach (CameraController controller in Object.FindObjectsByType<CameraController>(FindObjectsSortMode.None))
        {
            if (!controller.TryGetComponent(out CinemachineFollow follow)) continue;
            int id = follow.GetInstanceID();
            if (!baseOffsets.TryGetValue(id, out Vector3 baseOffset))
            {
                baseOffset = follow.FollowOffset;
                baseOffsets[id] = baseOffset;
            }
            follow.FollowOffset = baseOffset * factor;
        }
    }
}
