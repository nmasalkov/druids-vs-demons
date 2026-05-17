using System.Collections.Generic;
using Game._Scripts.Creatures;

public class BattleState : GameState
{
    public override void OnStateStart()
    {
        Utils.DoAfterDelay.Execute(BeginBattle, 0f);
    }

    public void BeginBattle()
    {
        var playerCreatures = G.PlayerCreaturesManager.GetAllCreatures();
        var enemyCreatures = G.EnemyCreaturesManager.GetAllCreatures();
        var playerHero = G.PlayerHero;
        var enemyHero = G.EnemyHero;

        if (playerCreatures.Count == 0 && enemyCreatures.Count == 0
            && (playerHero == null || playerHero.Health.IsDead())
            && (enemyHero == null || enemyHero.Health.IsDead()))
        {
            CompleteState();
            return;
        }

        var resolver = new AttacksResolver();
        resolver.Resolve(playerCreatures, enemyCreatures, playerHero, enemyHero);

        float maxDuration = resolver.ExecuteAttacks(null);

        if (maxDuration <= 0f)
        {
            CompleteState();
            return;
        }

        float endDelay = maxDuration + 1f;
        Utils.DoAfterDelay.Execute(CompleteState, endDelay);
    }
}
