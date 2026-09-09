using UnityEngine;

[CreateAssetMenu(menuName = "Pool/PoolConfig")]
public class PoolConfig : ScriptableObject
{
    public GameObject prefab;
    public int initialSize = 10;
    public bool expandable = true;
}