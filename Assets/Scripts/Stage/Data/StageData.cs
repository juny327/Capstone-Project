using UnityEngine;

[CreateAssetMenu(menuName = "Stage/StageData")]
public class StageData : ScriptableObject
{
    public int stageIndex;

    public int killTarget;

    public int maxEnemyCount;
}