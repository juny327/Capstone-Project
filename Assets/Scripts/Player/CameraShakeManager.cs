using UnityEngine;
using Unity.Cinemachine;

public class CameraShakeManager : MonoBehaviour
{
    public static CameraShakeManager Instance;

    public CinemachineImpulseSource impulseSource;

    void Awake()
    {
        Instance = this;
    }

    public void Shake(float force = 1f)
    {
        impulseSource.GenerateImpulse(force);
    }
}