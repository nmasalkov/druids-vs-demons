using System.Collections.Generic;
using Game._Scripts.Creatures;
using Game._Scripts.PlayerView;

/// <summary>
/// Bundle of "who is casting and against whom" passed to <see cref="ActionSO.CreateAndResolve"/>.
/// Each concrete action picks only the fields it needs (nukes use enemy targets,
/// self-spells use the caster's view, etc).
/// </summary>
public struct ActionContext
{
    public Hero Caster;
    public HeroView CasterView;
    public List<Creature> EnemyCreatures;
    public Hero EnemyHero;
    public Shield EnemyShield;
}

