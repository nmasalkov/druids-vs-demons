using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared stupidity/critical-failure resolver for every stupidity-affected AI decision (see
/// docs/AI.md). rankedCandidates must be ordered best-to-worst. NO-STUPID decisions bypass this
/// entirely and just return their forced/top pick directly.
/// </summary>
public static class AIDegrade
{
    public static T Resolve<T>(IReadOnlyList<T> rankedCandidates, int stupidityChance, int criticalFailureChance)
    {
        int index = 0;
        while (true)
        {
            if (!AIController.RollForProbability(stupidityChance))
                return rankedCandidates[index];

            if (AIController.RollForProbability(criticalFailureChance))
                return rankedCandidates[Random.Range(0, rankedCandidates.Count)];

            index++;
            if (index >= rankedCandidates.Count)
                return rankedCandidates[rankedCandidates.Count - 1];
        }
    }
}
