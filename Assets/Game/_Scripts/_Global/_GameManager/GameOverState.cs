using UnityEngine;

public class GameOverState : GameState
{
    protected override void OnEnter()
    {
        bool playerDead = G.PlayerHero.Health.IsDead();
        bool enemyDead = G.EnemyHero.Health.IsDead();

        if (playerDead && enemyDead)
        {
            Debug.Log("[GameOver] Draw! Both heroes have fallen.");
            CampaignManager.Instance.ResolveDefeat();
        }
        else if (playerDead)
        {
            Debug.Log("[GameOver] Enemy wins! Your hero has fallen.");
            CampaignManager.Instance.ResolveDefeat();
        }
        else if (enemyDead)
        {
            Debug.Log("[GameOver] Player wins! Enemy hero has fallen.");
            CampaignManager.Instance.ResolveVictory();
        }

        // Do not call CompleteState — the round loop stops here. ResolveVictory/ResolveDefeat
        // above schedule the actual campaign transition (advance/reload/complete) after a delay.
    }
}
