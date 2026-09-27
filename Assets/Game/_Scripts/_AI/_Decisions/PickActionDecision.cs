using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PickActionDecision
{
    /// <summary>
    /// excludeAction: the nuke/spell that just landed 3-of-a-kind, if this is the bonus roll granted
    /// by a triple (see docs/AI.md) — the AI never goes for the same action again on that bonus
    /// roll. Null on an ordinary roll. Unlike creatures, nukes/spells have no "already owned"
    /// concept, so this exclusion has to be explicit here.
    /// </summary>
    public ActionSO Decide(ActionSO excludeAction = null)
    {
        var candidates = new List<ActionSO>
        {
            G.EnemyNukes.nukeA, G.EnemyNukes.nukeB, G.EnemyNukes.nukeC,
            G.EnemySpells.spellA, G.EnemySpells.spellB, G.EnemySpells.spellC,
        };
        candidates.Remove(excludeAction);
        var ranked = RankByScoreDescending(candidates);
        var fight = CampaignStateManager.Instance.CurrentFight.enemyData;
        return AIDegrade.Resolve(ranked, fight.stupidityChance, fight.criticalFailureChance);
    }

    private static List<ActionSO> RankByScoreDescending(List<ActionSO> candidates)
    {
        return candidates
            .Select(a => (action: a, score: a.CreateAIScorer().Score(), tie: Random.value))
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.tie)
            .Select(x => x.action)
            .ToList();
    }
}
