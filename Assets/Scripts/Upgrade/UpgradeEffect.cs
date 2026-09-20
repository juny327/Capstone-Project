using UnityEngine;

public abstract class UpgradeEffect : ScriptableObject
{
    public abstract void Apply(GameObject player);
}