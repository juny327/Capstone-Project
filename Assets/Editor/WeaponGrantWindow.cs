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
///
/// 아래 "상위 무기" 칸 (상위무기-계획.md 16장) — 진화 조합(EvolutionCatalog)마다
///   · [조건] 원래 무기를 들고 최대 레벨 · 짝 장비를 채운다 — 보상 카드 · 정비 창의 진화 흐름을 볼 때
///   · [진화] 이어서 바로 진화한다 — 짝 장비를 가질 수 없는 캐릭터(사수의 서브 능력)는 짝 없이 시험 진화
/// 상위 무기 데이터(WD_EVO_*)는 위 총 · 근접 목록에서 뺀다 — 레벨 1 · 부착물 없이 받으면 진화한 무기와 다르다.
/// </summary>
public class WeaponGrantWindow : EditorWindow
{
    const string Tag = "[주무기 지급]";

    readonly List<WeaponData> guns = new List<WeaponData>();
    readonly List<WeaponData> melee = new List<WeaponData>();
    readonly List<WeaponEvolutionData> evolutions = new List<WeaponEvolutionData>();
    Vector2 scroll;
    bool drawnWhilePlaying;

    [MenuItem("Tools/주무기 지급")]
    static void Open() => GetWindow<WeaponGrantWindow>("주무기 지급");

    // 같은 창 — 상위 무기 칸이 맨 아래에 있다
    [MenuItem("Tools/상위 무기 시험")]
    static void OpenEvolution()
    {
        var window = GetWindow<WeaponGrantWindow>("주무기 지급");
        window.scroll = new Vector2(0f, float.MaxValue);
    }

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
        evolutions.Clear();

        foreach (string guid in AssetDatabase.FindAssets("t:WeaponData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
            if (!WeaponController.IsValidHeldWeapon(data) || data.IsEvolved) continue;

            (data.Kind == WeaponKind.Melee ? melee : guns).Add(data);
        }

        guns.Sort(ByName);
        melee.Sort(ByName);

        // 목록 순서 그대로 (EvolutionCatalog). 목록에 없는 조합 에셋은 뒤에 붙인다
        var catalog = AssetDatabase.LoadAssetAtPath<EvolutionCatalog>("Assets/Resources/" + EvolutionCatalog.ResourcePath + ".asset");
        if (catalog != null && catalog.evolutions != null)
            foreach (WeaponEvolutionData e in catalog.evolutions)
                if (e != null && !evolutions.Contains(e)) evolutions.Add(e);

        foreach (string guid in AssetDatabase.FindAssets("t:WeaponEvolutionData"))
        {
            var e = AssetDatabase.LoadAssetAtPath<WeaponEvolutionData>(AssetDatabase.GUIDToAssetPath(guid));
            if (e != null && !evolutions.Contains(e)) evolutions.Add(e);
        }
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
        DrawEvolutions(controller);
        DrawSynergies(controller);
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

    void DrawEvolutions(WeaponController controller)
    {
        if (evolutions.Count == 0) return;

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("상위 무기", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("[조건] 원래 무기 · 최대 레벨 · 짝 장비만 채운다 (보상 카드 · 정비 창 확인)   [진화] 바로 진화",
            EditorStyles.wordWrappedMiniLabel);

        var abilities = controller.GetComponent<SubAbilitySlot>();
        Vector2 iconSize = EditorGUIUtility.GetIconSize();
        EditorGUIUtility.SetIconSize(new Vector2(20f, 20f));

        foreach (WeaponEvolutionData e in evolutions)
        {
            if (e == null || e.from == null || e.result == null) continue;

            IWeapon evolved = controller.Find(e.result);
            bool done = evolved != null && evolved.Data == e.result;
            IWeapon owned = e.FindOwned(controller);

            string state = done ? "진화함"
                : e.IsReady(controller, abilities) ? "진화 가능"
                : owned != null ? $"{e.from.weaponName} Lv{owned.Level}/{e.RequiredLevel}{(e.HasPair(owned, abilities, controller) ? "" : " · 짝 없음")}"
                : "원래 무기 없음";

            using (new EditorGUILayout.HorizontalScope())
            {
                Texture icon = e.result.icon != null ? e.result.icon.texture : null;
                GUILayout.Label(new GUIContent(" " + e.Title, icon, e.ConditionText), GUILayout.Width(130f), GUILayout.Height(22f));
                GUILayout.Label($"{e.from.weaponName} + {PairName(e)} · {state}", EditorStyles.miniLabel, GUILayout.Height(22f));

                using (new EditorGUI.DisabledScope(done))
                {
                    if (GUILayout.Button("조건", GUILayout.Width(44f), GUILayout.Height(20f))) Prepare(controller, e, false);
                }

                if (GUILayout.Button(done ? "들기" : "진화", GUILayout.Width(44f), GUILayout.Height(20f))) Prepare(controller, e, true);
            }
        }

        EditorGUIUtility.SetIconSize(iconSize);
    }

    // 작은 조합 (상위무기-계획.md 7장 · 2026-10-11) — 짝 장비를 갖춰 바로 켜 본다. 테스트 룸에는 부착물을 달 곳이 없다
    void DrawSynergies(WeaponController controller)
    {
        SynergyCatalog catalog = SynergyCatalog.Instance;
        if (catalog == null || catalog.synergies == null || catalog.synergies.Length == 0) return;

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("작은 조합", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("[갖추기] 짝 장비를 단다 — 사수: 손에 든 총의 하부 + 탄 · 검사: 서브 능력 둘 (변형 포함). 켜지면 화면 위에 알림",
            EditorStyles.wordWrappedMiniLabel);

        var active = new List<SynergyData>();
        SynergyCatalog.CollectActive(controller.gameObject, active);

        foreach (SynergyData s in catalog.synergies)
        {
            if (s == null) continue;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent(" " + s.Title, s.description), GUILayout.Width(130f), GUILayout.Height(22f));
                GUILayout.Label($"{s.ConditionText} · {(active.Contains(s) ? "켜짐" : "꺼짐")}", EditorStyles.miniLabel, GUILayout.Height(22f));

                if (GUILayout.Button("갖추기", GUILayout.Width(56f), GUILayout.Height(20f)))
                {
                    SynergyCatalog.PrepareForTest(controller.gameObject, s, out string message);
                    Debug.Log($"{Tag} {s.Title}: {message}");
                }
            }
        }
    }

    static string PairName(WeaponEvolutionData e)
    {
        if (e.requiredAttachment != null) return e.requiredAttachment.displayName;
        if (e.requiredSubAbility != null) return e.requiredSubAbility.displayName;
        if (e.requiredAnyGunAttachment != null) return "총에 " + e.requiredAnyGunAttachment.displayName;
        return "짝 없음";
    }

    static void Prepare(WeaponController controller, WeaponEvolutionData e, bool evolve)
    {
        EvolutionService.PrepareForTest(controller.gameObject, e, evolve, out string message);
        Debug.Log($"{Tag} {e.Title}: {message}");
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
