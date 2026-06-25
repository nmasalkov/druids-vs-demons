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

        DrawNukeSection(tool);
        DrawSpellSection(tool);

        if (battleInProgress || (Application.isPlaying && (tool.IsNukeActionInProgress || tool.IsSpellActionInProgress)))
            Repaint();
    }

    private static readonly string[] NukeSlotLabels = { "A", "B", "C" };
    private static readonly string[] SpellSlotLabels = { "A", "B", "C" };

    private void DrawNukeSection(BalanceTool tool)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Nuke Action", EditorStyles.boldLabel);

        // Column header: " | L1 | L2 | L3 "
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("", GUILayout.Width(40));
        GUILayout.Label("L1", EditorStyles.miniBoldLabel, GUILayout.Width(30));
        GUILayout.Label("L2", EditorStyles.miniBoldLabel, GUILayout.Width(30));
        GUILayout.Label("L3", EditorStyles.miniBoldLabel, GUILayout.Width(30));
        EditorGUILayout.EndHorizontal();

        for (int s = 0; s < 3; s++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(NukeSlotLabels[s], GUILayout.Width(40));
            for (int l = 0; l < 3; l++)
            {
                bool current = tool.NukeToggles[s, l];
                bool next = GUILayout.Toggle(current, GUIContent.none, GUILayout.Width(30));
                if (next != current)
                {
                    tool.NukeToggles[s, l] = next;
                    EditorUtility.SetDirty(tool);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        int selected = tool.CountSelectedNukes();
        EditorGUILayout.LabelField($"Selected: {selected} (need 1..3)");

        bool nukeInProgress = Application.isPlaying && tool.IsNukeActionInProgress;
        bool canPlay = Application.isPlaying && !nukeInProgress && tool.IsNukeSelectionValid();

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(!canPlay);
        string label = nukeInProgress ? "Nuke action in progress..." : "Play Nuke Action";
        if (GUILayout.Button(label)) tool.PlayNukeAction();
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(nukeInProgress || selected == 0);
        if (GUILayout.Button("Clear", GUILayout.Width(60))) tool.ClearNukeToggles();
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSpellSection(BalanceTool tool)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Spell Action", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("", GUILayout.Width(40));
        GUILayout.Label("L1", EditorStyles.miniBoldLabel, GUILayout.Width(30));
        GUILayout.Label("L2", EditorStyles.miniBoldLabel, GUILayout.Width(30));
        GUILayout.Label("L3", EditorStyles.miniBoldLabel, GUILayout.Width(30));
        EditorGUILayout.EndHorizontal();

        for (int s = 0; s < 3; s++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(SpellSlotLabels[s], GUILayout.Width(40));
            for (int l = 0; l < 3; l++)
            {
                bool current = tool.SpellToggles[s, l];
                bool next = GUILayout.Toggle(current, GUIContent.none, GUILayout.Width(30));
                if (next != current)
                {
                    tool.SpellToggles[s, l] = next;
                    EditorUtility.SetDirty(tool);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        int selected = tool.CountSelectedSpells();
        EditorGUILayout.LabelField($"Selected: {selected} (need 1..3)");

        bool spellInProgress = Application.isPlaying && tool.IsSpellActionInProgress;
        bool canPlay = Application.isPlaying && !spellInProgress && tool.IsSpellSelectionValid();

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(!canPlay);
        string label = spellInProgress ? "Spell action in progress..." : "Play Spell Action";
        if (GUILayout.Button(label)) tool.PlaySpellAction();
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(spellInProgress || selected == 0);
        if (GUILayout.Button("Clear", GUILayout.Width(60))) tool.ClearSpellToggles();
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
    }
}
