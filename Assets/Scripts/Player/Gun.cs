using UnityEngine;

public class Gun : MonoBehaviour
{
    public Transform muzzle;
    public Transform bulletCase;
    public GameObject bulletPrefab;
    public GameObject muzzleFlashPrefab;
    public GameObject bulletCasePrefab;

    public float bulletSpeed = 20f;
    public float bulletDamage = 10f;

    void Start()
    {
        GameEvents.OnBulletDamageChanged?.Invoke(bulletDamage);
    }

    public void Shoot()
    {
        SpawnMuzzleFlash();
        SpawnBulletCase();
        GameObject bulletObj = PoolManager.Instance.Get(bulletPrefab);

        bulletObj.transform.position = muzzle.position;
        bulletObj.transform.rotation = muzzle.rotation;

        Bullet bullet = bulletObj.GetComponent<Bullet>();

        bullet.Init(bulletSpeed, bulletDamage);
    }

    void SpawnMuzzleFlash()
    {
        GameObject effect = PoolManager.Instance.Get(muzzleFlashPrefab);

        effect.transform.position = muzzle.position;
        effect.transform.rotation = muzzle.rotation;
    }

    void SpawnBulletCase()
    {
        GameObject caseObj = PoolManager.Instance.Get(bulletCasePrefab);

        caseObj.transform.position = bulletCase.position;
        caseObj.transform.rotation = bulletCase.rotation;
    }

    public void AddDamage(float amount)
    {
        bulletDamage += amount;
        GameEvents.OnBulletDamageChanged?.Invoke(bulletDamage);
    }

    public void AddSpeed(float aomunt)
    {
        bulletSpeed += aomunt;
        GameEvents.OnBulletSpeedChanged?.Invoke(bulletSpeed);
    }
}