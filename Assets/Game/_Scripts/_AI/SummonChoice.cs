public readonly struct SummonChoice
{
    public readonly bool ShouldSummon;

    /// <summary>The AI's own shocked creature type to go repair, if this is a repair-driven summon.
    /// Null for an ordinary (no-signal or board-strength) summon, or when ShouldSummon is false.</summary>
    public readonly CreatureSO RepairTarget;

    public SummonChoice(bool shouldSummon, CreatureSO repairTarget)
    {
        ShouldSummon = shouldSummon;
        RepairTarget = repairTarget;
    }
}
