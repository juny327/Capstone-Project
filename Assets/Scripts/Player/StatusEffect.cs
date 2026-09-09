using UnityEngine;

public abstract class StatusEffect
{
    public float duration;

    public abstract void Apply(GameObject target);
}