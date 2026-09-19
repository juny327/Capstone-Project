using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Upgrade/Pool")]
public class UpgradePoolData : ScriptableObject
{
    public List<UpgradeData> upgrades;
}