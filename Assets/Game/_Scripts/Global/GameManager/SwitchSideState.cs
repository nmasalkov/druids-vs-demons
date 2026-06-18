using Game._Scripts.Global;

public class SwitchSideState : GameState
{
    private readonly ActiveSide _targetSide;

    public SwitchSideState(ActiveSide targetSide)
    {
        _targetSide = targetSide;
    }

    protected override void OnEnter()
    {
        GameManager.Instance.SetActiveSide(_targetSide);
        CompleteState();
    }
}

