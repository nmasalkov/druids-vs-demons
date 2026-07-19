using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CampaignProgressTool))]
public class CampaignProgressToolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Campaign Navigation", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Calls the real CampaignProgressManager API — the same methods gameplay UI will use, " +
            "not separate debug-only logic.", MessageType.Info);

        bool available = Application.isPlaying && CampaignProgressManager.Instance != null;

        EditorGUI.BeginDisabledGroup(!available);
        if (GUILayout.Button("Start New Run")) CampaignProgressManager.Instance.StartNewRun();
        if (GUILayout.Button("Reset Current Encounter")) CampaignProgressManager.Instance.ResetCurrentEncounter();
        if (GUILayout.Button("Advance To Next Encounter")) CampaignProgressManager.Instance.AdvanceToNextEncounter();
        EditorGUI.EndDisabledGroup();

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play mode to use these.", MessageType.None);
    }
}
