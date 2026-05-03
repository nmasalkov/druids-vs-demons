public class GameStartState : GameState
{
    public override void OnStateStart()
    {
        Utils.DoAfterDelay.Execute(CompleteState, 2f);
    }
}

