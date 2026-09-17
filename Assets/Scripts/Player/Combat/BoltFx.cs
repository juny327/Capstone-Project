using UnityEngine;

/// <summary>
/// 두 지점 사이에 번개를 그리는 연출 래퍼 (11번 문서 6-3).
///
/// Lightning Bolt Effect(Digital Ruby) 프리팹 위에 올려 쓴다.
/// 그 에셋의 스크립트는 StartObject / EndObject 로 지정된 두 Transform 사이에 번개를 그리므로,
/// **우리 코드는 에셋 타입을 전혀 참조하지 않고 자식 두 개의 위치만 옮긴다.**
/// 덕분에 에셋을 받지 않은 팀원도 컴파일이 되고 번개 연출만 빠진다 (11번 1-2).
///
/// 프리팹 구성
///   FX_Tesla_Bolt (Poolable, BoltFx, LineRenderer, 에셋의 번개 스크립트)
///   ├─ Start
///   └─ End
/// </summary>
[RequireComponent(typeof(Poolable))]
public class BoltFx : MonoBehaviour, IPoolable
{
    [Header("Ends")]
    [Tooltip("비우면 자식에서 'Start' / 'End' 이름으로 찾는다")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;

    [Header("Life")]
    [Tooltip("번개가 보이는 시간(초). 이 시간이 지나면 풀로 돌아간다")]
    [Min(0.02f)] private float lifetime = 0.15f;

    private Poolable poolable;
    private LineRenderer line;

    private float endTime;
    private bool running;

    void Awake()
    {
        poolable = GetComponent<Poolable>();
        line = GetComponentInChildren<LineRenderer>(true);

        // 이름은 에셋마다 다르다 (Lightning Bolt Effect 는 LightningStart / LightningEnd)
        if (startPoint == null) startPoint = FindChild("Start", "LightningStart");
        if (endPoint == null) endPoint = FindChild("End", "LightningEnd");

        if (startPoint == null || endPoint == null)
            Debug.LogError("[BoltFx] Start / End 자식을 찾지 못했습니다.", this);
    }

    /// <summary>이름 후보 중 먼저 찾히는 자식을 돌려준다.</summary>
    Transform FindChild(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform found = transform.Find(names[i]);
            if (found != null) return found;
        }

        return null;
    }

    /// <summary>번개의 시작점과 끝점을 정한다. life 가 0 이하면 기본 수명을 쓴다.</summary>
    public void SetEnds(Vector3 from, Vector3 to, float life = -1f)
    {
        if (startPoint != null) startPoint.position = from;
        if (endPoint != null) endPoint.position = to;

        endTime = Time.time + (life > 0f ? life : lifetime);
        running = true;
    }

    void Update()
    {
        if (!running) return;
        if (Time.time < endTime) return;

        running = false;

        if (poolable != null) poolable.ReturnToPool();
    }

    public void OnSpawn()
    {
        running = false;
        ClearLine();
    }

    public void OnDespawn()
    {
        running = false;
        ClearLine();
    }

    /// <summary>
    /// 다음에 꺼낼 때 이전 번개가 한 프레임 번쩍이지 않도록 선을 지운다.
    /// 번개 에셋은 매 프레임 선을 다시 그리므로 지워도 문제가 없다.
    /// </summary>
    void ClearLine()
    {
        if (line != null) line.positionCount = 0;
    }
}
