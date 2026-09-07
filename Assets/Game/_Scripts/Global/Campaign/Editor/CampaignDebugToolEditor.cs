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
        EditorGUILayout.LabelField("External Save", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "When checked, the pasted JSON is parsed as a whole RunState (through the same " +
            "validation a real save goes through) and becomes CurrentRun for this session — " +
            "highest precedence, overrides Debug Profile and every granular field below. Never saved.",
            MessageType.Info);
        DrawToggleAndTextArea(tool, "Use External Save", ref tool.useExternalSave, ref tool.externalSaveJson);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Debug Profile", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "When checked, every RunState field is set from the profile asset wholesale — the " +
            "granular overrides below are ignored entirely. Never saved.", MessageType.Info);
        DrawToggleAndObject(tool, "Use Debug Profile", ref tool.useDebugProfile, ref tool.debugProfile);

        EditorGUILayout.Space(4);
        EditorGUI.BeginDisabledGroup(tool.debugProfile == null);
        if (GUILayout.Button("Generate Save JSON from Profile"))
        {
            tool.externalSaveJson = JsonUtility.ToJson(CampaignDebugTool.BuildRunStateFromProfile(tool.debugProfile), true);
            EditorUtility.SetDirty(tool);
        }
        EditorGUI.EndDisabledGroup();
        if (tool.debugProfile == null)
            EditorGUILayout.HelpBox("Assign a Debug Profile above to generate save JSON from it.", MessageType.None);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Run State Overrides", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Checked fields override CampaignStateManager's RunState in Awake(), before anything " +
            "reads it. Never saved — set overrides, press Play, test.", MessageType.Info);

        DrawToggleAndInt(tool, "Max HP", ref tool.overrideMaxHp, ref tool.maxHp);
        DrawToggleAndInt(tool, "Energy Capacity", ref tool.overrideEnergyCapacity, ref tool.energyCapacity);
        DrawToggleAndInt(tool, "Current Energy", ref tool.overrideCurrentEnergy, ref tool.currentEnergy);

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

        EditorGUILayout.Space(6);
        serializedObject.Update();
        DrawToggleAndList(serializedObject, "Status Rewards", "overrideStatusRewards", "statusRewards");
        DrawToggleAndList(serializedObject, "Boost Rewards", "overrideBoostRewards", "boostRewards");
        DrawToggleAndList(serializedObject, "Gathered Creatures", "overrideGatheredCreatures", "gatheredCreatures");
        DrawToggleAndList(serializedObject, "Gathered Nukes", "overrideGatheredNukes", "gatheredNukes");
        DrawToggleAndList(serializedObject, "Gathered Spells", "overrideGatheredSpells", "gatheredSpells");
        serializedObject.ApplyModifiedProperties();

        DrawSavedRunSection();
    }

    /// <summary>
    /// Read-only view of whatever CampaignStateManager.Save() last wrote via SaveStorage.Backend —
    /// there's no built-in Editor window for browsing PlayerPrefs (the default backend), so this
    /// is the debug affordance for it. Independent of the override fields above and of Play mode:
    /// reads storage directly, works in Edit mode too.
    /// </summary>
    private void DrawSavedRunSection()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Saved Run", EditorStyles.boldLabel);

        bool hasSave = SaveStorage.Backend.Exists();
        if (!hasSave)
        {
            EditorGUILayout.HelpBox(
                "No saved run yet — nothing has called CampaignStateManager.Save() (this debug tool " +
                "never does). A fresh RunState is used every time you press Play until something does.",
                MessageType.None);
        }
        else
        {
            string raw = SaveStorage.Backend.Read();
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
            SaveStorage.Backend.Delete();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        bool canExport = hasSave || (Application.isPlaying && CampaignStateManager.Instance != null);
        EditorGUI.BeginDisabledGroup(!canExport);
        if (GUILayout.Button("Export to Debug Profile")) ExportCurrentRunToProfile();
        EditorGUI.EndDisabledGroup();
        if (!canExport)
            EditorGUILayout.HelpBox("Nothing to export yet — no saved run and not currently in Play mode.", MessageType.None);
    }

    private const string ProfileExportFolder = "Assets/Game/_ScriptableObjects/Campaign/Profiles";

    /// <summary>
    /// Captures a whole RunState as a new CampaignProfileSO asset — the reverse of "Use Debug
    /// Profile"/"Generate Save JSON from Profile" above. Prefers the live CurrentRun while in Play
    /// mode (the exact state currently being tested, ahead of whatever the next real Save() trigger
    /// would persist); otherwise falls back to whatever's actually on disk via SaveStorage. Either
    /// way the result lands in the "Use Debug Profile" slot's Create menu location
    /// (Game/Campaign/Campaign Profile) so it can be dragged straight back in later. See
    /// docs/Campaign.md.
    /// </summary>
    private void ExportCurrentRunToProfile()
    {
        RunState run;
        GameCatalog catalog;
        RewardListSO rewardList;

        if (Application.isPlaying && CampaignStateManager.Instance != null)
        {
            run = CampaignStateManager.Instance.CurrentRun;
            catalog = CampaignStateManager.Instance.Catalog;
            rewardList = CampaignStateManager.Instance.RewardList;
        }
        else
        {
            run = JsonUtility.FromJson<RunState>(SaveStorage.Backend.Read());
            catalog = FindProjectAsset<GameCatalog>();
            rewardList = FindProjectAsset<RewardListSO>();
        }

        if (catalog == null || rewardList == null)
        {
            Debug.LogError("CampaignDebugTool: couldn't find a GameCatalog/RewardListSO asset in the project to resolve ids against — export aborted.");
            return;
        }

        var profile = CampaignDebugTool.BuildProfileFromRunState(run, catalog, rewardList);

        if (!AssetDatabase.IsValidFolder(ProfileExportFolder))
            AssetDatabase.CreateFolder("Assets/Game/_ScriptableObjects/Campaign", "Profiles");

        string path = EditorUtility.SaveFilePanelInProject(
            "Export to Debug Profile",
            $"RunState_Encounter{run.currentEncounterIndex + 1}",
            "asset",
            "Choose where to save the exported Campaign Profile.",
            ProfileExportFolder);
        if (string.IsNullOrEmpty(path))
        {
            DestroyImmediate(profile);
            return;
        }

        AssetDatabase.CreateAsset(profile, path);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(profile);
        Selection.activeObject = profile;
    }

    private static T FindProjectAsset<T>() where T : Object
    {
        var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
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

    /// <summary>
    /// List-shaped counterpart to DrawToggleAndObject — a single SO ref doesn't fit a
    /// List&lt;T&gt; override, so this reads/writes via SerializedProperty instead of ref fields.
    /// </summary>
    private static void DrawToggleAndList(SerializedObject so, string label, string overridePropName, string listPropName)
    {
        var overrideProp = so.FindProperty(overridePropName);
        overrideProp.boolValue = EditorGUILayout.ToggleLeft(label, overrideProp.boolValue);
        EditorGUI.BeginDisabledGroup(!overrideProp.boolValue);
        EditorGUILayout.PropertyField(so.FindProperty(listPropName), true);
        EditorGUI.EndDisabledGroup();
    }

    private static void DrawToggleAndTextArea(Object dirty, string label, ref bool overrideFlag, ref string value)
    {
        bool nextOverride = EditorGUILayout.ToggleLeft(label, overrideFlag);
        EditorGUI.BeginDisabledGroup(!nextOverride);
        string nextValue = EditorGUILayout.TextArea(value, GUILayout.Height(140));
        EditorGUI.EndDisabledGroup();

        if (nextOverride != overrideFlag || nextValue != value)
        {
            overrideFlag = nextOverride;
            value = nextValue;
            EditorUtility.SetDirty(dirty);
        }
    }
}
