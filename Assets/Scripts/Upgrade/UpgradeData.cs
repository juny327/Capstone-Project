using UnityEngine;

[CreateAssetMenu(menuName = "Upgrade/Data")]
public class UpgradeData : ScriptableObject
{
    public string upgradeName;

    public string description;

    public Sprite icon;

    public int costExp;   // 필요 경험치

    public UpgradeEffect effect;
}