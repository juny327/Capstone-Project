using UnityEngine;

public class Poolable : MonoBehaviour
{
    private ObjectPool pool;

    public void SetPool(ObjectPool pool)
    {
        this.pool = pool;
    }

    public void ReturnToPool()
    {
        if (pool != null)
            pool.Return(gameObject);
        else
            Destroy(gameObject);
    }
}