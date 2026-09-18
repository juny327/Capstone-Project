using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 탄창·장전 셋업 (12번 문서). 코드가 읽는 값만 만들어 준다.
///
/// 1. 무기 데이터(WD_Rifle / WD_SMG / WD_Sniper)에 탄창 크기·장전 시간을 기입한다.
///    새로 추가한 필드라 기존 .asset 에는 키 자체가 없어, 여기서 한 번 써 줘야 한다.
/// 2. PlayerAnimator 의 ShootLayer 에 Reload 상태와 Reload / ReloadSpeed 파라미터를 만든다.
///    ProjectileWeapon 이 이 두 파라미터 이름으로 모션을 재생한다.
///
/// 두 메뉴 모두 여러 번 실행해도 결과가 같다(멱등).
/// </summary>
public static class MagazineSetup
{
    const string RiflePath = "Assets/Scripts/Data/Weapons/WD_Rifle.asset";
    const string SmgPath = "Assets/Scripts/Data/Weapons/WD_SMG.asset";
    const string SniperPath = "Assets/Scripts/Data/Weapons/WD_Sniper.asset";

    const string ControllerPath = "Assets/Animations/Player/PlayerAnimator.controller";
    const string ReloadClipPath = "Assets/Animations/Player/Reload.anim";

    const string ShootLayerName = "ShootLayer";
    const string ReloadStateName = "Reload";
    const string ReloadTrigger = "Reload";
    const string ReloadSpeedParam = "ReloadSpeed";

    [MenuItem("Tools/Magazine Setup/전체 실행 (1~2)")]
    public static void RunAll()
    {
        ApplyWeaponData();
        BuildReloadState();

        AssetDatabase.SaveAssets();
        Debug.Log("[MagazineSetup] 전체 실행 완료");
    }

    // ───────── 1. 무기 데이터 탄창 값 ─────────

    [MenuItem("Tools/Magazine Setup/1. 무기 데이터 탄창 값")]
    public static void ApplyWeaponData()
    {
        // 12번 문서 값표. 연사가 빠른 무기일수록 탄창을 크게, 장전은 짧게 잡는다.
        SetAmmo(RiflePath, 30, 2.0f);
        SetAmmo(SmgPath, 45, 1.6f);
        SetAmmo(SniperPath, 5, 2.6f);

        AssetDatabase.SaveAssets();
    }

    static void SetAmmo(string path, int magazine, float reloadTime)
    {
        var data = AssetDatabase.LoadAssetAtPath<ProjectileWeaponData>(path);

        if (data == null)
        {
            Debug.LogError($"[MagazineSetup] 무기 데이터를 찾을 수 없습니다: {path}");
            return;
        }

        var so = new SerializedObject(data);
        so.FindProperty("magazineSize").intValue = magazine;
        so.FindProperty("reloadTime").floatValue = reloadTime;
        so.FindProperty("autoReload").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(data);
        Debug.Log($"[MagazineSetup] {data.name}: 탄창 {magazine}발, 장전 {reloadTime}초");
    }

    // ───────── 2. 장전 애니메이터 상태 ─────────

