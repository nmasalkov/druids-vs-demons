using UnityEngine;

// Deliberately NOT stupidity-affected — a fixed formula, not a ranked choice.
public class RerollBudgetDecision
{
    // Each 10 hp of (effective) health reserves one reroll: EnemyData.hp and EnemyData.rerollsAmount
    // are balanced ~10:1 per fight, so this reads as "how many 10-hp chunks the enemy still has
    // intact." See docs/AI.md.
    private const float HealthPerReroll = 10f;

    // A point of board-power deficit counts as a point of hp already lost — 1:1, so every 10 points
    // the player's board out-guns the AI by frees up one more reroll, exactly like taking 10 damage
    // would have.
    private const float HealthPerFirepowerPoint = 1f;

    public int Decide()
    {
        int pool = AIController.RerollsRemaining;
        int desiredBudget = Mathf.Max(1, pool - HealthReserve());
        return Mathf.Min(desiredBudget, pool); // "at least 1" must never exceed what's left in the pool
    }

    // How much of the pool the AI holds back: high near full health and with a board that matches the
    // player's, shrinking (so the budget climbs) as it takes damage or falls behind on the board.
    private static int HealthReserve() => Mathf.FloorToInt(EffectiveHealth() / HealthPerReroll);

    /// <summary>Current hp discounted by the board-power deficit — how healthy the AI effectively is,
    /// rather than how healthy its hp bar alone says it is. Raw hp points, not percent: the one hp
    /// check in this system that isn't percent-based, because the formula is only meaningful against
    /// the fight's own hp:rerolls ratio. Can go negative when the player's lead exceeds the AI's
    /// remaining hp — Decide()'s existing Mathf.Min clamp caps the result at the whole pool.</summary>
    private static float EffectiveHealth() => AIController.EnemyHeroCurrentHealth - FirepowerDeficitAsHealth();

    /// <summary>The player's board-power lead expressed in hp points. One-way on purpose: the AI being
    /// AHEAD on power does not make it hoard rerolls beyond what its own hp already dictates.</summary>
    private static float FirepowerDeficitAsHealth() =>
        Mathf.Max(0f, AIController.PlayerFirepowerLead) * HealthPerFirepowerPoint;
}
