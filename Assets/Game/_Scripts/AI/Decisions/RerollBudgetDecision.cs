using UnityEngine;

// Deliberately NOT stupidity-affected — a fixed formula, not a ranked choice.
public class RerollBudgetDecision
{
    public int Decide()
    {
        int pool = AIController.RerollsRemaining;
        int desiredBudget = Mathf.Max(1, pool - HealthReserve());
        return Mathf.Min(desiredBudget, pool); // "at least 1" must never exceed what's left in the pool
    }

    // EnemyData.hp and EnemyData.rerollsAmount are balanced ~10:1 per fight (see docs/AI.md) — this
    // reads as "how many 10-hp chunks of current HP the enemy still has intact," so the base reroll
    // count starts low near full HP (conserving the pool) and climbs as the enemy takes damage.
    // Raw HP points, not percent — the one HP check in this system that isn't percent-based.
    private static int HealthReserve() => Mathf.FloorToInt(AIController.EnemyHeroCurrentHealth / 10f);
}
