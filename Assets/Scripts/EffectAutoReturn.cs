using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class EffectAutoReturn : MonoBehaviour, IPoolable
{
    private ParticleSystem ps;
    private Poolable poolable;

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        poolable = GetComponent<Poolable>();
    }

    public void OnSpawn()
    {
        ps.Play();
    }

    public void OnDespawn()
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void OnParticleSystemStopped()
    {
        poolable.ReturnToPool();
    }
}