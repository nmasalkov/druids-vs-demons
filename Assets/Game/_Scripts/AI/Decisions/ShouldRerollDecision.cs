using System.Collections.Generic;
using UnityEngine;

public class ShouldRerollDecision
{
    public RerollChoice Decide(IReadOnlyList<ActionSO> currentSlots, ActionSO desiredAction,
        int rerollsUsedSoFar, int rerollBudget)
    {
        var nonMatching = NonMatchingIndices(currentSlots, desiredAction);
        if (rerollsUsedSoFar >= rerollBudget || nonMatching.Count == 0)
            return new RerollChoice(false, -1); // physically nothing left to do — no degrade

        int slotIndex = nonMatching[Random.Range(0, nonMatching.Count)];
        var fight = CampaignStateManager.Instance.CurrentFight.enemyData;
        bool shouldReroll = AIDegrade.Resolve(new[] { true, false }, fight.stupidityChance, fight.criticalFailureChance);
        return new RerollChoice(shouldReroll, slotIndex);
    }

    private static List<int> NonMatchingIndices(IReadOnlyList<ActionSO> currentSlots, ActionSO desiredAction)
    {
        var indices = new List<int>();
        for (int i = 0; i < currentSlots.Count; i++)
            if (currentSlots[i] != desiredAction) indices.Add(i);
        return indices;
    }
}
