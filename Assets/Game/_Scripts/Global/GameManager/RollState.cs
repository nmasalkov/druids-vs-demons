public class RollState : GameState
{
    public override void OnStateStart()
    {
        RollStateManager.Instance.ActivateSlotMachine();
        RollStateManager.Instance.OnRollFinished += HandleRollFinished;
    }

    public override void OnStateEnd()
    {
        RollStateManager.Instance.OnRollFinished -= HandleRollFinished;
    }

    private void HandleRollFinished()
    {
        CompleteState();
    }
}
