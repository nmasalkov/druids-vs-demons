public class RollState : GameState
{
    public override void OnStateStart()
    {
        RollStateManager.Instance.ActivatePlayerSlotMachine();
    }
}

