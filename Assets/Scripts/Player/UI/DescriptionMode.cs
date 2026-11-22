using System;
using UnityEngine;

/// <summary>
/// 설명 글 — 간단 / 상세 (2026-11-22 사용자: "설명들이 간단 명료했으면 · 위에 토글로 상세한 설명을 켜고 끌 수 있게").
///
/// 기본은 간단(핵심 한 줄). 켜면 상세(전체 설명 · 수치 · 조건). 정비 창 · 레벨업 창 · 보상 창 · 로비가 같은 값을 쓰고,
/// 어느 창에서 바꿔도 모두 따라 바뀐다 (<see cref="Changed"/>). 값은 PlayerPrefs 에 남는다.
/// 토글은 <see cref="DetailToggle"/>. 글을 짜는 곳은 <see cref="DescText"/>.
/// </summary>
public static class DescriptionMode
{
    const string Pref = "UI.DetailDescription";

    static int state = -1;   // −1 = 아직 읽지 않음

    /// <summary>간단 ↔ 상세가 바뀌었다 (true = 상세). 창은 이때 카드 글을 다시 그린다.</summary>
    public static event Action<bool> Changed;

    public static bool Detail
    {
        get
        {
            if (state < 0) state = PlayerPrefs.GetInt(Pref, 0) == 1 ? 1 : 0;
            return state == 1;
        }
        set
        {
            if (Detail == value) return;

            state = value ? 1 : 0;
            PlayerPrefs.SetInt(Pref, state);
            PlayerPrefs.Save();
            Changed?.Invoke(value);
        }
    }

    /// <summary>지금 모드의 글. 한쪽이 비어 있으면 다른 쪽을 쓴다 (간단 설명을 아직 안 쓴 항목은 예전 글 그대로).</summary>
    public static string Pick(string brief, string detail)
    {
        bool hasBrief = !string.IsNullOrWhiteSpace(brief);
        bool hasDetail = !string.IsNullOrWhiteSpace(detail);

        if (Detail) return hasDetail ? detail : hasBrief ? brief : "";
        return hasBrief ? brief : hasDetail ? detail : "";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        state = -1;
        Changed = null;
    }
}
