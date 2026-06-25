public class NukeState : ActionState
{
    protected override void OnEnter() =>
        PlayEntries(RollStateManager.Instance.NukeEntries,
                    NukeStateManager.Instance.PauseBetweenNukes,
                    nameof(NukeState));
    /// <summary>Instant resolution path (no animation, no waits). See rule 7.</summary>
    public static void ResolveNukesInstant() =>
        ResolveInstant(RollStateManager.Instance.NukeEntries);
}
