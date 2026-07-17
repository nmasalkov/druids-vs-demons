using Game._Scripts.Global;
using UnityEngine;

public class AIController : MonoBehaviour
{
    public static AIController Instance { get; private set; }

    private readonly AIRollController _rollController = new();
    private SlotMachine _targetMachine;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        GameManager.OnBattleRestart += ReleaseControl;
    }

    void OnDestroy()
    {
        GameManager.OnBattleRestart -= ReleaseControl;
    }

    public void TakeControl(SlotMachine slotMachine)
    {
        _targetMachine = slotMachine;
        _targetMachine.OnPostRollsEnter += HandlePostRolls;

        _targetMachine.StartAll();
        Utils.DoAfterDelay.Execute(() => { if (_targetMachine != null) _targetMachine.StopAll(); }, 2f);
    }

    public void ReleaseControl()
    {
        if (_targetMachine == null) return;
        _targetMachine.OnPostRollsEnter -= HandlePostRolls;
        _targetMachine = null;
    }

    private void HandlePostRolls()
    {
        var decision = _rollController.Decide();
        switch (decision)
        {
            case AIRollDecision.FinishRoll:
                _targetMachine.FinishRoll();
                break;
            case AIRollDecision.PostRollSlot1:
            case AIRollDecision.PostRollSlot2:
            case AIRollDecision.PostRollSlot3:
                // TODO: handle rerolls
                break;
        }
    }
}

