using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using _Scripts.Creatures;

// Run via Coplay execute_script (see SKILL.md next to this file). Not part of the Unity project —
// it lives outside Assets/ on purpose, so it's only ever compiled on demand.
//
// Finds every prefab under Assets/Game whose root has a SimpleProjectile, collects the prefabs it
// renders (the root itself + projectileParticle / muzzleParticle / impactParticle), and puts every
// Renderer and SortingGroup inside them on one sorting layer.
public class SetProjectileLayerOrder
{
    const string GameRoot = "Assets/Game";

    /// <param name="sortingLayer">Target sorting layer name, e.g. "Shield".</param>
    /// <param name="orderMode">"keep" (leave each renderer's order), "add" (order += orderValue),
    /// or "set" (order = orderValue for every renderer).</param>
    /// <param name="orderValue">Used by "add"/"set"; ignored by "keep".</param>
    /// <param name="onlyPrefab">Optional: limit to SimpleProjectile prefabs whose name or path
    /// contains this text (case-insensitive). Empty = all.</param>
    /// <param name="dryRun">true = report what would change, write nothing.</param>
    public static string Execute(string sortingLayer, string orderMode, int orderValue, string onlyPrefab, bool dryRun)
    {
        var sb = new StringBuilder();
        if (!SortingLayer.layers.Any(l => l.name == sortingLayer))
            return $"ERROR: sorting layer '{sortingLayer}' doesn't exist. Layers: {string.Join(", ", SortingLayer.layers.Select(l => l.name))}";
        if (orderMode != "keep" && orderMode != "add" && orderMode != "set")
            return $"ERROR: orderMode must be keep/add/set, got '{orderMode}'";

        var targets = new SortedDictionary<string, List<string>>();
        foreach (var projectilePath in FindProjectilePrefabs(onlyPrefab))
        {
            AddTarget(targets, projectilePath, Path.GetFileNameWithoutExtension(projectilePath));
            foreach (var field in new[] { "projectileParticle", "muzzleParticle", "impactParticle" })
            {
                var refPath = ResolveField(projectilePath, field, dryRun, sb);
                if (refPath != null) AddTarget(targets, refPath, $"{Path.GetFileNameWithoutExtension(projectilePath)}.{field}");
            }
        }

        int changedPrefabs = 0, changedRenderers = 0;
        foreach (var kv in targets)
        {
            int n = ApplyToPrefab(kv.Key, kv.Value, sortingLayer, orderMode, orderValue, dryRun, sb);
            if (n > 0) { changedPrefabs++; changedRenderers += n; }
        }
        if (!dryRun) AssetDatabase.SaveAssets();

        sb.Insert(0, $"{(dryRun ? "DRY RUN — " : "")}layer={sortingLayer} orderMode={orderMode} orderValue={orderValue} " +
                     $"prefabsScanned={targets.Count} prefabsChanged={changedPrefabs} renderersChanged={changedRenderers}\n");
        return sb.ToString();
    }

    static IEnumerable<string> FindProjectilePrefabs(string onlyPrefab)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { GameRoot }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(onlyPrefab) && path.IndexOf(onlyPrefab, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go.GetComponent<SimpleProjectile>() != null) yield return path;
        }
    }

    /// <summary>Returns the path of the prefab a SimpleProjectile field points at. A prefab outside
    /// Assets/Game (vendored, e.g. Epic Toon FX) is never edited: it's copied next to the projectile
    /// prefab and the field is repointed at the copy — the same pattern the existing per-projectile
    /// folders already follow.</summary>
    static string ResolveField(string projectilePath, string field, bool dryRun, StringBuilder sb)
    {
        var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(projectilePath).GetComponent<SimpleProjectile>();
        var so = new SerializedObject(projectile);
        var prop = so.FindProperty(field);
        var referenced = prop.objectReferenceValue as GameObject;
        if (referenced == null) return null;

        var refPath = AssetDatabase.GetAssetPath(referenced);
        if (refPath.StartsWith(GameRoot + "/")) return refPath;

        var copyPath = AssetDatabase.GenerateUniqueAssetPath($"{Path.GetDirectoryName(projectilePath).Replace('\\', '/')}/{Path.GetFileName(refPath)}");
        sb.AppendLine($"VENDOR {Path.GetFileName(projectilePath)}.{field}: {refPath} -> copy to {copyPath}{(dryRun ? " (dry run)" : "")}");
        if (dryRun) return refPath; // report the vendor prefab's renderers without touching it

        if (!AssetDatabase.CopyAsset(refPath, copyPath))
        {
            sb.AppendLine($"  ERROR: copy failed, leaving {field} untouched");
            return null;
        }
        prop.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(copyPath);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(projectile);
        AssetDatabase.SaveAssets();
        return copyPath;
    }

    static int ApplyToPrefab(string path, List<string> usedBy, string layer, string mode, int value, bool dryRun, StringBuilder sb)
    {
        bool vendor = !path.StartsWith(GameRoot + "/");
        var root = PrefabUtility.LoadPrefabContents(path);
        var lines = new List<string>();

        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            int newOrder = NewOrder(r.sortingOrder, mode, value);
            if (r.sortingLayerName == layer && r.sortingOrder == newOrder) continue;
            lines.Add($"  {r.GetType().Name} '{RelPath(r.transform, root.transform)}': {r.sortingLayerName}/{r.sortingOrder} -> {layer}/{newOrder}");
            r.sortingLayerName = layer;
            r.sortingOrder = newOrder;
        }
        foreach (var g in root.GetComponentsInChildren<SortingGroup>(true))
        {
            int newOrder = NewOrder(g.sortingOrder, mode, value);
            if (g.sortingLayerName == layer && g.sortingOrder == newOrder) continue;
            lines.Add($"  SortingGroup '{RelPath(g.transform, root.transform)}': {g.sortingLayerName}/{g.sortingOrder} -> {layer}/{newOrder}");
            g.sortingLayerName = layer;
            g.sortingOrder = newOrder;
        }

        sb.AppendLine($"{(lines.Count == 0 ? "ok     " : "change ")}{path}  [{string.Join(", ", usedBy)}]{(vendor ? " (vendor, not saved)" : "")}");
        lines.ForEach(l => sb.AppendLine(l));

        if (!dryRun && !vendor && lines.Count > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return lines.Count;
    }

    static int NewOrder(int current, string mode, int value) =>
        mode == "set" ? value : mode == "add" ? current + value : current;

    static string RelPath(Transform t, Transform root)
    {
        var rel = AnimationUtility.CalculateTransformPath(t, root);
        return rel == "" ? "<root>" : rel;
    }

    static void AddTarget(SortedDictionary<string, List<string>> d, string path, string usedBy)
    {
        if (!d.TryGetValue(path, out var list)) d[path] = list = new List<string>();
        list.Add(usedBy);
    }
}
