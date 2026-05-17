using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BalanceTool))]
public class BalanceToolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var tool = (BalanceTool)target;

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Player", EditorStyles.boldLabel);
        if (GUILayout.Button("Spawn Mage")) tool.SpawnPlayerMage();
        if (GUILayout.Button("Spawn Archer")) tool.SpawnPlayerArcher();
        if (GUILayout.Button("Spawn Tank")) tool.SpawnPlayerTank();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Enemy", EditorStyles.boldLabel);
        if (GUILayout.Button("Spawn Mage")) tool.SpawnEnemyMage();
        if (GUILayout.Button("Spawn Archer")) tool.SpawnEnemyArcher();
        if (GUILayout.Button("Spawn Tank")) tool.SpawnEnemyTank();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Battle", EditorStyles.boldLabel);

        bool battleInProgress = Application.isPlaying && tool.IsBattleInProgress;
        EditorGUI.BeginDisabledGroup(battleInProgress);
        if (GUILayout.Button(battleInProgress ? "Battle in progress..." : "Play Battle")) tool.PlayBattle();
        EditorGUI.EndDisabledGroup();

        if (battleInProgress)
            Repaint();
    }
}

