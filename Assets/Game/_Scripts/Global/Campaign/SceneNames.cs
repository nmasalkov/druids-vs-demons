/// <summary>
/// Central place for scene name constants (matches Unity's own SceneManager terminology — a
/// scene's "name" is its asset filename without extension, distinct from its full asset path).
/// Add the future MapScene's name here too once it exists, rather than inlining scene name
/// strings at each call site. See docs/Encounters.md.
/// </summary>
public static class SceneNames
{
    public const string BattleScene = "BattleScene";
}
