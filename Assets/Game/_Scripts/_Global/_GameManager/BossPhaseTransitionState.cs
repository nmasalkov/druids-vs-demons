using System.Collections.Generic;
using _Scripts.Creatures;

/// <summary>
/// A boss dies but the next campaign entry is flagged FightSO.continuesPreviousFight: instead of
/// ending, the whole enemy side is swapped over to that phase in place, the encounter index moves
/// onto it (unsaved — see CampaignManager.AdvanceToBossPhase) and the fight carries on. Entered
/// out-of-band from GameManager.TryStartBossPhaseTransition when the enemy hero dies, never from
/// the round sequence; completing it drops straight back into the round loop at the player's turn.
///
/// The beats, in order: the boss's creatures die with him -> a pause -> he plays Rise -> a camera
/// shake and white flash fire the moment that animation lands, and the avatar/HP/roster swap happens
/// while the screen is white -> a pause on the new form -> the player's board dies, its death
/// animations play out in full and the bodies are cleared -> a beat on the empty field -> the new
/// phase's level-1 trio is summoned. Timings live on BossPhaseTransitionManager.
/// See docs/GameLoop.md.
/// </summary>
public class BossPhaseTransitionState : GameState
{
    /// <summary>Whether a hero death should become a phase change rather than a game over.</summary>
    public static bool IsAvailable()
    {
        // A double KO is a defeat: the player doesn't get to watch the boss come back.
        if (G.PlayerHero.Health.IsDead()) return false;
        return NextPhase() != null;
    }

    private static FightSO NextPhase() => CampaignManager.Instance.NextPhase;

    protected override void OnEnter()
    {
        G.EnemyCreaturesManager.KillAll();
        Utils.DoAfterDelay.Execute(PlayRise, BossPhaseTransitionManager.Instance.RiseDelay);
    }

    private void PlayRise()
    {
        if (IsStale) return;

        // Unchecked cast (rule 5): only an Animator-driven avatar can rise, so a fight authored with
        // a second phase on a Spine avatar should fail loudly rather than silently skip the beat.
        var bossAnimator = (BossAnimator)G.EnemyHero.Animator;
        bossAnimator.PlayRise();
        Utils.DoAfterDelay.Execute(PlayResurrection, bossAnimator.GetRiseDuration());
    }

    private void PlayResurrection()
    {
        if (IsStale) return;

        G.EnemyView.PlayResurrectionFeedback();
        Utils.DoAfterDelay.Execute(SwapEnemySide, BossPhaseTransitionManager.Instance.FlashToSwapDelay);
    }

    private void SwapEnemySide()
    {
        if (IsStale) return;

        ApplyNextPhase();
        Utils.DoAfterDelay.Execute(KillPlayerBoard, BossPhaseTransitionManager.Instance.BoardSwapDelay);
    }

    private void KillPlayerBoard()
    {
        if (IsStale) return;

        // Wait for the slowest death animation to actually finish, then let the bodies lie a beat
        // before they're cleared — otherwise they vanish mid-fall.
        float deathDuration = G.PlayerCreaturesManager.KillAll();
        Utils.DoAfterDelay.Execute(ClearPlayerBoard,
            deathDuration + PostBattleStateManager.Instance.CleanUpDelay);
    }

    private void ClearPlayerBoard()
    {
        if (IsStale) return;

        // The player's corpses have to be cleared before their turn starts: SpawningState reads an
        // occupied slot as "promote or heal what's already there", so a dead body left in one would
        // swallow the summon. PostBattleState normally does this, but it won't run again until after
        // the player's turn and the battle that follows it.
        G.PlayerCreaturesManager.CleanUpDead();
        Utils.DoAfterDelay.Execute(SummonEnemyBoard, BossPhaseTransitionManager.Instance.EmptyBoardDelay);
    }

    private void SummonEnemyBoard()
    {
        if (IsStale) return;

        SpawnEnemyCreatures();
        Utils.DoAfterDelay.Execute(FinishTransition, BossPhaseTransitionManager.Instance.PostSummonDelay);
    }

    private void FinishTransition()
    {
        if (IsStale) return;

        CompleteState();
    }

    /// <summary>
    /// Instant counterpart (rule 7): the same end state with no animation, pause or feedback — both
    /// boards cleared, the enemy side on the next phase, its level-1 trio summoned.
    /// </summary>
    public static void ResolveInstant()
    {
        G.EnemyCreaturesManager.ResetAll();
        G.PlayerCreaturesManager.ResetAll();
        ApplyNextPhase();
        SpawnEnemyCreatures();
    }

    private static void ApplyNextPhase()
    {
        // Frees the slots the boss's own dead creatures still occupy, so the incoming trio can spawn.
        G.EnemyCreaturesManager.CleanUpDead();

        // Move the campaign onto the phase first, then apply whatever is now current — so the phase
        // is reached through the same index CurrentFight resolves from, rather than being applied
        // behind the campaign's back while the index still says phase 1.
        CampaignManager.Instance.AdvanceToBossPhase();
        CampaignStateManager.Instance.ApplyFightToScene(CampaignManager.Instance.CurrentFight);
    }

    /// <summary>
    /// Summons the phase's level-1 archer/tank/mage. Public because GameManager reuses it when a
    /// continuation fight is entered *directly* (a debug profile pointing at its index) rather than
    /// through this transition — one definition of "the board a phase opens with", so the two entry
    /// paths can't drift apart.
    /// </summary>
    public static void SpawnEnemyCreatures()
    {
        var creatures = G.EnemyCreatures;
        G.EnemyCreaturesManager.SpawnCreatures(new List<CreatureSO>
        {
            creatures.archer,
            creatures.tank,
            creatures.mage
        });
    }
}
