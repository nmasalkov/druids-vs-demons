/// <summary>
/// Debug-only surface (rule 20) — used by BalanceTool's "compared firepower" HUD readout, never by
/// real combat resolution. Mirrors ResolveTeam's (AttacksResolver.Mechanics.cs) per-attacker damage
/// formula exactly, without target/HP bookkeeping, since this is a pre-battle total-output estimate.
/// </summary>
public partial class AttacksResolver
{
    public static float EstimateFirepower(bool isPlayerSide)
    {
        var creatures = isPlayerSide ? G.PlayerCreaturesManager.GetAllCreatures() : G.EnemyCreaturesManager.GetAllCreatures();
        float total = 0f;
        foreach (var creature in creatures)
        {
            if (creature.StatusesManager.IsShocked) continue;

            var stats = creature.Data.Stats(creature.Experience.Level);
            float dmgPerHit = stats.damage * creature.StatusesManager.AttackDamageMultiplier;
            if (dmgPerHit <= 0f) continue;

            total += dmgPerHit * stats.numberOfAttacks;
        }
        return total;
    }
}
