using UnityEngine;

[CreateAssetMenu(fileName = "TornadoEffect", menuName = "Upgrade/Effects/Tornado")]
public class TornadoEffect : UpgradeEffect
{
    public GameObject projectilePrefab;

    public float fireInterval = 15f;
    public float projectileSpeed = 4f;
    public float targetRange = 25f;

    public float duration = 5f;
    public float damage = 2f;
    public float attackInterval = 0.5f;
    public float radius = 3f;

    public Vector3 spawnOffset = new Vector3(0f, 1f, 0f);

    public override void Apply(GameObject player)
    {
        Apply(player, 1);
    }

    public override void Apply(GameObject player, int level)
    {
        if (player == null)
            return;

        TornadoSkill skill = player.GetComponent<TornadoSkill>();

        if (skill == null)
            skill = player.AddComponent<TornadoSkill>();

        // 레벨마다 +25% (화염구 · 연쇄 번개와 같은 규칙, Lv5 = ×2) — 2026-11-08.
        // 예전 +1 정수는 기본 피해를 낮추면 Lv5 가 5~6배가 되어 메인 무기 DPS 를 넘었다
        float scaledDamage = ScaleValue(damage, level);

        skill.Setup(
            projectilePrefab,
            fireInterval,
            projectileSpeed,
            targetRange,
            duration,
            scaledDamage,
            attackInterval,
            radius,
            spawnOffset);
    }
}