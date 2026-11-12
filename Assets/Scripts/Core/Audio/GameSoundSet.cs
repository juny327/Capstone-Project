using UnityEngine;

/// <summary>
/// 맞힘 소리 종류 (2026-11-02) — 공격마다 맞는 느낌이 다르게.
/// 레이저 계열(충전 레이저 · 프리즘 노바 · 태양창)은 금속 '띵' 대신 '치익', 총알은 둔탁한 '퍽', 근접은 몸에 닿는 '퍽'.
/// 그 밖(부착물 효과 · 범위 공격 · 전격 소총 · 반사 탄)은 Normal — 공용 적중음.
/// </summary>
public enum HitSound
{
    Normal,
    Laser,
    Bullet,
    Melee,
    Electric,

    // Skill hit sounds. Existing values above are intentionally preserved.
    FireBall,
    ChainLightning,
    BlackHole,
    Tornado,
    Shuriken,
    ElectricShot
}

/// <summary>
/// 스킬이 발동될 때 재생할 시전 사운드 종류.
/// 피격 사운드(HitSound)와 분리해서 시전/착탄의 역할을 명확하게 구분한다.
/// </summary>
public enum SkillCastSound
{
    FireBall,
    ChainLightning,
    BlackHole,
    Tornado,
    Shuriken,
    ElectricShot
}

/// <summary>
/// 게임 이벤트에 붙일 소리 묶음.
///
/// 클립을 코드가 아니라 **데이터에 두는 이유**: 소리를 바꿀 때마다 코드를 고치지 않기 위해서다.
/// 무기 소리를 `WeaponData` 에 두기로 한 것과 같은 이유다.
/// </summary>
[CreateAssetMenu(menuName = "Audio/Game Sound Set", order = 0)]
public class GameSoundSet : ScriptableObject
{
    /// <summary>소리 하나. 클립을 여러 개 넣으면 돌려 가며 쓴다.</summary>
    [System.Serializable]
    public class Entry
    {
        [Tooltip("여러 개면 무작위로 하나를 고른다. 같은 소리가 반복되면 기계음처럼 들린다")]
        public AudioClip[] clips;

        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("재생마다 피치를 이 범위에서 무작위로 잡는다. 1,1 이면 고정")]
        public Vector2 pitchRange = new Vector2(0.96f, 1.04f);

        [Tooltip("이 시간(초) 안에 또 나오면 건너뛴다. 처치음처럼 자주 나는 소리에 필요하다")]
        [Min(0f)] public float minInterval;

        // 마지막 재생 시각. 런타임 전용이라 직렬화하지 않는다.
        [System.NonSerialized] public float lastPlayedAt = -999f;

        public bool HasClip => clips != null && clips.Length > 0;

        public AudioClip Pick()
        {
            if (!HasClip) return null;

            return clips[Random.Range(0, clips.Length)];
        }

        public float PickPitch()
        {
            float min = Mathf.Min(pitchRange.x, pitchRange.y);
            float max = Mathf.Max(pitchRange.x, pitchRange.y);

            if (min <= 0f) min = 1f;
            if (max <= 0f) max = 1f;

            return Random.Range(min, max);
        }
    }

    [Header("성장")]
    public Entry levelUp = new Entry();
    public Entry upgradeOpen = new Entry();
    public Entry weaponAcquired = new Entry();

    [Header("전투")]
    [Tooltip("적을 맞혔을 때. 관통 · 광역이면 한 프레임에 여러 번 들어온다")]
    public Entry hit = new Entry();

    [Tooltip("치명타. 일반 명중과 확실히 달라야 체감된다")]
    public Entry criticalHit = new Entry();

    [Tooltip("레이저 계열이 적을 맞혔을 때 (치명타 · 일반 모두 — 치명타면 조금 크게). 비우면 일반 적중음")]
    public Entry laserHit = new Entry();

    [Tooltip("총알이 적을 맞혔을 때 (치명타는 criticalHit). 비우면 일반 적중음")]
    public Entry bulletHit = new Entry();

    [Tooltip("근접 무기 판정이 적에게 들어갔을 때 — 무기의 베는 소리(fireSounds) 밑에 깔리는 몸통 소리 (치명타는 criticalHit). 비우면 일반 적중음")]
    public Entry meleeHit = new Entry();

    [Tooltip("전기 계열(전격 소총 · 퍼지는 전기 · 낙뢰)이 적을 맞혔을 때 '치직' (치명타 · 일반 모두 — 치명타면 조금 크게). 비우면 일반 적중음")]
    public Entry electricHit = new Entry();

    [Header("스킬 - 피격")]
    [Tooltip("파이어볼이 적에게 명중했을 때")]
    public Entry fireBallHit = new Entry();

    [Tooltip("연쇄 번개가 적에게 명중했을 때")]
    public Entry chainLightningHit = new Entry();

    [Tooltip("블랙홀이 적에게 피해를 줄 때. minInterval 권장")]
    public Entry blackHoleHit = new Entry();

    [Tooltip("토네이도가 적에게 피해를 줄 때. minInterval 권장")]
    public Entry tornadoHit = new Entry();

    [Tooltip("표창이 적에게 명중했을 때. minInterval 권장")]
    public Entry shurikenHit = new Entry();

    [Tooltip("전격 샷이 적에게 적중해 경직을 걸 때")]
    public Entry electricShotHit = new Entry();

    [Header("스킬 - 시전")]
    [Tooltip("파이어볼 발사 시")]
    public Entry fireBallCast = new Entry();

    [Tooltip("연쇄 번개 발동 시. 연쇄 전이마다 재생하지 않고 1회")]
    public Entry chainLightningCast = new Entry();

    [Tooltip("블랙홀 생성 시")]
    public Entry blackHoleCast = new Entry();

    [Tooltip("토네이도 생성 시")]
    public Entry tornadoCast = new Entry();

    [Tooltip("표창 공전 시작 시")]
    public Entry shurikenCast = new Entry();

    [Tooltip("전격 샷 발사 시")]
    public Entry electricShotCast = new Entry();

    [Tooltip("흡혈 성공 시. Drain은 피격음 대신 회복 성공음으로 처리")]
    public Entry drainHeal = new Entry();

    [Tooltip("총알 · 광선이 벽 · 엄폐물에 막혔을 때. 산탄은 여러 알이 한꺼번에 막히므로 minInterval 을 꼭 둘 것")]
    public Entry obstacleHit = new Entry();

    [Tooltip("적을 처치할 때. 초당 여러 번 나므로 minInterval 을 꼭 둘 것")]
    public Entry enemyKilled = new Entry();

    public Entry weaponSwap = new Entry();
    public Entry playerDead = new Entry();

    [Header("진행")]
    public Entry stageClear = new Entry();
    public Entry stageRewardOpen = new Entry();
    public Entry bossSpawn = new Entry();
}
