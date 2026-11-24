using System;
using UnityEngine;

/// <summary>
/// 재화(코인) 설정 — 몬스터별 코인 · 줍기 · 클리어 보너스 · 소리.
///
/// 수치를 바꿀 때는 이 에셋(Resources/Economy/EconomySettings)만 고치면 된다.
/// 처음 한 번은 도구가 만든다: Tools / 재화 · 상점 / 전체 만들기 (이미 있으면 값을 덮어쓰지 않는다).
/// </summary>
[CreateAssetMenu(menuName = "Economy/Settings", fileName = "EconomySettings")]
public class EconomySettings : ScriptableObject
{
    public const string ResourcePath = "Economy/EconomySettings";

    [Serializable]
    public struct Drop
    {
        public EnemyData enemy;
        [Min(0)] public int coins;
    }

    [Header("재화")]
    [Tooltip("화면에 보이는 재화 이름")]
    public string currencyName = "크레딧";
    public Sprite currencyIcon;

    [Header("몬스터가 떨어뜨리는 코인")]
    [Tooltip("땅에 떨어지는 코인 (Prefabs/Economy/Coin). 모양을 바꾸려면 이 프리팹만 바꾼다")]
    public CoinPickup coinPrefab;

    [Tooltip("몬스터 종류별 코인. 목록에 없는 몬스터는 기본값")]
    public Drop[] drops;

    [Min(0)] public int defaultCoins = 1;

    [Tooltip("몬스터 코인 배율 (정리/게임흐름-밸런스-변경.md, 2026-11-05 — 처치 목표가 늘어 0.7). 소수는 확률로 반올림한다 (코인 1 × 0.7 = 70% 확률로 1). " +
             "아래 스테이지별 배율이 있으면 그쪽을 쓴다")]
    [Range(0f, 2f)] public float coinMultiplier = 0.7f;

    // 2026-11-17 — 뒤 스테이지로 갈수록 수입을 키운다 (밸런스 결정 4 "넉넉하게"). 처치 목표 60 · 85 · 110 과
    // 스테이지별 몬스터(1 = A · B, 2 = C · D 추가, 3 = 다섯 종)에 맞춰 한 판 약 407 (스테이지 65 · 130 · 214)이 되게 잡은 값.
    // 2026-11-24 — 흐름 (사용자: "초반은 잔잔하게, 아무리 빨라도 스테이지 2 중간 정비에서 상위 무기 하나, 그 뒤 화력"):
    // 0.6 · 0.65 · 0.8 → 0.45 · 0.6 · 0.9 — 뒤로 갈수록 더. 상위 무기는 스테이지 2 부터 열린다 (ShopCatalog.evolutionFromStage).
    // 캐릭터별 배율은 CharacterData.coinMultiplier.
    [Tooltip("스테이지별 몬스터 코인 배율 [스테이지 1, 2, 3 …]. 표보다 큰 스테이지는 마지막 값, 비우면 coinMultiplier 하나")]
    public float[] stageCoinMultipliers = { 0.45f, 0.6f, 0.9f };

    [Tooltip("한 번에 떨어뜨리는 코인 오브젝트 수의 상한. 넘으면 한 개가 여러 크레딧을 갖는다")]
    [Min(1)] public int maxCoinObjects = 4;

    [Tooltip("코인이 튀어 흩어지는 거리 (최소, 최대 m)")]
    public Vector2 scatterRadius = new Vector2(0.4f, 1.3f);

    [Header("줍기")]
    [Tooltip("이 거리 안에 들어오면 코인이 날아온다 (m)")]
    [Min(0f)] public float magnetRadius = 3.5f;

    [Min(0.1f)] public float flySpeed = 8f;
    [Min(0f)] public float flyAcceleration = 30f;

    [Tooltip("이 거리 안에 닿으면 지갑에 들어간다 (m)")]
    [Min(0.05f)] public float pickupDistance = 0.7f;

    [Header("스테이지 클리어")]
    [Tooltip("스테이지 1 · 2 · 3 클리어 보너스")]
    public int[] clearBonus = { 10, 15, 20 };

    [Tooltip("클리어하면 남은 코인이 모두 플레이어에게 날아온다 — 상점에서 쓸 수 있게")]
    public bool collectAllOnClear = true;

    [Header("소리")]
    public GameSoundSet.Entry pickupSound = new GameSoundSet.Entry();

    /// <summary>이 몬스터가 떨어뜨리는 코인 (크레딧).</summary>
    public int CoinsFor(EnemyData enemy)
    {
        if (enemy != null && drops != null)
        {
            for (int i = 0; i < drops.Length; i++)
            {
                if (drops[i].enemy == enemy)
                    return drops[i].coins;
            }
        }

        return defaultCoins;
    }

    /// <summary>그 스테이지의 몬스터 코인 배율. 표가 비면 coinMultiplier, 표보다 큰 스테이지는 마지막 값.</summary>
    public float CoinMultiplierFor(int stageIndex)
    {
        if (stageCoinMultipliers == null || stageCoinMultipliers.Length == 0)
            return coinMultiplier;

        int i = Mathf.Clamp(stageIndex - 1, 0, stageCoinMultipliers.Length - 1);
        return Mathf.Max(0f, stageCoinMultipliers[i]);
    }

    /// <summary>스테이지 클리어 보너스. 표보다 큰 스테이지는 마지막 값.</summary>
    public int ClearBonusFor(int stageIndex)
    {
        if (clearBonus == null || clearBonus.Length == 0)
            return 0;

        int i = Mathf.Clamp(stageIndex - 1, 0, clearBonus.Length - 1);
        return Mathf.Max(0, clearBonus[i]);
    }

    static EconomySettings cached;

    public static EconomySettings Load()
    {
        if (cached == null)
            cached = Resources.Load<EconomySettings>(ResourcePath);

        return cached;
    }
}
