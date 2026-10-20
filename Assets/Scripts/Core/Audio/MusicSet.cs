using UnityEngine;

/// <summary>
/// 씬별 배경음 목록.
///
/// 씬 이름으로 찾으므로 **씬을 추가해도 코드를 고치지 않는다** — 항목만 늘리면 된다.
/// 목록에 없는 씬은 음악을 끈다(예: 연출 씬).
/// </summary>
[CreateAssetMenu(menuName = "Audio/Music Set", order = 2)]
public class MusicSet : ScriptableObject
{
    [System.Serializable]
    public class SceneTrack
    {
        [Tooltip("씬 이름 그대로 (Stage1 · Loby …)")]
        public string sceneName;

        public AudioClip clip;

        [Range(0f, 1f)] public float volume = 0.5f;
    }

    public SceneTrack[] tracks;

    /// <summary>해당 씬의 곡. 없으면 null.</summary>
    public SceneTrack Find(string sceneName)
    {
        if (tracks == null || string.IsNullOrEmpty(sceneName)) return null;

        for (int i = 0; i < tracks.Length; i++)
        {
            if (tracks[i] == null) continue;
            if (tracks[i].sceneName != sceneName) continue;

            return tracks[i];
        }

        return null;
    }
}
