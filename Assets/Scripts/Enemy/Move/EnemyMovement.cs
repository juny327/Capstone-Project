using UnityEngine;

public abstract class EnemyMovement : MonoBehaviour
{
    protected Enemy enemy;

    public virtual void Initialize(Enemy enemy)
    {
        this.enemy = enemy;
    }

    public abstract void MoveTo(Vector3 position);

    public abstract void Stop();
    public abstract void Resume();

    public virtual void SetSpeedMultiplier(float multiplier) { }
}
