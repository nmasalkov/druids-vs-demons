using UnityEngine;

/// <summary>
/// Adds bonusHp to the player's max HP for the rest of the run. Claiming appends this SO's id
/// to RunState.statusRewardIds; CampaignStateManager.GetMaxHpBonus() sums bonusHp across every
/// claimed copy (duplicates allowed and expected). See docs/Rewards.md.
/// </summary>
[CreateAssetMenu(fileName = "HpBoostReward", menuName = "Game/Campaign/Rewards/Hp Boost Reward")]
public class HpBoostRewardSO : StatusRewardSO
{
    public int bonusHp = 15;

    public override void Claim(RunState run) => run.statusRewardIds.Add(id);
}
