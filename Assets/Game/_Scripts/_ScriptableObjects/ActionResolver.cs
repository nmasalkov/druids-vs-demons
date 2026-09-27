/// <summary>
/// Shared base for action resolvers (Nuke, Spell, …). Concrete resolvers fill their own
/// typed Shots list during their typed <c>Resolve(...)</c> call; <see cref="ApplyInstant"/>
/// applies all queued effects without animation (rule #7).
/// </summary>
public abstract class ActionResolver
{
    public abstract void ApplyInstant();
}

