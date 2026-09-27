public class SpellState : ActionState
{
    protected override void OnEnter() =>
        PlayEntries(RollStateManager.Instance.SpellEntries,
                    SpellStateManager.Instance.PauseBetweenSpells,
                    nameof(SpellState));
    /// <summary>Instant resolution path (no animation, no waits). See rule 7.</summary>
    public static void ResolveSpellsInstant() =>
        ResolveInstant(RollStateManager.Instance.SpellEntries);
}
