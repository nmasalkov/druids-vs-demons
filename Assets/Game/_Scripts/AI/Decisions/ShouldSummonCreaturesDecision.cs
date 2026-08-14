using Game._Scripts.Global;

public class ShouldSummonCreaturesDecision
{
    // Placeholder — the spec gives 60%/40% for "behind the player, 1/2 creatures" but no number for
    // "not behind." Retune freely, see docs/AI.md.
    private const int DefaultSummonChance = 15;

    public bool Decide()
    {
        if (GameManager.Instance.IsFirstRound) return true;    // NO STUPID
        if (AIController.EnemyBoardFull) return false;         // NO STUPID — nothing left to summon

        bool desired = DesiredSummon();
        var fight = CampaignStateManager.Instance.CurrentFight.enemyData;
        return AIDegrade.Resolve(new[] { desired, !desired }, fight.stupidityChance, fight.criticalFailureChance);
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
}
