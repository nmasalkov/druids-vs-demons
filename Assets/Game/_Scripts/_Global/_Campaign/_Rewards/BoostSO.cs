/// <summary>
/// Abstract base for a unique, run-long upgrade — a stat boost (<see cref="CreatureClassBoostSO"/>,
/// <see cref="ActionStatBoostSO"/>) or a mechanic change (<see cref="ActionImprovementSO"/>).
/// Claiming appends this SO's id to RunState.boostRewardIds; RewardBonuses reads that list at
/// resolve time. Boosts only ever apply to the player side. See docs/Rewards.md.
/// </summary>
public abstract class BoostSO : RewardSO
{
    public override void Claim(RunState run) => run.boostRewardIds.Add(id);
    public override bool IsOwned(RunState run) => run.boostRewardIds.Contains(id);
    public override bool ShowsBoostOverlay => true;
}
