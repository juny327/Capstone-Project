using TMPro;
using UnityEngine;

/// <summary>
/// 정비 신호기 위치 표시 — 화면 안이면 신호기 위에 표시 · 거리, 화면 밖이면 그쪽 화면 가장자리에 화살표 · 거리.
/// 나타날 때 위쪽에 안내 문구를 잠깐 띄운다. 프리팹은 Tools / 재화 · 상점 / 보급 아이템 만들기 가 만든다 (화면 덮개 캔버스).
/// </summary>
public class SupplyIndicator : MonoBehaviour
{
    [SerializeField] RectTransform canvasRect;
    [Tooltip("화살표 · 표시 묶음 (가운데 기준, 위쪽을 가리키는 삼각형)")]
    [SerializeField] RectTransform pointer;
    [SerializeField] RectTransform arrow;
    [SerializeField] TMP_Text label;
    [SerializeField] GameObject banner;
    [SerializeField] TMP_Text bannerText;
    [Tooltip("이만큼 화면 안쪽에 들어와야 신호기 위에 표시한다 (캔버스 단위)")]
    [SerializeField] float edgeMargin = 80f;
    [Tooltip("화면 밖일 때 화살표가 플레이어 둘레를 도는 타원 반지름 (캔버스 단위) — 화면 가장자리의 HUD 를 가리지 않게")]
    [SerializeField] Vector2 ringRadius = new Vector2(300f, 220f);

    Transform target;
    float bannerLeft;

    public void Track(Transform beacon, string announce, float announceSeconds)
    {
        target = beacon;
        gameObject.SetActive(beacon != null);
        if (bannerText != null) bannerText.text = announce;
        bannerLeft = string.IsNullOrEmpty(announce) ? 0f : announceSeconds;
        if (banner != null) banner.SetActive(bannerLeft > 0f);
    }

    public void Clear()
    {
        target = null;
        gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (bannerLeft > 0f)
        {
            bannerLeft -= Time.unscaledDeltaTime;
            if (bannerLeft <= 0f && banner != null) banner.SetActive(false);
        }

        Camera cam = Camera.main;
        SupplySystem system = SupplySystem.Instance;
        if (target == null || cam == null || canvasRect == null || pointer == null)
        {
            if (pointer != null) pointer.gameObject.SetActive(false);
            return;
        }

        // 정비 · 카드 창이 열린 동안은 숨긴다
        bool paused = Time.timeScale < 0.01f;
        pointer.gameObject.SetActive(!paused);
        if (paused) return;

        Vector3 world = target.position + Vector3.up * 3.2f;
        Vector3 screen = cam.WorldToScreenPoint(world);

        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local);
        Vector2 half = canvasRect.rect.size * 0.5f - new Vector2(edgeMargin, edgeMargin);

        // 카메라 뒤(화면 아래쪽 먼 곳)는 화면 좌표가 뒤집혀 나온다 — 그때는 화면 밖으로 본다
        bool inside = screen.z > 0f && Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y;
        Vector2 pos;
        float angle;

        if (inside)
        {
            // 신호기 바로 위 — 아래를 가리킨다
            pos = local;
            angle = 180f;
        }
        else
        {
            // 플레이어 둘레 타원 위 — 방향은 바닥 위 플레이어 → 신호기를 카메라 기준으로 (화면 좌표는 카메라 뒤에서 뒤집히므로 쓰지 않는다)
            Vector2 center = Vector2.zero;
            Vector3 from = system != null && system.HasLivePlayer ? system.PlayerPosition : cam.transform.position;
            Vector3 ps = cam.WorldToScreenPoint(from + Vector3.up * 1f);
            if (ps.z > 0f) RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, ps, null, out center);

            Vector3 right = cam.transform.right;
            Vector3 up = Mathf.Abs(cam.transform.forward.y) > 0.99f ? cam.transform.up : cam.transform.forward;
            right.y = 0f;
            up.y = 0f;
            Vector3 d = target.position - from;
            Vector2 to = new Vector2(Vector3.Dot(d, right.normalized), Vector3.Dot(d, up.normalized));
            Vector2 dir = to.sqrMagnitude > 0.01f ? to.normalized : Vector2.up;
            float k = 1f / Mathf.Sqrt(dir.x * dir.x / (ringRadius.x * ringRadius.x) + dir.y * dir.y / (ringRadius.y * ringRadius.y));
            pos = center + dir * k;
            angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        }

        pointer.anchoredPosition = pos;
        if (arrow != null)
        {
            arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 6f);
            arrow.localScale = Vector3.one * pulse;
        }

        if (label != null && system != null && system.HasLivePlayer)
        {
            Vector3 d = target.position - system.PlayerPosition;
            d.y = 0f;
            label.text = $"정비 신호기  {Mathf.RoundToInt(d.magnitude)}m";
        }
    }
}
