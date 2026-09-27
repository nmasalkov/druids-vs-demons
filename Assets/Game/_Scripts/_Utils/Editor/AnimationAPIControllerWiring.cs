using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Utils;

/// <summary>
/// Generates the parameter + transition wiring <see cref="AnimationAPI"/> expects, from the states
/// already in a controller's base layer. Destructive on purpose — every existing Any State, Entry
/// and outgoing state transition of that layer is replaced — and idempotent, so re-running it
/// after adding a state is the whole workflow. See docs/AnimationAPI.md.
///
/// For every root state N of layer 0:
/// <list type="bullet">
/// <item>Any State → N on trigger <c>N</c> (no exit time, can re-enter itself: repeated hits restart Hurt).</item>
/// <item>Entry → N when <c>IsN</c> is true.</item>
/// <item>N → Exit when <c>IsN</c> is false, at exit time — a triggered one-shot finishes, leaves
/// through Exit and re-enters through Entry into whichever animation is enabled (or the default
/// state if none is); an enabled animation never satisfies its own exit, so it just loops.</item>
/// </list>
/// </summary>
public static class AnimationAPIControllerWiring
{
    private const float ReturnBlendDuration = 0.1f;

    // Must stay below 1: Unity re-checks exit times < 1 on every loop, but evaluates exit times
    // >= 1 only once — a looping state would then never leave after its first cycle.
    private const float MaxExitTime = 0.99f;

    public static AnimatorController ResolveController(Animator animator)
    {
        var runtime = animator.runtimeAnimatorController;
        if (runtime is AnimatorOverrideController overrideController)
            runtime = overrideController.runtimeAnimatorController;
        return runtime as AnimatorController;
    }

    public static IReadOnlyList<AnimatorState> RootStates(AnimatorController controller) =>
        controller.layers[0].stateMachine.states.Select(s => s.state).ToList();

    public static bool IsWired(AnimatorController controller, string stateName) =>
        HasParameter(controller, stateName, AnimatorControllerParameterType.Trigger)
        && HasParameter(controller, AnimationAPI.BoolPrefix + stateName, AnimatorControllerParameterType.Bool);

    /// <returns>False (and logs why) if a parameter name collision made it abort before changing anything.</returns>
    public static bool Wire(AnimatorController controller)
    {
        var stateMachine = controller.layers[0].stateMachine;
        var states = RootStates(controller);

        if (!ValidateParameterTypes(controller, states)) return false;
        WarnAboutIgnoredParts(controller, stateMachine);

        AddMissingParameters(controller, states);
        ClearTransitions(stateMachine);
        foreach (var state in states)
            WireState(stateMachine, state);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AnimationAPI] Wired {states.Count} states in '{controller.name}': " +
                  string.Join(", ", states.Select(s => s.name)));
        return true;
    }

    private static bool ValidateParameterTypes(AnimatorController controller, IReadOnlyList<AnimatorState> states)
    {
        foreach (var state in states)
        {
            if (!HasCollision(controller, state.name)) continue;
            Debug.LogError($"[AnimationAPI] '{controller.name}': a parameter named '{state.name}' or " +
                           $"'{AnimationAPI.BoolPrefix}{state.name}' already exists with a different type. " +
                           "Rename or delete it, then wire again. Nothing was changed.");
            return false;
        }
        return true;
    }

    private static bool HasCollision(AnimatorController controller, string stateName) =>
        HasWrongType(controller, stateName, AnimatorControllerParameterType.Trigger)
        || HasWrongType(controller, AnimationAPI.BoolPrefix + stateName, AnimatorControllerParameterType.Bool);

    private static void WarnAboutIgnoredParts(AnimatorController controller, AnimatorStateMachine stateMachine)
    {
        if (controller.layers.Length > 1 || stateMachine.stateMachines.Length > 0)
            Debug.LogWarning($"[AnimationAPI] '{controller.name}': only layer 0's root states are wired; " +
                             "extra layers and sub-state machines are left untouched.");
    }

    private static void AddMissingParameters(AnimatorController controller, IReadOnlyList<AnimatorState> states)
    {
        foreach (var state in states)
        {
            AddParameterIfMissing(controller, state.name, AnimatorControllerParameterType.Trigger);
            AddParameterIfMissing(controller, AnimationAPI.BoolPrefix + state.name, AnimatorControllerParameterType.Bool);
        }
    }

    private static void ClearTransitions(AnimatorStateMachine stateMachine)
    {
        foreach (var transition in stateMachine.anyStateTransitions)
            stateMachine.RemoveAnyStateTransition(transition);
        foreach (var transition in stateMachine.entryTransitions)
            stateMachine.RemoveEntryTransition(transition);
        foreach (var child in stateMachine.states)
            ClearStateTransitions(child.state);
    }

    private static void ClearStateTransitions(AnimatorState state)
    {
        foreach (var transition in state.transitions)
            state.RemoveTransition(transition);
    }

    private static void WireState(AnimatorStateMachine stateMachine, AnimatorState state)
    {
        string boolName = AnimationAPI.BoolPrefix + state.name;

        var trigger = stateMachine.AddAnyStateTransition(state);
        trigger.AddCondition(AnimatorConditionMode.If, 0f, state.name);
        trigger.hasExitTime = false;
        trigger.hasFixedDuration = true;
        trigger.duration = 0f;
        trigger.canTransitionToSelf = true;

        var entry = stateMachine.AddEntryTransition(state);
        entry.AddCondition(AnimatorConditionMode.If, 0f, boolName);

        var exit = state.AddExitTransition();
        exit.AddCondition(AnimatorConditionMode.IfNot, 0f, boolName);
        exit.hasExitTime = true;
        exit.exitTime = ExitTimeFor(state);
        exit.hasFixedDuration = true;
        exit.duration = ReturnBlendDuration;
    }

    /// <summary>Starts the return blend so it finishes right as the clip ends.</summary>
    private static float ExitTimeFor(AnimatorState state)
    {
        float length = state.motion is AnimationClip clip ? clip.length : 0f;
        if (length <= ReturnBlendDuration) return 0f;
        return Mathf.Min(1f - ReturnBlendDuration / length, MaxExitTime);
    }

    private static bool HasParameter(AnimatorController controller, string parameterName, AnimatorControllerParameterType type) =>
        controller.parameters.Any(p => p.name == parameterName && p.type == type);

    private static bool HasWrongType(AnimatorController controller, string parameterName, AnimatorControllerParameterType type) =>
        controller.parameters.Any(p => p.name == parameterName && p.type != type);

    private static void AddParameterIfMissing(AnimatorController controller, string parameterName, AnimatorControllerParameterType type)
    {
        if (HasParameter(controller, parameterName, type)) return;
        controller.AddParameter(parameterName, type);
    }
}