    [MenuItem("Tools/Magazine Setup/2. 장전 애니메이터 상태")]
    public static void BuildReloadState()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        if (controller == null)
        {
            Debug.LogError($"[MagazineSetup] 애니메이터를 찾을 수 없습니다: {ControllerPath}");
            return;
        }

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ReloadClipPath);

        if (clip == null)
        {
            Debug.LogError($"[MagazineSetup] 장전 클립을 찾을 수 없습니다: {ReloadClipPath}");
            return;
        }

        EnsureTrigger(controller, ReloadTrigger);
        EnsureFloat(controller, ReloadSpeedParam, 1f);

        AnimatorStateMachine sm = FindLayer(controller, ShootLayerName);
        if (sm == null) return;

        AnimatorState state = FindState(sm, ReloadStateName);

        if (state == null)
        {
            // 기존 상태들과 겹치지 않게 아래쪽에 배치한다
            state = sm.AddState(ReloadStateName, sm.entryPosition + new Vector3(360f, 180f, 0f));
        }

        state.motion = clip;

        // 장전 시간이 무기마다 다르므로 클립을 배속 재생한다.
        // 코드가 ReloadSpeed = 클립길이(2.67초) / 장전시간 으로 넣는다.
        state.speedParameterActive = true;
        state.speedParameter = ReloadSpeedParam;

        BuildAnyStateTransition(sm, state);
        BuildExitTransition(sm, state);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log($"[MagazineSetup] {ShootLayerName} 에 {ReloadStateName} 상태 구성 완료 (클립 {clip.length:F2}초)");
    }

    /// <summary>어느 상태에서든 장전으로 넘어갈 수 있게 AnyState 전이를 만든다.</summary>
    static void BuildAnyStateTransition(AnimatorStateMachine sm, AnimatorState state)
    {
        // 여러 번 실행해도 전이가 쌓이지 않게 기존 것을 먼저 지운다
        foreach (AnimatorStateTransition t in new List<AnimatorStateTransition>(sm.anyStateTransitions))
        {
            if (t.destinationState == state)
                sm.RemoveAnyStateTransition(t);
        }

        AnimatorStateTransition tr = sm.AddAnyStateTransition(state);
        tr.hasExitTime = false;          // 트리거를 누른 즉시 넘어간다
        tr.duration = 0.05f;
        tr.canTransitionToSelf = false;  // 장전 중 재입력으로 모션이 처음부터 다시 돌지 않게
        tr.AddCondition(AnimatorConditionMode.If, 0f, ReloadTrigger);
    }

    /// <summary>장전이 끝나면 이 레이어의 기본 상태(대기)로 돌아간다.</summary>
    static void BuildExitTransition(AnimatorStateMachine sm, AnimatorState state)
    {
        AnimatorState target = sm.defaultState;

        if (target == null || target == state)
        {
            Debug.LogWarning("[MagazineSetup] ShootLayer 기본 상태를 찾지 못해 복귀 전이를 만들지 않았습니다.");
            return;
        }

        foreach (AnimatorStateTransition t in new List<AnimatorStateTransition>(state.transitions))
            state.RemoveTransition(t);

        AnimatorStateTransition tr = state.AddTransition(target);
        tr.hasExitTime = true;
        tr.exitTime = 0.95f;   // 클립이 루프 설정이라, 한 바퀴가 끝나기 직전에 빠져나온다
        tr.duration = 0.15f;
    }

    // ───────── 헬퍼 ─────────

    static AnimatorStateMachine FindLayer(AnimatorController controller, string layerName)
    {
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            if (layer.name == layerName)
                return layer.stateMachine;
        }

        Debug.LogError($"[MagazineSetup] '{layerName}' 레이어가 없습니다.");
        return null;
    }

    static AnimatorState FindState(AnimatorStateMachine sm, string stateName)
    {
        foreach (ChildAnimatorState c in sm.states)
        {
            if (c.state != null && c.state.name == stateName)
                return c.state;
        }

        return null;
    }

    static bool HasParameter(AnimatorController controller, string name)
    {
        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            if (p.name == name) return true;
        }

        return false;
    }

    static void EnsureTrigger(AnimatorController controller, string name)
    {
        if (HasParameter(controller, name)) return;

        controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
        Debug.Log($"[MagazineSetup] 파라미터 추가: {name} (Trigger)");
    }

    static void EnsureFloat(AnimatorController controller, string name, float defaultValue)
    {
        if (HasParameter(controller, name)) return;

        controller.AddParameter(new AnimatorControllerParameter
        {
            name = name,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = defaultValue
        });

        Debug.Log($"[MagazineSetup] 파라미터 추가: {name} (Float, 기본 {defaultValue})");
    }
}
