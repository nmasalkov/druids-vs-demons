using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Draws the 3 reward cards a RewardEncounter offers. See docs/Rewards.md for the full
/// pooling/fallback rule this implements:
/// slot 1 = Vitality/Energy, slot 2 = creature (else the other Vitality/Energy card),
/// slot 3 = boost (else creature, else any Vitality/Energy card not already shown).
/// </summary>
public static class RewardDrawer
{
    public static List<RewardSO> DrawThree(RewardListSO catalog, RunState run)
    {
        var available = catalog.allRewards.Where(r => !(r.unique && r.IsOwned(run))).ToList();

        var hpOrEnergy = available.Where(r => r is HpBoostRewardSO || r is BonusEnergyRewardSO).ToList();
        var creatures = available.OfType<CreatureRewardSO>().Cast<RewardSO>().ToList();
        var boosts = available.OfType<BoostSO>().Cast<RewardSO>().ToList();

        // Every card drawn so far — no reward (unique or not) is shown twice in one draw while
        // another candidate exists.
        var used = new HashSet<RewardSO>();
        return new List<RewardSO>
        {
            PickFromChain(used, hpOrEnergy),
            PickFromChain(used, creatures, hpOrEnergy),
            PickFromChain(used, boosts, creatures, hpOrEnergy),
        };
    }

    /// <summary>Tries each pool in priority order, falling through to the next when the current one
    /// has no candidates left that aren't already in this draw. Last resort: repeat a non-unique
    /// card from the final pool.</summary>
    private static RewardSO PickFromChain(HashSet<RewardSO> used, params List<RewardSO>[] poolsInPriorityOrder)
    {
        foreach (var pool in poolsInPriorityOrder)
        {
            var pick = PickFrom(pool, used);
            if (pick != null) return pick;
        }
        return PickRepeat(poolsInPriorityOrder[^1]);
    }

    private static RewardSO PickFrom(List<RewardSO> pool, HashSet<RewardSO> used)
    {
        var candidates = pool.Where(r => !used.Contains(r)).ToList();
        if (candidates.Count == 0) return null;

        var pick = candidates[Random.Range(0, candidates.Count)];
        used.Add(pick);
        return pick;
    }

    /// <summary>Only reached once every pool is exhausted for this draw — hpOrEnergy is never
    /// unique, so repeating one of its cards is always valid.</summary>
    private static RewardSO PickRepeat(List<RewardSO> pool) => pool[Random.Range(0, pool.Count)];
}
