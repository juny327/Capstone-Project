using UnityEngine;

public class StunZone : MonoBehaviour
{
    [Header("Stun Settings")]
    public float duration = 2f;
    public GameObject stunEffectPrefab;

    [Header("Zone Settings")]
    public float lifeTime = 3f; // 영역 유지 시간

    bool alreadyApplied = false;

    void Start()
    {
        // 🔥 일정 시간 후 영역 제거
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (alreadyApplied) return;

        IStatusReceiver receiver = other.GetComponent<IStatusReceiver>();

        if (receiver != null)
        {
            alreadyApplied = true;
            
            var stun = new StunEffect(duration, stunEffectPrefab);
            receiver.ApplyStatus(stun);
        }
    }
}