using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play 중에 주무기(손에 드는 총 · 근접 무기)를 바로 받는 창. 메뉴: Tools / 주무기 지급
///
/// 프로젝트의 WeaponData 를 전부 찾아 보여 주므로 무기를 새로 만들어도 이 파일은 고치지 않는다
/// (드론처럼 손에 들지 않는 무기와 프리팹이 없는 데이터는 뺀다).
///
/// 받기는 테스트 룸 픽업과 같은 WeaponController.SelectHeldWeapon 을 쓴다
///   · 없으면 빈 칸에 장착하고 손에 든다 · 이미 가졌으면 꺼내 든다
///   · 칸이 가득 차면 손에 든 무기와 바꾼다 — 바뀐 무기의 레벨 · 부착물은 사라진다
/// 캐릭터 제한(사수 = 총, 검사 = 근접)은 테스트 룸처럼 무시하고, 목록에 "다른 캐릭터용" 으로만 표시한다.
/// </summary>
public class WeaponGrantWindow : EditorWindow
{
    const string Tag = "[주무기 지급]";

    readonly List<WeaponData> guns = new List<WeaponData>();
    readonly List<WeaponData> melee = new List<WeaponData>();
    Vector2 scroll;
    bool drawnWhilePlaying;

    [MenuItem("Tools/주무기 지급")]
    static void Open() => GetWindow<WeaponGrantWindow>("주무기 지급");

    void OnEnable() => Reload();

    // 무기 데이터를 새로 만들거나 지우면 목록을 다시 읽는다
    void OnProjectChange()
    {
        Reload();
        Repaint();
    }

    // 보유 · 손에 든 무기는 게임 안에서 계속 바뀌므로 Play 중에는 계속 다시 그린다 (멈춘 직후 한 번 더)
    void OnInspectorUpdate()
    {
        if (EditorApplication.isPlaying || drawnWhilePlaying) Repaint();
    }

    void Reload()
    {
        guns.Clear();
        melee.Clear();

        foreach (string guid in AssetDatabase.FindAssets("t:WeaponData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
            if (!WeaponController.IsValidHeldWeapon(data)) continue;

            (data.Kind == WeaponKind.Melee ? melee : guns).Add(data);
        }

        guns.Sort(ByName);
        melee.Sort(ByName);
    }

    static int ByName(WeaponData a, WeaponData b) => string.CompareOrdinal(a.weaponName, b.weaponName);

    void OnGUI()
    {
        drawnWhilePlaying = EditorApplication.isPlaying;

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Play 중에 스테이지나 테스트 룸에 들어가면 쓸 수 있습니다.", MessageType.Info);
            return;
        }

        WeaponController controller = FindFirstObjectByType<WeaponController>();
        if (controller == null || !controller.IsInitialized)
        {
            EditorGUILayout.HelpBox("플레이어가 아직 없습니다. 스테이지나 테스트 룸에 들어간 뒤 쓰세요.", MessageType.Info);
            return;
        }

        CharacterLoadout loadout = controller.GetComponent<CharacterLoadout>();
        string character = loadout != null && loadout.Data != null ? loadout.Data.displayName : "캐릭터";

        EditorGUILayout.LabelField(
            $"{character} · 주무기 {controller.HeldWeapons.Count} / {controller.MaxHeldSlots}칸", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            controller.HasFreeHeldSlot
                ? "빈 칸에 장착하고 바로 손에 듭니다."
                : "칸이 가득 찼습니다 — 손에 든 무기와 바꿉니다 (그 무기의 레벨 · 부착물은 사라짐).",
            EditorStyles.wordWrappedMiniLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawGroup("총", guns, controller);
        DrawGroup("근접", melee, controller);
        EditorGUILayout.EndScrollView();
    }

    // 클릭으로 상태가 바뀌어도 그리는 컨트롤 수는 같게 둔다 (IMGUI 레이아웃 오류 방지)
    static void DrawGroup(string title, List<WeaponData> list, WeaponController controller)
    {
        if (list.Count == 0) return;

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        Vector2 iconSize = EditorGUIUtility.GetIconSize();
        EditorGUIUtility.SetIconSize(new Vector2(20f, 20f));

        foreach (WeaponData data in list)
        {
            if (data == null) continue;   // Play 중에 에셋을 지운 경우

            IWeapon owned = controller.Find(data);
            bool inHand = owned != null && owned == controller.ActiveWeapon;

            var notes = new List<string>();
            if (owned != null) notes.Add(inHand ? $"Lv{owned.Level} · 손에 듦" : $"Lv{owned.Level} · 보유");
            if (!controller.IsAllowedForCharacter(data)) notes.Add("다른 캐릭터용");

            using (new EditorGUILayout.HorizontalScope())
            {
                Texture icon = data.icon != null ? data.icon.texture : null;
                GUILayout.Label(new GUIContent(" " + data.weaponName, icon), GUILayout.Width(130f), GUILayout.Height(22f));
                GUILayout.Label(string.Join(" · ", notes), EditorStyles.miniLabel, GUILayout.Height(22f));

                using (new EditorGUI.DisabledScope(inHand))
                {
                    if (GUILayout.Button(owned == null ? "받기" : "들기", GUILayout.Width(56f), GUILayout.Height(20f)))
                        Grant(controller, data);
                }
            }
        }

        EditorGUIUtility.SetIconSize(iconSize);
    }

    static void Grant(WeaponController controller, WeaponData data)
    {
        IWeapon active = controller.ActiveWeapon;
        string previous = active != null && active.Data != null ? active.Data.weaponName : "";

        string message = controller.SelectHeldWeapon(data) switch
        {
            HeldWeaponSelectionResult.Equipped => "장착했습니다",
            HeldWeaponSelectionResult.Swapped => "꺼내 들었습니다",
            HeldWeaponSelectionResult.Replaced => $"손에 든 {previous} 대신 장착했습니다",
            HeldWeaponSelectionResult.Unchanged => "이미 손에 들고 있습니다",
            HeldWeaponSelectionResult.NotReady => "플레이어가 아직 준비되지 않았거나 쓰러졌습니다",
            _ => "무기 데이터나 프리팹이 잘못됐습니다",
        };

        Debug.Log($"{Tag} {data.weaponName}: {message}");
    }
}
