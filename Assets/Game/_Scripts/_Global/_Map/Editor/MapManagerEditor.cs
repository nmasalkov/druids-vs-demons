using UnityEditor;
using UnityEngine;

/// <summary>
/// Surfaces MapManager's live state — see CLAUDE.md's "show important state in the Inspector"
/// rule. Mirrors CampaignManagerEditor.
/// </summary>
[CustomEditor(typeof(MapManager))]
public class MapManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var manager = (MapManager)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Live Map State", EditorStyles.boldLabel);

        if (!Application.isPlaying || CampaignManager.Instance == null)
        {
            EditorGUILayout.HelpBox("Enter Play mode to see live map state.", MessageType.None);
            return;
        }

        var campaign = CampaignManager.Instance;

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.EnumPopup("State", manager.State);
            EditorGUILayout.EnumPopup("Entered As", manager.LastEntry);
            EditorGUILayout.IntField("Frontier Node", campaign.ProgressNodeIndex);
            EditorGUILayout.IntField("Selected Encounter Index", campaign.CurrentEncounterIndex);
            EditorGUILayout.ObjectField("Selected Encounter", campaign.CurrentFight, typeof(FightSO), false);
            EditorGUILayout.ObjectField("Selected Map Point", manager.SelectedPoint, typeof(MapEncounterPoint), true);
            EditorGUILayout.Toggle("Is Replay", campaign.IsReplay);
        }

        Repaint();
    }
}
