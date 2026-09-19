using UnityEditor;
using UnityEngine;

/// <summary>
/// Player 프리팹에서 옛 발사 체계(Gun · AutoAttack) 컴포넌트를 떼어낸다 (13번 3장).
///
/// 실제 발사는 WeaponController -> ProjectileWeapon 이 한다. Gun 은 발사에 관여하지 않으면서
/// HUD 이벤트(OnBulletDamageChanged)만 쏘고 있어, 무기의 실제 스탯을 덮어썼다.
///
/// 스크립트 파일(Gun.cs · AutoAttack.cs)은 지우지 않는다 —
/// WeaponController 와 WeaponAssetSetup 이 AutoAttack 타입을 참조하고,
/// Assets/_Recovery 의 백업 씬들도 두 스크립트를 물고 있다.
/// </summary>
public static class LegacyGunCleanup
{
    const string Tag = "[LegacyGunCleanup]";
    const string PrefabPath = "Assets/Prefabs/Player.prefab";

    [MenuItem("Tools/Legacy Cleanup/Player 프리팹에서 Gun · AutoAttack 제거")]
    public static void Run()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        if (root == null)
        {
            Debug.LogError($"{Tag} 프리팹을 열 수 없습니다: {PrefabPath}");
            return;
        }

        try
        {
            int removed = 0;

            // AutoAttack 을 먼저 지운다 — Gun 을 참조하고 있다
            removed += RemoveAll<AutoAttack>(root);
            removed += RemoveAll<Gun>(root);

            if (removed == 0)
            {
                Debug.Log($"{Tag} 제거할 컴포넌트가 없습니다 (이미 정리됨)");
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"{Tag} 컴포넌트 {removed}개 제거 완료");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static int RemoveAll<T>(GameObject root) where T : Component
    {
        T[] found = root.GetComponentsInChildren<T>(true);

        foreach (T component in found)
        {
            Debug.Log($"{Tag} 제거: {typeof(T).Name} ({component.gameObject.name})");
            Object.DestroyImmediate(component, true);
        }

        return found.Length;
    }
}
