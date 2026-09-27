using UnityEngine;

/// <summary>
/// Abstract base for a +percentage stat boost to a specific action (creature/nuke/spell).
/// Claiming appends this SO's id to RunState.boostRewardIds; RewardBonuses.ApplyBonuses reads
/// that list at resolve time to find every boost matching a given ActionSO. See docs/Rewards.md.
/// </summary>
public abstract class BoostSO : RewardSO
{
    public ActionSO action;
    [Range(0f, 1f)] public float percentage = 0.2f;

    public override void Claim(RunState run) => run.boostRewardIds.Add(id);
    public override bool IsOwned(RunState run) => run.boostRewardIds.Contains(id);
    protected override Sprite FallbackIcon => action.cardSprite;
}
