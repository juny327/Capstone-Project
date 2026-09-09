using System;
using UnityEngine;

public class EnemyDetector : MonoBehaviour
{
    public int Count { get; private set; }

    public event Action<bool> OnEnemyEnter;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Enemy enemy = other.GetComponent<Enemy>();
            Count++;
            enemy.OnDeath += HandleEnemyDeath;
        }
        if (other.CompareTag("Boss"))
        {
            Count++;
        }
        if(Count == 1)
        {
            OnEnemyEnter?.Invoke(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Enemy enemy = other.GetComponent<Enemy>();
            Count--;
            if (Count < 0) Count = 0;
            enemy.OnDeath -= HandleEnemyDeath;
        }
        if(Count == 0)
        {
            OnEnemyEnter?.Invoke(false);
        }
    }

    void HandleEnemyDeath(Enemy enemy)
    {
        Count--;
        if (Count < 0) Count = 0;

        enemy.OnDeath -= HandleEnemyDeath;
    }


    public bool HasEnemy()
    {
        return Count > 0;
    }
}