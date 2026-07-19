using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CampaignDebugTool))]
public class CampaignDebugToolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var tool = (CampaignDebugTool)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Run State Overrides", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Checked fields override CampaignManager's RunState in Awake(), before anything " +
            "reads it. Never saved — set overrides, press Play, test.", MessageType.Info);

        DrawToggleAndInt(tool, "Max HP", ref tool.overrideMaxHp, ref tool.maxHp);
        DrawToggleAndInt(tool, "Energy Capacity", ref tool.overrideEnergyCapacity, ref tool.energyCapacity);

        EditorGUILayout.Space(6);
        DrawToggleAndObject(tool, "Archer", ref tool.overrideArcher, ref tool.archer);
        DrawToggleAndObject(tool, "Tank", ref tool.overrideTank, ref tool.tank);
        DrawToggleAndObject(tool, "Mage", ref tool.overrideMage, ref tool.mage);

        EditorGUILayout.Space(6);
        DrawToggleAndObject(tool, "Nuke A", ref tool.overrideNukeA, ref tool.nukeA);
        DrawToggleAndObject(tool, "Nuke B", ref tool.overrideNukeB, ref tool.nukeB);
        DrawToggleAndObject(tool, "Nuke C", ref tool.overrideNukeC, ref tool.nukeC);

        EditorGUILayout.Space(6);
        DrawToggleAndObject(tool, "Spell A", ref tool.overrideSpellA, ref tool.spellA);
        DrawToggleAndObject(tool, "Spell B", ref tool.overrideSpellB, ref tool.spellB);
        DrawToggleAndObject(tool, "Spell C", ref tool.overrideSpellC, ref tool.spellC);

        DrawSavedRunSection();
    }

    /// <summary>
    /// Read-only view of whatever CampaignManager.Save() last wrote to PlayerPrefs — there's no
    /// built-in Editor window for browsing PlayerPrefs, so this is the debug affordance for it.
    /// Independent of the override fields above and of Play mode: reads PlayerPrefs directly,
    /// works in Edit mode too.
    /// </summary>
    private void DrawSavedRunSection()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Saved Run (PlayerPrefs)", EditorStyles.boldLabel);

        bool hasSave = PlayerPrefs.HasKey(CampaignManager.SaveKey);
        if (!hasSave)
        {
            EditorGUILayout.HelpBox(
                "No saved run yet — nothing has called CampaignManager.Save() (this debug tool " +
                "never does). A fresh RunState is used every time you press Play until something does.",
                MessageType.None);
        }
        else
        {
            string raw = PlayerPrefs.GetString(CampaignManager.SaveKey);
            string pretty = raw;
            try
            {
                pretty = JsonUtility.ToJson(JsonUtility.FromJson<RunState>(raw), true);
            }
            catch { /* show the raw string if it doesn't parse as RunState */ }

            EditorGUILayout.SelectableLabel(pretty, EditorStyles.textArea, GUILayout.Height(220));
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh")) Repaint();
        EditorGUI.BeginDisabledGroup(!hasSave);
        if (GUILayout.Button("Clear Saved Run"))
        {
            PlayerPrefs.DeleteKey(CampaignManager.SaveKey);
            PlayerPrefs.Save();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawToggleAndInt(Object dirty, string label, ref bool overrideFlag, ref int value)
    {
        EditorGUILayout.BeginHorizontal();
        bool nextOverride = EditorGUILayout.ToggleLeft(label, overrideFlag, GUILayout.Width(160));
        EditorGUI.BeginDisabledGroup(!nextOverride);
        int nextValue = EditorGUILayout.IntField(value);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        if (nextOverride != overrideFlag || nextValue != value)
        {
            overrideFlag = nextOverride;
            value = nextValue;
            EditorUtility.SetDirty(dirty);
        }
    }

    private static void DrawToggleAndObject<T>(Object dirty, string label, ref bool overrideFlag, ref T value) where T : Object
    {
        EditorGUILayout.BeginHorizontal();
        bool nextOverride = EditorGUILayout.ToggleLeft(label, overrideFlag, GUILayout.Width(160));
        EditorGUI.BeginDisabledGroup(!nextOverride);
        T nextValue = (T)EditorGUILayout.ObjectField(value, typeof(T), false);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        if (nextOverride != overrideFlag || nextValue != value)
        {
            overrideFlag = nextOverride;
            value = nextValue;
            EditorUtility.SetDirty(dirty);
        }
    }
}
