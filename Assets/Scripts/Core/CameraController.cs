using UnityEngine;
using Unity.Cinemachine;
public class CameraController : MonoBehaviour
{
    CinemachineCamera vcam;

    void Awake()
    {
        vcam = GetComponent<CinemachineCamera>();
    }

    void OnEnable()
    {
        GameEvents.OnPlayerSpawned += SetTarget;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerSpawned -= SetTarget;
    }

    void SetTarget(Transform player)
    {
        vcam.Follow = player;
        vcam.LookAt = player;
    }
}