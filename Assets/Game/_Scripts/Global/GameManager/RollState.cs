using System.Collections.Generic;
using System.Linq;
using Game._Scripts.Global;

/// <summary>
/// Activates the active side's slot machine and waits for the roll to finish. For the AI-controlled
/// side, this is also the orchestrator that drives the whole roll: it asks AIController for
/// decisions and executes them itself (SetRollType/StartAll/TriggerReroll/FinishRoll) — the same
/// calls a player's UI would make. AIController itself never touches SlotMachine. See docs/AI.md.
/// </summary>
public class RollState : GameState
{
    private const float AIThinkDelay = 2f;

    private SlotMachine _machine;
    private ActionSO _desiredAction;
    private int _rerollBudget;
    private int _rerollsUsed;

    protected override void OnEnter()
    {
        RollStateManager.Instance.OnRollFinished += HandleRollFinished;
        RollStateManager.Instance.ActivateSlotMachine();

        if (GameManager.Instance.ActiveSide != ActiveSide.Enemy) return;

        Utils.DoAfterDelay.Execute(BeginAITurn, 0f);
    }

    protected override void OnExit()
    {
        RollStateManager.Instance.OnRollFinished -= HandleRollFinished;
        if (_machine == null) return;
        _machine.OnPostRollsEnter -= HandlePostRollsEnter;
        _machine.OnRerollResolved -= HandleRerollResolved;
    }

    private void HandleRollFinished() => CompleteState();

    private void BeginAITurn()
    {
        if (IsStale) return;

        _machine = RollStateManager.Instance.ActiveMachine;
        _rerollsUsed = 0;

        // If the AI's own previous roll this turn tripled, this is the bonus roll TakeTurn()'s
        // do-while grants — never go for the same action that just landed 3-of-a-kind (docs/AI.md).
        var justTripledAction = GetJustTripledAction();

        var summon = AIController.DecideShouldSummonCreatures();
        SlotMachine.RollType rollType;
        if (summon.ShouldSummon)
        {
            _desiredAction = AIController.DecidePreferredCreatureType(summon.RepairTarget, justTripledAction as CreatureSO);
            rollType = SlotMachine.RollType.Creature;
        }
        else
        {
            _desiredAction = AIController.DecidePreferredNukeOrSpell(justTripledAction);
            rollType = _desiredAction is NukeSO ? SlotMachine.RollType.Nuke : SlotMachine.RollType.Spell;
        }

        _machine.SetRollType(rollType);
        _machine.OnPostRollsEnter += HandlePostRollsEnter;
        _machine.OnRerollResolved += HandleRerollResolved;
        _machine.StartAll();

        Utils.DoAfterDelay.Execute(() => { if (!IsStale) _machine.StopAll(); }, AIThinkDelay);
    }

    private void HandlePostRollsEnter()
    {
        if (IsStale) return;
        var landedSlots = ReadCurrentSlots();
        _rerollBudget = AIController.DecideRerollBudget(landedSlots, _desiredAction);
        EvaluateReroll(landedSlots);
    }

    private void HandleRerollResolved()
    {
        if (IsStale) return;
        EvaluateReroll(ReadCurrentSlots());
    }

    private void EvaluateReroll(List<ActionSO> currentSlots)
    {
        var choice = AIController.DecideReroll(currentSlots, _desiredAction, _rerollsUsed, _rerollBudget);
        if (!choice.ShouldReroll)
        {
            _machine.FinishRoll();
            return;
        }

        _rerollsUsed++;
        AIController.SpendReroll();
        _machine.Columns[choice.SlotIndex].TriggerReroll();
    }

    private List<ActionSO> ReadCurrentSlots() => _machine.Columns.Select(c => c.WinningAction).ToList();

    private static ActionSO GetJustTripledAction()
    {
        if (!RollStateManager.Instance.TripleRolled) return null;
        return RollStateManager.Instance.LastRollType switch
        {
            SlotMachine.RollType.Nuke => RollStateManager.Instance.NukeEntries[0].Nuke,
            SlotMachine.RollType.Spell => RollStateManager.Instance.SpellEntries[0].Spell,
            _ => RollStateManager.Instance.SpawnEntries[0].Creature,
        };
    }
}
