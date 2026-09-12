using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 구르기(Roll) 애니메이션을 PlayerAnimator에 자동으로 배선하는 에디터 도구.
/// 메뉴: Tools / Player / Setup Roll Animation
///
/// 하는 일
///  1) Roll.fbx 의 루트 모션 설정을 맞춘다 (Rotation/Y 는 Bake, XZ 는 해제)
///  2) 컨트롤러에 Roll(Trigger) 파라미터를 추가한다
///  3) Base Layer 에 Roll 상태를 만들고 클립을 연결한다
///  4) Any State -> Roll (조건 Roll, Can Transition To Self 해제) 전이를 만든다
///  5) Roll -> Idle (Has Exit Time, Exit Time 1) 전이를 만든다
///
/// 여러 번 실행해도 안전하다(이미 있으면 기존 것을 고쳐 쓴다).
/// </summary>
public static class PlayerRollSetup
{
    const string ControllerPath = "Assets/Animations/Player/PlayerAnimator.controller";
    const string RollFbxPath = "Assets/Animations/Player/Roll.fbx";

    const string RollParam = "Roll";
    const string RollStateName = "Roll";

    // PlayerMove 의 rollDuration 과 맞춘다
    const float RollDuration = 1.5f;

    [MenuItem("Tools/Player/Setup Roll Animation")]
    public static void Setup()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[RollSetup] 컨트롤러를 찾지 못했습니다: {ControllerPath}");
            return;
        }

        AnimationClip clip = FindRollClip();
        if (clip == null)
        {
            Debug.LogError($"[RollSetup] {RollFbxPath} 안에서 AnimationClip을 찾지 못했습니다.");
            return;
        }

        FixImportSettings();
        AddTriggerParameter(controller);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState rollState = FindState(sm, RollStateName);
        if (rollState == null)
        {
            rollState = sm.AddState(RollStateName, sm.entryPosition + new Vector3(350f, 140f, 0f));
            Debug.Log("[RollSetup] Roll 상태를 새로 만들었습니다.");
        }

        rollState.motion = clip;
        rollState.speed = ComputeSpeed(clip);
        rollState.writeDefaultValues = InferWriteDefaults(sm);

        SetupAnyStateTransition(sm, rollState);
        SetupExitTransition(sm, rollState);

        EditorUtility.SetDirty(rollState);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[RollSetup] 완료. 클립 '{clip.name}' 길이 {clip.length:F3}초 / " +
            $"rollDuration {RollDuration}초 / 적용 Speed {rollState.speed:F3}");
    }

    // ── 1) 임포트 설정: Bake Into Pose 켜기 ──────────────────────
    // Apply Root Motion 이 꺼진 프로젝트에서 Bake Into Pose 가 꺼져 있으면
    // 구르기의 회전이 루트 모션으로 추출된 뒤 그대로 버려져서 모션이 밋밋해진다.
    static void FixImportSettings()
    {
        var importer = AssetImporter.GetAtPath(RollFbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[RollSetup] ModelImporter를 찾지 못했습니다: {RollFbxPath}");
            return;
        }

        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning("[RollSetup] 클립 정보를 읽지 못했습니다.");
            return;
        }

        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i].lockRootRotation && clips[i].lockRootHeightY && !clips[i].lockRootPositionXZ
                && clips[i].keepOriginalOrientation && clips[i].keepOriginalPositionY
                && clips[i].keepOriginalPositionXZ)
                continue;

            // Rotation / Position(Y) 는 포즈에 남겨 구르는 동작이 보이게 한다.
            // Position(XZ) 는 포즈에서 빼야 한다. 포즈에 남기면 메시가 앞으로 이동해
            // 코드로 움직이는 실제 위치(콜라이더)와 어긋난다.
            clips[i].lockRootRotation = true;     // Rotation      -> Bake Into Pose ON
            clips[i].lockRootHeightY = true;      // Position (Y)  -> Bake Into Pose ON
            clips[i].lockRootPositionXZ = false;  // Position (XZ) -> Bake Into Pose OFF

            // Based Upon 을 모두 Original 로. Center of Mass 기준이면 구르는 동안
            // 무게중심이 크게 움직여 메시가 루트에서 밀려난다.
            clips[i].keepOriginalOrientation = true;
            clips[i].keepOriginalPositionY = true;
            clips[i].keepOriginalPositionXZ = true;

            clips[i].loopTime = false;
            changed = true;
        }

        if (!changed)
        {
            Debug.Log("[RollSetup] Bake Into Pose 설정은 이미 켜져 있습니다.");
            return;
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        Debug.Log("[RollSetup] 루트 모션 설정(Rotation/Y=Bake, XZ=해제, Based Upon=Original)을 적용하고 재임포트했습니다.");
    }

    // ── 2) Trigger 파라미터 ─────────────────────────────────────
    static void AddTriggerParameter(AnimatorController controller)
    {
        foreach (var p in controller.parameters)
        {
            if (p.name != RollParam) continue;

            if (p.type != AnimatorControllerParameterType.Trigger)
                Debug.LogWarning($"[RollSetup] '{RollParam}' 파라미터가 Trigger가 아닙니다: {p.type}");
            else
                Debug.Log($"[RollSetup] '{RollParam}' 파라미터가 이미 있습니다.");
            return;
        }

        controller.AddParameter(RollParam, AnimatorControllerParameterType.Trigger);
        Debug.Log($"[RollSetup] '{RollParam}' Trigger 파라미터를 추가했습니다.");
    }

    // ── 4) Any State -> Roll ────────────────────────────────────
    static void SetupAnyStateTransition(AnimatorStateMachine sm, AnimatorState rollState)
    {
        AnimatorStateTransition transition = null;

        foreach (var t in sm.anyStateTransitions)
        {
            if (t.destinationState != rollState) continue;
            transition = t;
            break;
        }

        if (transition == null)
        {
            transition = sm.AddAnyStateTransition(rollState);
            Debug.Log("[RollSetup] Any State -> Roll 전이를 만들었습니다.");
        }

        // 조건을 Roll 하나로 재설정
        for (int i = transition.conditions.Length - 1; i >= 0; i--)
            transition.RemoveCondition(transition.conditions[i]);

        transition.AddCondition(AnimatorConditionMode.If, 0f, RollParam);

        transition.canTransitionToSelf = false;  // ★ 연타 시 처음부터 재생되는 것 방지
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.08f;
        transition.offset = 0f;
        transition.interruptionSource = TransitionInterruptionSource.None;

        EditorUtility.SetDirty(transition);

        // 조건이 실제로 붙었는지 확인한다. 이게 비면 트리거를 쏴도 전이가 일어나지 않는다.
        if (transition.conditions.Length == 0)
        {
            Debug.LogError(
                "[RollSetup] Any State -> Roll 전이에 조건을 붙이지 못했습니다. " +
                "Animator 창에서 해당 화살표를 선택하고 Conditions에 'Roll'을 수동으로 추가하세요.");
        }
        else
        {
            Debug.Log($"[RollSetup] Any State -> Roll 조건 확인: {transition.conditions[0].parameter}");
        }
    }

    // ── 5) Roll -> Idle ─────────────────────────────────────────
    static void SetupExitTransition(AnimatorStateMachine sm, AnimatorState rollState)
    {
        AnimatorState idle = FindIdleState(sm);
        if (idle == null)
        {
            Debug.LogWarning("[RollSetup] Idle 상태를 찾지 못해 Roll -> Idle 전이를 건너뜁니다. 수동으로 연결하세요.");
            return;
        }

        AnimatorStateTransition transition = null;
        foreach (var t in rollState.transitions)
        {
            if (t.destinationState != idle) continue;
            transition = t;
            break;
        }

        if (transition == null)
        {
            transition = rollState.AddTransition(idle);
            Debug.Log($"[RollSetup] Roll -> {idle.name} 전이를 만들었습니다.");
        }

        for (int i = transition.conditions.Length - 1; i >= 0; i--)
            transition.RemoveCondition(transition.conditions[i]);

        transition.hasExitTime = true;
        transition.exitTime = 1f;
        transition.hasFixedDuration = true;
        transition.duration = 0.1f;

        EditorUtility.SetDirty(transition);
    }

    // ── 헬퍼 ────────────────────────────────────────────────────

    static AnimationClip FindRollClip()
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(RollFbxPath))
        {
            var clip = asset as AnimationClip;
            if (clip == null) continue;
            if (clip.name.StartsWith("__preview__")) continue;
            return clip;
        }
        return null;
    }

    static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        foreach (var child in sm.states)
            if (child.state != null && child.state.name == name)
                return child.state;
        return null;
    }

    static AnimatorState FindIdleState(AnimatorStateMachine sm)
    {
        // 기본 상태가 Idle 계열이면 그것을 우선 사용
        if (sm.defaultState != null && sm.defaultState.name.ToLower().Contains("idle"))
            return sm.defaultState;

        foreach (var child in sm.states)
            if (child.state != null && child.state.name.ToLower().Contains("idle"))
                return child.state;

        return sm.defaultState;
    }

    // 클립 길이를 rollDuration 에 맞추는 재생 배수
    static float ComputeSpeed(AnimationClip clip)
    {
        if (clip.length <= 0.001f) return 1f;
        return clip.length / RollDuration;
    }

    // 컨트롤러의 기존 상태들이 쓰는 Write Defaults 값을 따라간다(혼용 시 포즈가 튀는 것을 방지)
    static bool InferWriteDefaults(AnimatorStateMachine sm)
    {
        foreach (var child in sm.states)
            if (child.state != null && child.state.name != RollStateName)
                return child.state.writeDefaultValues;
        return true;
    }
}
