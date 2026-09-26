using UnityEngine;

/// <summary>
/// 패링 성공 연출 — 섬광 · 퍼지는 고리 · 불꽃.
///
/// 파티클 구성은 CharacterSetup 이 만든 프리팹(Prefabs/Effect/Parry/ParryFlash)에 있고, 여기서는 재생만 한다.
/// 패링은 5초에 한 번이라 풀링하지 않고 ParryController 가 하나를 만들어 두고 다시 쓴다.
/// 파티클은 월드 공간에서 움직이므로 플레이어가 달려도 제자리에 남는다.
/// </summary>
public class ParryFlash : MonoBehaviour
{
    [SerializeField] private ParticleSystem flash;
    [SerializeField] private ParticleSystem ring;
    [SerializeField] private ParticleSystem sparks;

    [Tooltip("Play 한 번에 튀는 불꽃 수")]
    [Min(0)] [SerializeField] private int sparkBurst = 26;

    [Tooltip("쳐낸 투사체마다 튀는 불꽃 수")]
    [Min(0)] [SerializeField] private int sparksPerDeflect = 10;

    /// <summary>막은 자리에서 섬광 · 고리 · 불꽃을 터뜨린다. facing 쪽으로 불꽃이 튄다.</summary>
    public void Play(Vector3 position, Vector3 facing)
    {
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(Flat(facing)));

        Restart(flash);
        Restart(ring);
        EmitSparks(position, facing, sparkBurst);
    }

    /// <summary>쳐낸 투사체 자리에서 불꽃만 튀긴다.</summary>
    public void Deflect(Vector3 position, Vector3 direction)
    {
        EmitSparks(position, direction, sparksPerDeflect);
    }

    void EmitSparks(Vector3 position, Vector3 direction, int count)
    {
        if (sparks == null || count <= 0) return;

        // 불꽃 모양(원뿔)은 자기 회전을 따른다 — 방향을 맞춘 뒤 그 자리에서 뿜는다
        sparks.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Flat(direction)));
        sparks.Emit(count);
    }

    static void Restart(ParticleSystem ps)
    {
        if (ps == null) return;

        ps.Clear(true);
        ps.Play(true);
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }
}
