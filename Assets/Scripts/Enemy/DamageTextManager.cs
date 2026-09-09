using UnityEngine;

public class DamageTextManager : MonoBehaviour
{
    public static DamageTextManager Instance { get; private set; }

    public GameObject damageTextPrefab;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void ShowDamage(int damage, Vector3 pos, bool isCritical)
    {
        GameObject obj = PoolManager.Instance.Get(damageTextPrefab);
        obj.GetComponent<DamageText>().Show(damage, pos, isCritical);
    }
}