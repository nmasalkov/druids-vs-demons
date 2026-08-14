using System.Collections.Generic;
using System.Linq;
using Game._Scripts.Creatures;
using UnityEngine;

/// <summary>
/// Shared, readable query surface for nuke/spell AI scoring (see docs/AI.md). Concrete scorers
/// should only ever read board state through these named helpers — never reach G/Health/
/// CreaturesManager directly — so a formula reads as a sequence of named checks, not data plumbing.
/// </summary>
public abstract class ActionAIScorer
{
    public abstract int Score();

    protected static bool PlayerHpPercentageBelow(int percent) => AIController.PlayerHeroBelow(percent / 100f);
    protected static bool PlayerHpPercentageMore(int percent) => !PlayerHpPercentageBelow(percent);
    protected static bool EnemyHpPercentageBelow(int percent) => AIController.EnemyHeroBelow(percent / 100f);
    protected static bool EnemyHpPercentageMore(int percent) => !EnemyHpPercentageBelow(percent);

    protected static bool PlayerHasShield => G.PlayerView.Shield != null;
    protected static bool EnemyHasShield => G.EnemyView.Shield != null;

    protected static bool PlayerHasTank => G.PlayerCreaturesManager.Tank != null;
    protected static bool PlayerHasMage => G.PlayerCreaturesManager.Mage != null;
    protected static bool PlayerHasArcher => G.PlayerCreaturesManager.Archer != null;
    protected static bool EnemyHasTank => G.EnemyCreaturesManager.Tank != null;
    protected static bool EnemyHasMage => G.EnemyCreaturesManager.Mage != null;
    protected static bool EnemyHasArcher => G.EnemyCreaturesManager.Archer != null;

    protected static Creature PlayerMage => G.PlayerCreaturesManager.Mage;
    protected static Creature PlayerArcher => G.PlayerCreaturesManager.Archer;

    protected static int PlayerCreatureCount => G.PlayerCreaturesManager.GetAllCreatures().Count;
    protected static int EnemyCreatureCount => G.EnemyCreaturesManager.GetAllCreatures().Count;

    protected static int CountPlayerCreaturesBelow(int percent) =>
        G.PlayerCreaturesManager.GetAllCreatures().Count(c => CreatureHpBelow(c, percent));
    protected static int CountEnemyCreaturesBelow(int percent) =>
        G.EnemyCreaturesManager.GetAllCreatures().Count(c => CreatureHpBelow(c, percent));

    protected static IReadOnlyList<Creature> PlayerCreatures => G.PlayerCreaturesManager.GetAllCreatures();

    protected static bool CreatureHpBelow(Creature creature, int percent) =>
        creature != null && creature.Health.HealthPercent < percent / 100f;

    protected static int ClampScore(int score) => Mathf.Clamp(score, 0, 100);
}
