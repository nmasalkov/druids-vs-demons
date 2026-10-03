using Game._Scripts.Creatures;

/// <summary>
/// Shared base for action resolvers (Nuke, Spell, …). Concrete resolvers fill their own
/// typed Shots list during their typed <c>Resolve(...)</c> call; <see cref="ApplyInstant"/>
/// applies all queued effects without animation (rule #7).
///
/// Also the uniform entry point for reward upgrades (docs/Rewards.md): <see cref="Boosted"/> for
/// a percentage stat boost, <see cref="IsImproved{T}"/> for a mechanic-changing improvement —
/// <c>if (IsImproved(caster, out ShockImprovementSO improvement)) { …altered logic… }</c>. Both are
/// player-only: an enemy caster always gets the base value / false.
/// </summary>
public abstract class ActionResolver
{
    public abstract void ApplyInstant();

    protected static bool IsPlayer(Hero caster) => caster == G.PlayerHero;

    protected static bool IsImproved<T>(Hero caster, out T improvement) where T : ActionImprovementSO
        => RewardBonuses.TryGetImprovement(IsPlayer(caster), out improvement);

    protected static float Boosted(ActionSO source, Hero caster, float baseValue)
        => RewardBonuses.ApplyActionBonus(source, baseValue, IsPlayer(caster));
}
