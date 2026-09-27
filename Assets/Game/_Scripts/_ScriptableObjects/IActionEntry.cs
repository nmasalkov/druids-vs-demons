/// <summary>
/// Common shape for roll-result entries (NukeEntry, SpellEntry). Lets the generic
/// <see cref="ActionState"/> play loop work without knowing the concrete entry type.
/// </summary>
public interface IActionEntry
{
    /// <summary>The action SO this entry refers to (NukeSO, SpellSO, …).</summary>
    ActionSO Source { get; }

    /// <summary>Match count / level for the entry (1, 2, or 3).</summary>
    int Level { get; }
}
