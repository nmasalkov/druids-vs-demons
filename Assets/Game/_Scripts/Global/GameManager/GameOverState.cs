using UnityEngine;

public class GameOverState : GameState
{
    protected override void OnEnter()
    {
        bool playerAlive = IsSideAlive(G.PlayerCreaturesManager, G.PlayerHero);
        bool enemyAlive = IsSideAlive(G.EnemyCreaturesManager, G.EnemyHero);

        if (!playerAlive && !enemyAlive)
            Debug.Log("[GameOver] Draw! Both sides are eliminated.");
        else if (!playerAlive)
            Debug.Log("[GameOver] Enemy wins! Player side is eliminated.");
        else if (!enemyAlive)
            Debug.Log("[GameOver] Player wins! Enemy side is eliminated.");

        // Do not call CompleteState — the game stops here.
    }

    private bool IsSideAlive(Game._Scripts.PlayerView.CreaturesManager creatures, Game._Scripts.Creatures.Hero hero)
    {
        if (hero != null && !hero.Health.IsDead()) return true;
        return creatures.GetAllCreatures().Count > 0;
    }
}
