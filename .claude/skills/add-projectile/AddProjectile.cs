using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using _Scripts.Creatures;

/// <summary>
/// Builds a SimpleProjectile prefab in Assets/Game/_Prefabs/_Projectiles/_{name}/ from three effect
/// prefabs (missile / muzzle / impact), each COPIED into that folder so the projectile never
/// references vendor assets. See SKILL.md next to this file.
/// </summary>
public class AddProjectile
{
    const string ProjectilesRoot = "Assets/Game/_Prefabs/_Projectiles";
    const string Template = "Assets/Game/_Prefabs/_Projectiles/_FireBall/Fireball.prefab";

    public static string Execute(string name, string missilePath, string muzzlePath, string impactPath, bool dryRun)
    {
        var sb = new StringBuilder();
        foreach (var p in new[] { missilePath, muzzlePath, impactPath })
        {
            if (string.IsNullOrEmpty(p)) continue;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(p) == null) return $"ERROR: not a prefab: {p}";
        }

        string folder = $"{ProjectilesRoot}/_{name}";
        string projectilePath = $"{folder}/{name}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(projectilePath) != null) return $"ERROR: {projectilePath} already exists";

        sb.AppendLine($"folder:     {folder}");
        sb.AppendLine($"projectile: {projectilePath} (copied from {Template})");
        if (dryRun)
        {
            foreach (var p in new[] { missilePath, muzzlePath, impactPath })
                sb.AppendLine(string.IsNullOrEmpty(p) ? "  (none)" : $"  copy {p} -> {folder}/{Path.GetFileName(p)}");
            sb.AppendLine("dry run — nothing written");
            return sb.ToString();
        }

        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(ProjectilesRoot, "_" + name);

        GameObject missile = CopyInto(missilePath, folder, sb);
        GameObject muzzle = CopyInto(muzzlePath, folder, sb);
        GameObject impact = CopyInto(impactPath, folder, sb);

        if (!AssetDatabase.CopyAsset(Template, projectilePath)) { sb.AppendLine($"ERROR: copy {Template} failed"); return sb.ToString(); }
        var root = PrefabUtility.LoadPrefabContents(projectilePath);
        root.name = name;
        var projectile = root.GetComponent<SimpleProjectile>();
        projectile.projectileParticle = missile;
        projectile.muzzleParticle = muzzle;
        projectile.impactParticle = impact;
        projectile.trailParticles = new GameObject[0];
        PrefabUtility.SaveAsPrefabAsset(root, projectilePath);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();

        // Proof: no vendor GUIDs referenced from the new projectile.
        var deps = AssetDatabase.GetDependencies(projectilePath, false)
            .Where(d => d.EndsWith(".prefab") && d != projectilePath).ToArray();
        foreach (var d in deps) sb.AppendLine($"  depends on {d}{(d.StartsWith(folder) ? "" : "   <-- OUTSIDE FOLDER")}");
        sb.AppendLine($"created {projectilePath}");
        return sb.ToString();
    }

    static GameObject CopyInto(string source, string folder, StringBuilder sb)
    {
        if (string.IsNullOrEmpty(source)) return null;
        string dest = $"{folder}/{Path.GetFileName(source)}";
        if (!AssetDatabase.CopyAsset(source, dest)) { sb.AppendLine($"  FAILED copy {source}"); return null; }
        sb.AppendLine($"  copied {source} -> {dest}");
        return AssetDatabase.LoadAssetAtPath<GameObject>(dest);
    }
}
