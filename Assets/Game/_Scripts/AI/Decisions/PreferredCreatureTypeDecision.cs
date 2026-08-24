using System.Collections.Generic;

public class PreferredCreatureTypeDecision
{
    /// <summary>
    /// repairTarget: the AI's own shocked creature type ShouldSummonCreaturesDecision already picked
    /// to go repair, if this is a repair-driven summon (see docs/AI.md) — ranked above every other
    /// candidate. Null on an ordinary summon.
    /// excludeCreature: the type that just landed 3-of-a-kind, if this is the bonus roll granted by
    /// a triple (see docs/AI.md) — the AI never goes for the same type again on that bonus roll.
    /// Null on an ordinary roll.
    /// </summary>
    public CreatureSO Decide(CreatureSO repairTarget = null, CreatureSO excludeCreature = null)
    {
        var ranked = BuildRankedList(repairTarget, excludeCreature);
        var fight = CampaignStateManager.Instance.CurrentFight.enemyData;
        return AIDegrade.Resolve(ranked, fight.stupidityChance, fight.criticalFailureChance);
    }

    // Only ever called after ShouldSummonCreaturesDecision returns true. EnemyBoardFull can be true
    // here now (a shocked native creature's own slot needs no free slot to repair), but ranked is
    // still guaranteed non-empty: whenever ShouldSummonCreaturesDecision returned a repair-driven
    // choice, repairTarget is non-null here too (it's the same value that choice already carries).
    // repairTarget can also never equal excludeCreature — the just-tripled creature was just
    // healed/promoted, which unconditionally clears its own shock (StatusesManager.ClearAllStatuses)
    // — so it never gets stripped out by the exclude filter below (rule 5 — no defensive empty-list
    // handling needed).
    private static List<CreatureSO> BuildRankedList(CreatureSO repairTarget, CreatureSO excludeCreature)
    {
        var creatures = G.EnemyCreatures;
        var owned = G.EnemyCreaturesManager;
        var ranked = new List<CreatureSO>();

        if (repairTarget != null) ranked.Add(repairTarget);

        if (AIController.EnemyCreatureCount == 0 && owned.Tank == null)
            ranked.Add(creatures.tank);
        if (AIController.AnyPlayerCreatureBelow(0.3f) && owned.Archer == null && !ranked.Contains(creatures.archer))
            ranked.Add(creatures.archer);
        if (AIController.PlayerHeroBelow(0.3f) && owned.Mage == null && !ranked.Contains(creatures.mage))
            ranked.Add(creatures.mage);

        // No-signal fallback: remaining not-yet-listed, not-owned types, in CreaturesManager.AllSlots()'s own order.
        AddIfMissing(ranked, creatures.tank, owned.Tank == null);
        AddIfMissing(ranked, creatures.mage, owned.Mage == null);
        AddIfMissing(ranked, creatures.archer, owned.Archer == null);

        ranked.Remove(excludeCreature);
        return ranked;
    }

    private static void AddIfMissing(List<CreatureSO> ranked, CreatureSO candidate, bool notOwned)
    {
        if (notOwned && !ranked.Contains(candidate)) ranked.Add(candidate);
    }
}
