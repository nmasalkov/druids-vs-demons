using Game._Scripts.Creatures;
using Game._Scripts.PlayerView;
using UnityEngine;

public class RollState : GameState
{
    public override void OnStateStart()
    {
        RollStateManager.Instance.OnRollFinished += HandleRollFinished;
        RollStateManager.Instance.ActivateSlotMachine();
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
