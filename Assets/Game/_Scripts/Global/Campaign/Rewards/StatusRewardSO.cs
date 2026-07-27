/// <summary>
/// Abstract category for rewards that stack into a permanent, run-scoped modifier tracked in
/// RunState.statusRewardIds (currently only HpBoostRewardSO). Never unique-filtered by
/// RewardDrawer — IsOwned is meaningless for a stacking, non-unique reward.
/// </summary>
public abstract class StatusRewardSO : RewardSO
{
    public override bool IsOwned(RunState run) => false;
}
