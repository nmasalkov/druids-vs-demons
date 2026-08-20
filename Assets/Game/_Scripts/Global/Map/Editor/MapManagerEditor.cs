using UnityEditor;
using UnityEngine;

/// <summary>
/// Surfaces MapManager's live reveal state — see CLAUDE.md's "show important state in the
/// Inspector" rule. Mirrors CampaignManagerEditor.
/// </summary>
[CustomEditor(typeof(MapManager))]
public class MapManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var manager = (MapManager)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Current Progress", EditorStyles.boldLabel);

        if (!Application.isPlaying || CampaignManager.Instance == null)
        {
            EditorGUILayout.HelpBox("Enter Play mode to see live map state.", MessageType.None);
            return;
        }

        var current = CampaignManager.Instance.CurrentFight;

        MapEncounterPoint currentPoint = null;
        if (manager.Points != null)
        {
            foreach (var point in manager.Points)
            {
                if (point.Fight == current) { currentPoint = point; break; }
            }
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("Encounter Index", CampaignManager.Instance.CurrentEncounterIndex);
            EditorGUILayout.ObjectField("Current Encounter", current, typeof(FightSO), false);
            EditorGUILayout.ObjectField("Current Map Point", currentPoint, typeof(MapEncounterPoint), true);
        }

        if (currentPoint == null)
            EditorGUILayout.HelpBox("No placed MapEncounterPoint resolves to the current encounter.", MessageType.Warning);

        Repaint();
    }
}
