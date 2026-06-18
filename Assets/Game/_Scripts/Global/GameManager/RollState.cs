using Game._Scripts.Creatures;
using Game._Scripts.PlayerView;
using UnityEngine;

public class RollState : GameState
{
    protected override void OnEnter()
    {
        RollStateManager.Instance.OnRollFinished += HandleRollFinished;
        RollStateManager.Instance.ActivateSlotMachine();
    }

    protected override void OnExit()
    {
        RollStateManager.Instance.OnRollFinished -= HandleRollFinished;
    }

    private void HandleRollFinished()
    {
        CompleteState();
    }
}
