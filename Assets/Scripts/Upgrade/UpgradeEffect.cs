using UnityEngine;

public abstract class UpgradeEffect : ScriptableObject
{
    // 기존 호출과의 호환을 유지한다.
    public abstract void Apply(GameObject player);

    // 업그레이드 레벨을 전달하는 신규 진입점.
    // 기존 Effect는 별도 수정 없이 Lv.1 동작을 그대로 사용한다.
    public virtual void Apply(GameObject player, int level)
    {
        Apply(player);
    }

    // Lv.1 = 기본값, Lv.2 = +25%, Lv.3 = +50% ...
    protected float ScaleValue(float baseValue, int level)
    {
        int safeLevel = Mathf.Max(1, level);
        return baseValue * (1f + (safeLevel - 1) * 0.25f);
    }

    protected int ScaleInt(int baseValue, int level)
    {
        return Mathf.Max(0, Mathf.RoundToInt(ScaleValue(baseValue, level)));
    }
}
