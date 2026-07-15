using System.Collections.Generic;
using Game._Scripts.Creatures;

public class BattleState : GameState
{
    protected override void OnEnter()
    {
        Utils.DoAfterDelay.Execute(BeginBattle, 0f);
    }

    /// <summary>
    /// Instant resolution path covering battle + post-battle (rule 7): plans the same attack
    /// assignments as the animated battle, lands every hit immediately, removes dead bodies,
    /// clears BattleCry and grants the earned XP without gem flights. No animation, no waits.
    /// </summary>
    public static void ResolveBattleInstant()
    {
        var resolver = new AttacksResolver();
        resolver.Resolve(
            G.PlayerCreaturesManager.GetAllCreatures(), G.EnemyCreaturesManager.GetAllCreatures(),
            G.PlayerHero, G.EnemyHero, G.PlayerView.Shield, G.EnemyView.Shield);

        resolver.ApplyAttacksInstant();

        G.PlayerCreaturesManager.CleanUpDead();
        G.EnemyCreaturesManager.CleanUpDead();
        PostBattleState.ClearBattleCryStatuses();
        ExperienceManager.Instance.ResolveGemsInstant();
    }

    public void BeginBattle()
    {
        var playerCreatures = G.PlayerCreaturesManager.GetAllCreatures();
        var enemyCreatures = G.EnemyCreaturesManager.GetAllCreatures();
        var playerHero = G.PlayerHero;
        var enemyHero = G.EnemyHero;
        var playerShield = G.PlayerView.Shield;
        var enemyShield = G.EnemyView.Shield;

        if (playerCreatures.Count == 0 && enemyCreatures.Count == 0
            && (playerHero == null || playerHero.Health.IsDead())
            && (enemyHero == null || enemyHero.Health.IsDead()))
        {
            CompleteState();
            return;
        }

        var resolver = new AttacksResolver();
        resolver.Resolve(playerCreatures, enemyCreatures, playerHero, enemyHero, playerShield, enemyShield);

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
