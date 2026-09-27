using _Scripts.Creatures;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Hides the Spine fields BossAnimator inherits from UnitAnimator but never uses (their
/// [SpineAnimation] drawers only error without a SkeletonAnimation), plus UnitAnimator's
/// CurrentAnimation string, which BossAnimator doesn't maintain. Rule 19: in Play mode shows what the
/// boss is actually doing, read live off its AnimationAPI. See docs/AnimationAPI.md.
/// </summary>
[CustomEditor(typeof(BossAnimator))]
public class BossAnimatorEditor : Editor
{
    private static readonly string[] HiddenFields =
        { "m_Script", "skeletonAnimation", "idle", "dead", "attack", "CurrentAnimation" };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, HiddenFields);
        serializedObject.ApplyModifiedProperties();

        var boss = (BossAnimator)target;
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play mode to see the boss's live animation state.", MessageType.Info);
            return;
        }

        // Play mode alone isn't enough. The prefab can also be selected as an asset, or open in
        // Prefab Mode, while the game runs — in both cases nothing ever called Awake() on this
        // BossAnimator, so its AnimationAPI never cached an Animator and reading live state would
        // throw UnassignedReferenceException on every repaint. (Prefab Mode objects live in a
        // preview scene and are NOT persistent, so IsPersistent alone doesn't catch them.)
        if (EditorUtility.IsPersistent(boss) || PrefabStageUtility.GetPrefabStage(boss.gameObject) != null)
        {
            EditorGUILayout.HelpBox("This is the prefab, not a live boss — select one in the scene to see its animation state.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Is dead", boss.IsDead.ToString());
        EditorGUILayout.LabelField("Current state", boss.AnimationApi.CurrentStateName ?? "-");
        EditorGUILayout.LabelField("Enabled animation", boss.AnimationApi.EnabledAnimation ?? "none");
        EditorGUILayout.LabelField("Last triggered", boss.AnimationApi.LastTriggeredAnimation ?? "-");
        Repaint();
    }
}
