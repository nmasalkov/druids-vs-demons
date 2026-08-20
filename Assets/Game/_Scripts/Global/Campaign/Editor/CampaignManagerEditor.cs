using UnityEditor;
using UnityEngine;

/// <summary>
/// Surfaces CampaignManager's live navigation state — see CLAUDE.md's "show important state in
/// the Inspector" rule. Current encounter index/id/asset are load-bearing state (what battle
/// you're actually on), so they're shown directly rather than buried in private fields. Full
/// RunState visibility (every field, not just navigation) lives on the sibling `RunStateMonitor`
/// child GameObject instead — see docs/Campaign.md.
/// </summary>
[CustomEditor(typeof(CampaignManager))]
public class CampaignManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var manager = (CampaignManager)target;
        var encounterListProp = serializedObject.FindProperty("encounterList");

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Current Progress", EditorStyles.boldLabel);

        var encounterList = encounterListProp.objectReferenceValue as EncounterListSO;
        if (encounterList == null || encounterList.fights == null || encounterList.fights.Count == 0)
        {
            EditorGUILayout.HelpBox("No Encounter List assigned (or it's empty).", MessageType.Warning);
            return;
        }

        // RunState (owned by CampaignStateManager) is only populated by its Awake(), which never
        // runs outside Play mode — reading CurrentEncounterIndex/CurrentFight here otherwise
        // throws (this object is placed in both BattleScene and MapScene, so it gets selected/
        // inspected in Edit mode routinely).
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play mode to see live progress.", MessageType.None);
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("Encounter Index", manager.CurrentEncounterIndex);

            var current = manager.CurrentFight;
            EditorGUILayout.ObjectField("Current Encounter", current, typeof(FightSO), false);
            EditorGUILayout.TextField("Fight Id", current.fightId);
        }

        if (Application.isPlaying) Repaint();
    }
}
