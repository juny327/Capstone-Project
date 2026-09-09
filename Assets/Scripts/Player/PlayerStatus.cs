using UnityEngine;

public class PlayerStatus : MonoBehaviour, IStatusReceiver
{
    public void ApplyStatus(StatusEffect effect)
    {
        effect.Apply(gameObject);
    }
}