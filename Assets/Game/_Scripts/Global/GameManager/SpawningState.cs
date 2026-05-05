using Game._Scripts.PlayerView;

public class SpawningState : GameState
{
    public override void OnStateStart()
    {
        var creaturesManager = SpawnStateManager.Instance.GetActiveCreaturesManager();
        creaturesManager.SpawnCreatures(RollStateManager.Instance.CreaturesToSpawn);
        CompleteState();
    }
}
