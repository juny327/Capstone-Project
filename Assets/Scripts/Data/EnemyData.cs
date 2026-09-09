using UnityEngine;

[CreateAssetMenu(menuName = "Enemy/EnemyData")]
public class EnemyData : ScriptableObject
{
    public float maxHp = 10;
    public float attackPower = 2;

    public float chaseRange = 10f;
    public float attackRange = 2f;

    public float moveSpeed = 3f;
    public int expDrop = 5;

    [Header("Spawn")]
    public int spawnWeight = 10;
}