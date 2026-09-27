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

    private static string[] GetNukeSlotLabels()
    {
        if (!Application.isPlaying) return new[] { "A", "B", "C" };
        return new[]
        {
            G.DefaultNukes.nukeA != null ? G.DefaultNukes.nukeA.actionName : "A",
            G.DefaultNukes.nukeB != null ? G.DefaultNukes.nukeB.actionName : "B",
            G.DefaultNukes.nukeC != null ? G.DefaultNukes.nukeC.actionName : "C",
        };
    }

    private static string[] GetSpellSlotLabels()
    {
        if (!Application.isPlaying) return new[] { "A", "B", "C" };
        return new[]
        {
            G.DefaultSpells.spellA != null ? G.DefaultSpells.spellA.actionName : "A",
            G.DefaultSpells.spellB != null ? G.DefaultSpells.spellB.actionName : "B",
            G.DefaultSpells.spellC != null ? G.DefaultSpells.spellC.actionName : "C",
        };
    }

    private void DrawNukeSection(BalanceTool tool)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Nuke Action", EditorStyles.boldLabel);

        var nukeSlotLabels = GetNukeSlotLabels();

        // Column header: " | L1 | L2 | L3 "
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("", GUILayout.Width(70));
        GUILayout.Label("L1", EditorStyles.miniBoldLabel, GUILayout.Width(30));
        GUILayout.Label("L2", EditorStyles.miniBoldLabel, GUILayout.Width(30));
        GUILayout.Label("L3", EditorStyles.miniBoldLabel, GUILayout.Width(30));
        EditorGUILayout.EndHorizontal();

        for (int s = 0; s < 3; s++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(nukeSlotLabels[s], GUILayout.Width(70));
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

        bool nextCastAsEnemy = EditorGUILayout.ToggleLeft("Cast as Enemy (onto Player)", tool.CastNukeAsEnemy);
        if (nextCastAsEnemy != tool.CastNukeAsEnemy)
        {
            tool.CastNukeAsEnemy = nextCastAsEnemy;
            EditorUtility.SetDirty(tool);
        }

        bool nukeInProgress = Application.isPlaying && tool.IsNukeActionInProgress;
        bool canPlay = Application.isPlaying && !nukeInProgress && tool.IsNukeSelectionValid();

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(!canPlay);
        string label = nukeInProgress ? "Nuke action in progress..."
            : tool.CastNukeAsEnemy ? "Play Nuke Action (as Enemy)" : "Play Nuke Action";
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

        var spellSlotLabels = GetSpellSlotLabels();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("", GUILayout.Width(70));
        GUILayout.Label("L1", EditorStyles.miniBoldLabel, GUILayout.Width(30));
        GUILayout.Label("L2", EditorStyles.miniBoldLabel, GUILayout.Width(30));
        GUILayout.Label("L3", EditorStyles.miniBoldLabel, GUILayout.Width(30));
        EditorGUILayout.EndHorizontal();

        for (int s = 0; s < 3; s++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(spellSlotLabels[s], GUILayout.Width(70));
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

        bool nextCastAsEnemy = EditorGUILayout.ToggleLeft("Cast as Enemy (onto Player)", tool.CastSpellAsEnemy);
        if (nextCastAsEnemy != tool.CastSpellAsEnemy)
        {
            tool.CastSpellAsEnemy = nextCastAsEnemy;
            EditorUtility.SetDirty(tool);
        }

        bool spellInProgress = Application.isPlaying && tool.IsSpellActionInProgress;
        bool canPlay = Application.isPlaying && !spellInProgress && tool.IsSpellSelectionValid();

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(!canPlay);
        string label = spellInProgress ? "Spell action in progress..."
            : tool.CastSpellAsEnemy ? "Play Spell Action (as Enemy)" : "Play Spell Action";
        if (GUILayout.Button(label)) tool.PlaySpellAction();
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(spellInProgress || selected == 0);
        if (GUILayout.Button("Clear", GUILayout.Width(60))) tool.ClearSpellToggles();
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
    }
}
