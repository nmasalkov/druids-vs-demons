using UnityEditor;
using UnityEngine;
using Utils;

/// <summary>
/// Rule 19: shows what DepthSortingOrder actually resolved at runtime — which renderer it picked as
/// the main visual, the order it is currently writing, and every companion renderer it drives with
/// the offset it kept. Without this the whole thing is invisible private state, and "why is this
/// creature drawing in front of that one" is only answerable in a debugger.
/// </summary>
[CustomEditor(typeof(DepthSortingOrder))]
public class DepthSortingOrderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var tool = (DepthSortingOrder)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Resolved on Awake — enter Play mode to see the detected " +
                                    "renderer and live sorting order.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Main renderer",
            tool.Main != null ? $"{tool.Main.name} ({tool.Main.GetType().Name})" : "none");
        EditorGUILayout.LabelField("Sorting layer",
            tool.Main != null ? tool.Main.sortingLayerName : "-");
        EditorGUILayout.LabelField("Current order", tool.CurrentOrder.ToString());

        EditorGUILayout.LabelField($"Driven renderers ({tool.Group.Count})");
        EditorGUI.indentLevel++;
        for (int i = 0; i < tool.Group.Count; i++)
        {
            var r = tool.Group[i];
            EditorGUILayout.LabelField(r != null ? r.name : "<destroyed>",
                $"offset {tool.Offsets[i]:+0;-0;0}  ->  order {(r != null ? r.sortingOrder : 0)}");
        }
        EditorGUI.indentLevel--;

        Repaint();
    }
}
