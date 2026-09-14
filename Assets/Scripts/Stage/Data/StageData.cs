using UnityEngine;

[CreateAssetMenu(menuName = "Stage/StageData")]
public class StageData : ScriptableObject
{
    public int stageIndex;

    public int killTarget;

    public int maxEnemyCount;

    [Min(0.1f)]
    public float spawnInterval = 3f;
}
