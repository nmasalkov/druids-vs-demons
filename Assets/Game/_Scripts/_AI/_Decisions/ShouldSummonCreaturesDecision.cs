using System.Collections.Generic;
using Game._Scripts.Creatures;
using Game._Scripts.Global;

public class ShouldSummonCreaturesDecision
{
    // Placeholder — the spec gives 60%/40% for "behind the player, 1/2 creatures" but no number for
    // "not behind." Retune freely, see docs/AI.md.
    private const int DefaultSummonChance = 15;

    // Repair-desire probabilities for the AI's own shocked (stunned) native creatures — rolling a
    // shocked creature's own type heals/promotes it, which also cures the shock (see
    // SpawningState.HandleExistingCreature / StatusesManager.ClearAllStatuses). See docs/AI.md.
    private const int TwoShockedRepairChance = 80;
    private const int OneShockedLevel3RepairChance = 50;
    private const int OneShockedLevel4RepairChance = 80;

    public SummonChoice Decide()
    {
        if (GameManager.Instance.IsFirstRound) return new SummonChoice(true, null);   // NO STUPID

        if (TryRepairShockedCreature(out var repair)) return repair;
        if (AIController.EnemyBoardFull) return new SummonChoice(false, null);        // NO STUPID — nothing left to summon

        return new SummonChoice(Degrade(DesiredSummon()), null);
    }

    private static bool TryRepairShockedCreature(out SummonChoice choice)
    {
        var shocked = AIController.ShockedEnemyCreatures;
        if (shocked.Count == 0)
        {
            choice = default;
            return false;
        }

        var target = BestToRepair(shocked);
        choice = new SummonChoice(Degrade(DesiredRepair(shocked)), target.Data);
        return true;
    }

    private static bool DesiredRepair(IReadOnlyList<Creature> shocked)
    {
        if (shocked.Count >= 3) return true;
        if (shocked.Count == 2) return AIController.RollForProbability(TwoShockedRepairChance);
        return DesiredRepairByLevel(shocked[0].Experience.Level);
    }

    // Exactly 1 shocked creature — desire scales with how much is invested in it.
    private static bool DesiredRepairByLevel(int level)
    {
        if (level >= 4) return AIController.RollForProbability(OneShockedLevel4RepairChance);
        if (level == 3) return AIController.RollForProbability(OneShockedLevel3RepairChance);
        return false; // level 1/2 — placeholder, spec gave no number here, retune freely
    }

    /// <summary>Among several shocked creatures, the one most worth repairing — highest current HP.</summary>
    private static Creature BestToRepair(IReadOnlyList<Creature> shocked)
    {
        Creature best = null;
        foreach (var creature in shocked)
            if (best == null || creature.Health.CurrentHealth > best.Health.CurrentHealth)
                best = creature;
        return best;
    }

    private static bool DesiredSummon()
    {
        int enemyCount = AIController.EnemyCreatureCount;
        int playerCount = AIController.PlayerCreatureCount;

        if (enemyCount == 0) return true;
        if (enemyCount < playerCount && enemyCount == 1) return AIController.RollForProbability(60);
        if (enemyCount < playerCount && enemyCount == 2) return AIController.RollForProbability(40);
        return AIController.RollForProbability(DefaultSummonChance);
    }

    private static bool Degrade(bool desired)
    {
        var fight = CampaignStateManager.Instance.CurrentFight.enemyData;
        return AIDegrade.Resolve(new[] { desired, !desired }, fight.stupidityChance, fight.criticalFailureChance);
    }
}
