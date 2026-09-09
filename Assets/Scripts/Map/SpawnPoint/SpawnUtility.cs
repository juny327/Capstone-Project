using System.Collections.Generic;
using UnityEngine;


// 스폰 유틸클래스
public static class SpawnUtility
{
    public static bool IsFarEnough(Vector3 pos, List<Vector3> placed, float minDistance)
    {
        foreach (var p in placed)
        {
            if (Vector3.Distance(pos, p) < minDistance)
                return false;
        }

        return true;
    }
}