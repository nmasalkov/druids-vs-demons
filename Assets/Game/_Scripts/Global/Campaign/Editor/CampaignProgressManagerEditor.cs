using UnityEditor;
using UnityEngine;

/// <summary>
/// Surfaces CampaignProgressManager's live navigation state — see CLAUDE.md's "show important
/// state in the Inspector" rule. Current encounter index/id/asset are load-bearing state (what
/// battle you're actually on), so they're shown directly rather than buried in private fields.
/// </summary>
[CustomEditor(typeof(CampaignProgressManager))]
public class CampaignProgressManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var manager = (CampaignProgressManager)target;
        var encounterListProp = serializedObject.FindProperty("encounterList");

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Current Progress", EditorStyles.boldLabel);

        var encounterList = encounterListProp.objectReferenceValue as EncounterListSO;
        if (encounterList == null || encounterList.encounters == null || encounterList.encounters.Count == 0)
        {
            EditorGUILayout.HelpBox("No Encounter List assigned (or it's empty).", MessageType.Warning);
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("Encounter Index", manager.CurrentEncounterIndex);

            var current = manager.CurrentEncounter;
            EditorGUILayout.ObjectField("Current Encounter", current, typeof(EncounterSO), false);

            if (current is BattleSO battle)
                EditorGUILayout.TextField("Battle Id", battle.battleId);
        }

        if (Application.isPlaying) Repaint();
    }
}
