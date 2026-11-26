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

    public void ShowDamage(int damage, Vector3 pos, bool isCritical) => ShowDamage((float)damage, pos, isCritical);

    /// <summary>
    /// 피해 숫자 (2026-11-17) — 10 미만은 소수 한 자리(2.9), 10 이상은 반올림한 정수(14).
    /// 예전엔 소수점을 버려(int) 소총 2.2 → 2.86 강화가 계속 "2"로 보였다. 실제 체력은 원래부터 소수까지 깎인다.
    /// </summary>
    public void ShowDamage(float damage, Vector3 pos, bool isCritical)
    {
        if (!GameSettings.DamageNumbers) return;   // 설정 '피해 숫자' 끔 (2026-11-26 접근성 — 후반 화면을 덮는 숫자 · 프레임 부담을 줄인다)
        GameObject obj = PoolManager.Instance.Get(damageTextPrefab);
        obj.GetComponent<DamageText>().Show(damage, pos, isCritical);
    }

    /// <summary>화면에 보일 피해 숫자 — 10 미만은 소수 한 자리(".0" 은 뺀다), 10 이상은 반올림.</summary>
    public static string Format(float damage)
    {
        if (float.IsNaN(damage) || float.IsInfinity(damage)) return "0";
        float tenth = Mathf.Round(damage * 10f) / 10f;
        if (tenth >= 10f) return Mathf.RoundToInt(damage).ToString();
        return Mathf.Approximately(tenth, Mathf.Round(tenth))
            ? Mathf.RoundToInt(tenth).ToString()
            : tenth.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
    }
}