using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Deliberately NOT stupidity-affected — a fixed number, not a ranked choice.
public class RerollBudgetDecision
{
    // The spec's literal "2" — a fixed constant, not per-fight tunable. EnemyData.rerollsAmount
    // (the fight-wide pool) is what actually regulates the AI over the course of a fight.
    private const int BaseMaxRerollsPerTurn = 2;

    public int Decide(IReadOnlyList<ActionSO> initialLandedSlots, ActionSO desiredAction)
    {
        int matches = initialLandedSlots.Count(slot => slot == desiredAction);
        int baseBudget = matches switch
        {
            0 => BaseMaxRerollsPerTurn,
            1 => 1,
            _ => BaseMaxRerollsPerTurn, // pair (2); 3 never reached — triples auto-finish
        };

        int desiredBudget = baseBudget + HpBonusRerolls();
        return Mathf.Min(desiredBudget, AIController.RerollsRemaining);
    }

    private static int HpBonusRerolls()
    {
        if (AIController.EnemyHeroBelow(0.15f) || AIController.PlayerHeroBelow(0.15f)) return 2;
        if (AIController.EnemyHeroBelow(0.25f) || AIController.PlayerHeroBelow(0.25f)) return 1;
        return 0;
    }
}
