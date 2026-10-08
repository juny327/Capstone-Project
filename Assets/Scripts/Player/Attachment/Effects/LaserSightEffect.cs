using UnityEngine;

/// <summary>
/// 레이저 조준기 — 총구에서 탄이 나가는 방향으로 붉은 조준선 (커스터마이징-구현계획.md 4-3 · 4-7).
/// 적에 닿으면 거기서 멈춘다. 탑다운에서 가장 잘 보이는 부착물 신호다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Attachment Effects/Laser Sight", fileName = "AE_LaserSight")]
public class LaserSightEffect : AttachmentEffect
{
    public Material material;
    public Color color = new Color(1f, 0.18f, 0.15f, 0.8f);
    [Min(0.005f)] public float width = 0.03f;
    [Min(1f)] public float maxLength = 14f;

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly LaserSightEffect cfg;
        readonly RaycastHit[] hits = new RaycastHit[16];
        LineRenderer line;

        public Runtime(LaserSightEffect cfg) => this.cfg = cfg;

        protected override void OnBind()
        {
            // 무기 아래에 둔다 — 무기를 바꿔 들면 WeaponBase.SetActive 가 자식째 숨긴다
            var go = new GameObject("LaserSight");
            go.transform.SetParent(Weapon.transform, false);

            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 2;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = cfg.material;
            line.startWidth = cfg.width;
            line.endWidth = cfg.width * 0.6f;
            Color end = cfg.color;
            end.a *= 0.25f;
            line.startColor = cfg.color;
            line.endColor = end;
            line.enabled = false;
        }

        public override void Tick(float deltaTime)
        {
            if (line == null) return;

            bool show = Weapon.IsActive && Weapon.Controller != null && cfg.material != null;
            line.enabled = show;
            if (!show) return;

            Vector3 from = Weapon.MuzzlePoint.position;
            Vector3 dir = Weapon.Controller.transform.forward;   // 탄이 나가는 방향 = 플레이어가 보는 방향
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();

            float length = cfg.maxLength;
            int count = Physics.SphereCastNonAlloc(from, 0.08f, dir, hits, cfg.maxLength, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                if (CombatFx.ResolveRoot(hits[i].collider) == null) continue;
                if (hits[i].distance > 0f && hits[i].distance < length) length = hits[i].distance;
            }

            line.SetPosition(0, from);
            line.SetPosition(1, from + dir * length);
        }

        public override void Dispose()
        {
            if (line != null) Object.Destroy(line.gameObject);
            line = null;
        }
    }
}
