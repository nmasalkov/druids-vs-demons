using System.Collections.Generic;

/// <summary>
/// The one place that reads claimed BoostSO rewards (RunState.boostRewardIds). Every query takes
/// isPlayer and is a no-op for the enemy — rewards are player-only. Resolvers reach it through
/// ActionResolver.Boosted / IsImproved; creature combat through ApplyCreatureBonus. See docs/Rewards.md.
/// </summary>
public static class RewardBonuses
{
    /// <summary>baseValue scaled by every claimed ActionStatBoostSO for this action.</summary>
    public static float ApplyActionBonus(ActionSO action, float baseValue, bool isPlayer)
        => baseValue * (1f + ActionBonusFraction(action, isPlayer));

    /// <summary>Summed ActionStatBoostSO.percentage for this action (0 for the enemy) — for
    /// actions that scale only part of a value (Battle Cry's bonus part).</summary>
    public static float ActionBonusFraction(ActionSO action, bool isPlayer)
    {
        float bonus = 0f;
        foreach (var boost in ClaimedBoosts<ActionStatBoostSO>(isPlayer))
        {
            if (boost.action == action) bonus += boost.percentage;
        }
        return bonus;
    }

    /// <summary>baseValue scaled by every claimed CreatureClassBoostSO matching this creature's class.</summary>
    public static float ApplyCreatureBonus(CreatureSO data, float baseValue, bool isPlayer)
    {
        float bonus = 0f;
        foreach (var boost in ClaimedBoosts<CreatureClassBoostSO>(isPlayer))
        {
            if (boost.Matches(data)) bonus += boost.percentage;
        }
        return baseValue * (1f + bonus);
    }

    /// <summary>True (with the SO) if the player owns an improvement of type T.</summary>
    public static bool TryGetImprovement<T>(bool isPlayer, out T improvement) where T : ActionImprovementSO
    {
        foreach (var boost in ClaimedBoosts<T>(isPlayer))
        {
            improvement = boost;
            return true;
        }
        improvement = null;
        return false;
    }

    private static IEnumerable<T> ClaimedBoosts<T>(bool isPlayer) where T : BoostSO
    {
        if (!isPlayer) yield break;
        foreach (var id in CampaignStateManager.Instance.CurrentRun.boostRewardIds)
        {
            if (G.RewardList.Find(id) is T boost) yield return boost;
        }
    }
}
