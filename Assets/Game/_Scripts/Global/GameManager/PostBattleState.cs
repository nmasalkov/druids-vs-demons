public class PostBattleState : GameState
{
    protected override void OnEnter()
    {
        float cleanUpDelay = PostBattleStateManager.Instance.CleanUpDelay;

        Utils.DoAfterDelay.Execute(() =>
        {
            G.PlayerCreaturesManager.CleanUpDead();
            G.EnemyCreaturesManager.CleanUpDead();

            float gemDelay = PostBattleStateManager.Instance.GemCollectionDelay;
            Utils.DoAfterDelay.Execute(CollectExperience, gemDelay);
        }, cleanUpDelay);
    }

    private void CollectExperience()
    {
        ExperienceManager.Instance.OnAllGemsCollected += HandleGemsCollected;
        ExperienceManager.Instance.ResolveGems();
    }

    private void HandleGemsCollected()
    {
        ExperienceManager.Instance.OnAllGemsCollected -= HandleGemsCollected;
        CompleteState();
    }
}

