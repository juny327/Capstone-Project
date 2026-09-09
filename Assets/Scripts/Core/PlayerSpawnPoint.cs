using UnityEngine;

public class PlayerSpawnPoint : MonoBehaviour
{
    void Start()
    {
        RunManager.Instance.SpawnPlayer(transform.position);
    }
}