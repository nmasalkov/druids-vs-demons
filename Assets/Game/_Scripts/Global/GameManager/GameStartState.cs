public class GameStartState : GameState
{
    protected override void OnEnter()
    {
        Utils.DoAfterDelay.Execute(CompleteState, 2f);
    }
}

