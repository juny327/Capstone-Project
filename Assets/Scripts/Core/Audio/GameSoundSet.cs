using UnityEngine;

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

    [Tooltip("적을 처치할 때. 초당 여러 번 나므로 minInterval 을 꼭 둘 것")]
    public Entry enemyKilled = new Entry();

    public Entry weaponSwap = new Entry();
    public Entry playerDead = new Entry();

    [Header("진행")]
    public Entry stageClear = new Entry();
    public Entry stageRewardOpen = new Entry();
    public Entry bossSpawn = new Entry();
}
