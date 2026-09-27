using UnityEditor;
using UnityEngine;
using Utils;

/// <summary>
/// Edit mode: lists the controller's base-layer states, marks which already have the AnimationAPI
/// trigger/bool pair, and offers the one-click "Wire controller" (AnimationAPIControllerWiring).
/// Play mode (rule 19): what the Animator is actually doing — current state, enabled animation,
/// last trigger — plus a Trigger/Enable button per animation for poking at it live.
/// See docs/AnimationAPI.md.
/// </summary>
[CustomEditor(typeof(AnimationAPI))]
public class AnimationAPIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var api = (AnimationAPI)target;
        EditorGUILayout.Space(8);

        if (Application.isPlaying)
        {
            DrawRuntime(api);
            return;
        }
        DrawWiring(api);
    }

    private static void DrawWiring(AnimationAPI api)
    {
        EditorGUILayout.LabelField("Controller wiring", EditorStyles.boldLabel);

        var controller = AnimationAPIControllerWiring.ResolveController(api.GetComponent<Animator>());
        if (controller == null)
        {
            EditorGUILayout.HelpBox("Assign an AnimatorController to the Animator first.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("Controller", AssetDatabase.GetAssetPath(controller));
        EditorGUI.indentLevel++;
        foreach (var state in AnimationAPIControllerWiring.RootStates(controller))
        {
            bool wired = AnimationAPIControllerWiring.IsWired(controller, state.name);
            EditorGUILayout.LabelField(state.name,
                wired ? $"trigger {state.name} / bool {AnimationAPI.BoolPrefix}{state.name}" : "not wired");
        }
        EditorGUI.indentLevel--;

        if (IsVendorAsset(controller))
            EditorGUILayout.HelpBox("This controller lives outside Assets/Game (third-party). Copy it into " +
                                    "Assets/Game and wire the copy instead — a re-import would wipe the wiring.",
                                    MessageType.Warning);

        if (!GUILayout.Button("Wire controller")) return;
        if (!EditorUtility.DisplayDialog("Wire controller for AnimationAPI",
                $"Replace every Any State, Entry and state transition in layer 0 of '{controller.name}' " +
                "with the AnimationAPI convention (and add missing trigger/bool parameters)?",
                "Wire", "Cancel")) return;
        AnimationAPIControllerWiring.Wire(controller);
    }

    private static bool IsVendorAsset(Object asset) =>
        !AssetDatabase.GetAssetPath(asset).StartsWith("Assets/Game/");

    private void DrawRuntime(AnimationAPI api)
    {
        EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Current state", api.CurrentStateName ?? "-");
        EditorGUILayout.LabelField("Enabled animation", api.EnabledAnimation ?? "none");
        EditorGUILayout.LabelField("Last triggered", api.LastTriggeredAnimation ?? "-");

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"Animations ({api.AnimationNames.Count})", EditorStyles.boldLabel);
        foreach (string animationName in api.AnimationNames)
            DrawAnimationRow(api, animationName);

        Repaint();
    }

    private static void DrawAnimationRow(AnimationAPI api, string animationName)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(animationName);
            if (GUILayout.Button("Trigger", GUILayout.Width(70))) api.TriggerAnimation(animationName);
            if (GUILayout.Button("Enable", GUILayout.Width(70))) api.EnableAnimation(animationName);
        }
    }
}
