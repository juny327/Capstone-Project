using UnityEngine;

/// <summary>
/// 상위 무기 부속 — 자식들을 축 둘레로 돌린다 (프리즘 노바의 떠 있는 결정, 상위무기-계획.md 14-3).
/// 자식마다 자기 자리에서 조금씩 위아래로 떠 있고 스스로도 돈다.
/// </summary>
public class EvolvedOrbit : MonoBehaviour
{
    [Tooltip("무기 기준 회전축 (총은 총열 방향)")]
    [SerializeField] Vector3 axis = Vector3.forward;
    [SerializeField] float degreesPerSecond = 120f;
    [SerializeField] float spinPerSecond = 200f;
    [Min(0f)] [SerializeField] float bob = 0.02f;

    Vector3[] basePositions;

    void Awake()
    {
        basePositions = new Vector3[transform.childCount];
        for (int i = 0; i < basePositions.Length; i++)
            basePositions[i] = transform.GetChild(i).localPosition;
    }

    void Update()
    {
        transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);

        for (int i = 0; i < basePositions.Length && i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            child.localPosition = basePositions[i] + axis.normalized * (Mathf.Sin(Time.time * 3f + i * 2.1f) * bob);
            child.Rotate(Vector3.up, spinPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
