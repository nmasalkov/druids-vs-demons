using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class AssetDemoSceneLoader : EditorWindow
{
    private string[] _assetFolders;
    private int _selectedIndex;
    private Vector2 _scrollPos;
    private string[] _foundScenes;

    [MenuItem("Tools/Asset Demo Scene Loader")]
    public static void ShowWindow()
    {
        GetWindow<AssetDemoSceneLoader>("Demo Scene Loader");
    }

    private void OnEnable()
    {
        RefreshFolders();
    }

    private void RefreshFolders()
    {
        var rootDirs = Directory.GetDirectories(Application.dataPath)
            .Select(d => new DirectoryInfo(d))
            .Where(d => d.Name != "Game")
            .OrderBy(d => d.Name)
            .Select(d => d.Name)
            .ToArray();

        _assetFolders = rootDirs;
        _selectedIndex = 0;
        _foundScenes = null;
    }

    private void OnGUI()
    {
        if (_assetFolders == null || _assetFolders.Length == 0)
        {
            EditorGUILayout.HelpBox("No asset folders found (excluding Game).", MessageType.Info);
            if (GUILayout.Button("Refresh")) RefreshFolders();
            return;
        }

        EditorGUILayout.Space(5);
        _selectedIndex = EditorGUILayout.Popup("Asset Folder", _selectedIndex, _assetFolders);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Find Scenes"))
        {
            var folder = "Assets/" + _assetFolders[_selectedIndex];
            _foundScenes = AssetDatabase.FindAssets("t:Scene", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .ToArray();
        }

        if (GUILayout.Button("Refresh Folders")) RefreshFolders();
        EditorGUILayout.EndHorizontal();

        if (_foundScenes == null) return;

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField($"Found {_foundScenes.Length} scene(s)", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (_foundScenes.Length > 0 && GUILayout.Button("Add All to Build Settings"))
        {
            AddScenesToBuild(_foundScenes);
        }
        if (_foundScenes.Length > 0 && GUILayout.Button("Remove All from Build Settings"))
        {
            RemoveScenesFromBuild(_foundScenes);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        var buildScenePaths = new HashSet<string>(
            EditorBuildSettings.scenes.Select(s => s.path));

        foreach (var scene in _foundScenes)
        {
            EditorGUILayout.BeginHorizontal();

            bool inBuild = buildScenePaths.Contains(scene);
            var icon = inBuild ? "\u2713 " : "   ";
            EditorGUILayout.LabelField(icon + scene);

            if (GUILayout.Button("Open", GUILayout.Width(60)))
            {
                EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            }
            if (!inBuild && GUILayout.Button("+Build", GUILayout.Width(55)))
            {
                AddScenesToBuild(new[] { scene });
            }
            if (inBuild && GUILayout.Button("-Build", GUILayout.Width(55)))
            {
                RemoveScenesFromBuild(new[] { scene });
            }

            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private static void AddScenesToBuild(string[] scenePaths)
    {
        var existing = EditorBuildSettings.scenes.ToList();
        var existingPaths = new HashSet<string>(existing.Select(s => s.path));
        int added = 0;

        foreach (var path in scenePaths)
        {
            if (existingPaths.Contains(path)) continue;
            existing.Add(new EditorBuildSettingsScene(path, true));
            added++;
        }

        EditorBuildSettings.scenes = existing.ToArray();
        Debug.Log($"[Demo Scene Loader] Added {added} scene(s) to Build Settings.");
    }

    private static void RemoveScenesFromBuild(string[] scenePaths)
    {
        var toRemove = new HashSet<string>(scenePaths);
        var filtered = EditorBuildSettings.scenes
            .Where(s => !toRemove.Contains(s.path))
            .ToArray();

        int removed = EditorBuildSettings.scenes.Length - filtered.Length;
        EditorBuildSettings.scenes = filtered;
        Debug.Log($"[Demo Scene Loader] Removed {removed} scene(s) from Build Settings.");
    }
}
