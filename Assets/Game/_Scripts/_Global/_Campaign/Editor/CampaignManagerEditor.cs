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

            // Boss-phase state: which campaign node this index actually belongs to, and whether
            // the fight is mid-multi-phase. Load-bearing for understanding current behaviour —
            // a phase index looks like an ordinary encounter index otherwise (CLAUDE.md rule 19).
            EditorGUILayout.IntField("Map Node / Point", manager.NodeIndexOf(manager.CurrentEncounterIndex));
            EditorGUILayout.Toggle("Is Boss Phase", current.continuesPreviousFight);
            EditorGUILayout.ObjectField("Next Phase", manager.NextPhase, typeof(FightSO), false);
        }

        if (manager.CurrentFight.continuesPreviousFight)
        {
            EditorGUILayout.HelpBox(
                $"Playing a boss phase. This index is not saved — a defeat or restart rewinds to " +
                $"encounter {manager.NodeStartIndex(manager.CurrentEncounterIndex)}, so both phases " +
                "must be won in one session.", MessageType.Info);
        }

        if (Application.isPlaying) Repaint();
    }
}
