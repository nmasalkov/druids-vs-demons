using UnityEngine;

/// <summary>
/// Unlocks a new creature into the player's collection. Claiming appends creature.id to
/// RunState.gatheredCreatureIds (not yet fielded/selectable — see docs/Rewards.md). Unique:
/// once gathered, this reward is excluded from future draws.
/// </summary>
[CreateAssetMenu(fileName = "CreatureReward", menuName = "Game/Campaign/Rewards/Creature Reward")]
public class CreatureRewardSO : RewardSO
{
    public CreatureSO creature;

    public override void Claim(RunState run) => run.gatheredCreatureIds.Add(creature.id);
    public override bool IsOwned(RunState run) => run.gatheredCreatureIds.Contains(creature.id);
    protected override Sprite FallbackIcon => creature.cardSprite;
}
