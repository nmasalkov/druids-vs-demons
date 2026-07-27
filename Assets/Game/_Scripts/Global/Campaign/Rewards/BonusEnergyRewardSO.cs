using UnityEngine;

/// <summary>
/// One-time reroll energy grant, applied and forgotten immediately on claim (not tracked in any
/// RunState list). Purely additive, uncapped by RunState.energyCapacity — same as
/// RewardPickSO's guaranteed energy reward. See docs/Rewards.md and docs/Energy.md.
/// </summary>
[CreateAssetMenu(fileName = "BonusEnergyReward", menuName = "Game/Campaign/Rewards/Bonus Energy Reward")]
public class BonusEnergyRewardSO : RewardSO
{
    public int bonusEnergy = 50;

    public override void Claim(RunState run) => run.currentEnergy += bonusEnergy;

    public override bool IsOwned(RunState run) => false;
}
