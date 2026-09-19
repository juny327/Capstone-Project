using UnityEngine;

public abstract class UpgradeEffect : ScriptableObject
{
    public abstract void Apply(GameObject player);

    // ───────── 카드 표시 문구 ─────────
    //
    // 기본은 UpgradeData 에 적힌 값을 그대로 쓴다.
    // 서브유닛 카드처럼 문구가 다른 에셋(WeaponData)에 있는 경우만 재정의한다.
    // 이렇게 해 두면 같은 설명을 두 군데 적고 한쪽만 고쳐 어긋나는 일이 없다.

    public virtual string GetTitle(UpgradeData data) => data.upgradeName;

    public virtual string GetDescription(UpgradeData data) => data.description;

    public virtual Sprite GetIcon(UpgradeData data) => data.icon;
}