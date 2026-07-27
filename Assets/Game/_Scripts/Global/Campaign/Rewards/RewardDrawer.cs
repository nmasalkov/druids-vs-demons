using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Draws the 3 reward cards a RewardEncounter offers. See docs/Rewards.md for the full
/// pooling/fallback rule this implements.
/// </summary>
public static class RewardDrawer
{
    public static List<RewardSO> DrawThree(RewardListSO catalog, RunState run)
    {
        var available = catalog.allRewards.Where(r => !(r.unique && r.IsOwned(run))).ToList();

        var hpOrEnergy = available.Where(r => r is HpBoostRewardSO || r is BonusEnergyRewardSO).ToList();
        var creatures = available.OfType<CreatureRewardSO>().Cast<RewardSO>().ToList();
        var boosts = available.OfType<BoostSO>().Cast<RewardSO>().ToList();

        var used = new HashSet<RewardSO>();
        return new List<RewardSO>
        {
            PickFromChain(used, hpOrEnergy),
            PickFromChain(used, creatures, boosts, hpOrEnergy),
            PickFromChain(used, boosts, creatures, hpOrEnergy),
        };
    }

    /// <summary>Tries each pool in priority order, falling through to the next when the current one has no unclaimed candidates left.</summary>
    private static RewardSO PickFromChain(HashSet<RewardSO> used, params List<RewardSO>[] poolsInPriorityOrder)
    {
        foreach (var pool in poolsInPriorityOrder)
        {
            var pick = PickFrom(pool, used);
            if (pick != null) return pick;
        }
        return null; // unreachable: hpOrEnergy is always a fallback and is never fully excludable (not unique)
    }

    private static RewardSO PickFrom(List<RewardSO> pool, HashSet<RewardSO> used)
    {
        var candidates = pool.Where(r => !r.unique || !used.Contains(r)).ToList();
        if (candidates.Count == 0) return null;

        var pick = candidates[Random.Range(0, candidates.Count)];
        if (pick.unique) used.Add(pick);
        return pick;
    }
}
