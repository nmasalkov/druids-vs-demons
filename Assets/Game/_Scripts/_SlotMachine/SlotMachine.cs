using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SlotMachine : MonoBehaviour
{
    [SerializeField] private List<SlotColumn> columns;
    [SerializeField] private float stopDelayBetweenColumns = 0.5f;
    [SerializeField] private bool isPlayerMachine = true;
    [SerializeField] private Button spinButton;
    [SerializeField] private Button finishRollButton;
    [SerializeField] private Button creatureRollButton;
    [SerializeField] private Button nukeRollButton;
    [SerializeField] private Button spellRollButton;
    [SerializeField] private float typeSwitchSettleDelay = 0.5f;

    public enum MachineState { FirstRoll, Rolling, PostRolls }
    public enum RollType { Creature, Nuke, Spell }
    private MachineState _machineState = MachineState.FirstRoll;

    public RollType CurrentRollType { get; private set; } = RollType.Creature;
    private bool _typeButtonsLocked;

    private bool _stopping;
    private int _columnsStopped;
    private int _spinningCount;
    private int _rerollingCount;

    public event Action OnSlotMachineStart;
    public event Action OnSlotMachineStop;
    public event Action OnPostRollsEnter;
    public event Action OnPostRollsExit;
    public event Action OnRerollResolved;
    public event Action<List<ActionSO>, RollType> OnFinishRollCompleted;

    public IReadOnlyList<SlotColumn> Columns => columns;
    public bool IsPlayerMachine => isPlayerMachine;

    void Awake()
    {
        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            OnSlotMachineStart += col.StartSpin;
            OnSlotMachineStop += col.StopSpin;
            OnPostRollsEnter += col.OnPostRollsEnter;
            OnPostRollsExit += col.OnPostRollsExit;
            col.AddStopDelay(i * stopDelayBetweenColumns);
            col.OnColumnStopped += HandleColumnStopped;
            col.OnRerollStarted += HandleRerollStarted;
        }

        spinButton.onClick.AddListener(OnSpinButtonClicked);
        finishRollButton.onClick.AddListener(OnFinishRollClicked);
        creatureRollButton.onClick.AddListener(() => SwitchRollType(RollType.Creature));
        nukeRollButton.onClick.AddListener(() => SwitchRollType(RollType.Nuke));
        spellRollButton.onClick.AddListener(() => SwitchRollType(RollType.Spell));
        finishRollButton.gameObject.SetActive(false);
        UpdateTypeButtonsVisibility();
    }

    void OnDestroy()
    {
        foreach (var col in columns)
        {
            OnSlotMachineStart -= col.StartSpin;
            OnSlotMachineStop -= col.StopSpin;
            OnPostRollsEnter -= col.OnPostRollsEnter;
            OnPostRollsExit -= col.OnPostRollsExit;
            col.OnColumnStopped -= HandleColumnStopped;
            col.OnRerollStarted -= HandleRerollStarted;
        }

        spinButton.onClick.RemoveListener(OnSpinButtonClicked);
        finishRollButton.onClick.RemoveListener(OnFinishRollClicked);
        creatureRollButton.onClick.RemoveAllListeners();
        nukeRollButton.onClick.RemoveAllListeners();
        spellRollButton.onClick.RemoveAllListeners();
    }

    public ActionSO[] GetActionOptions()
    {
        switch (CurrentRollType)
        {
            case RollType.Nuke:
            {
                var n = isPlayerMachine ? G.DefaultNukes : G.EnemyNukes;
                return new ActionSO[] { n.nukeA, n.nukeB, n.nukeC };
            }
            case RollType.Spell:
            {
                var s = isPlayerMachine ? G.DefaultSpells : G.EnemySpells;
                return new ActionSO[] { s.spellA, s.spellB, s.spellC };
            }
            default:
            {
                var c = isPlayerMachine ? G.DefaultCreatures : G.EnemyCreatures;
                return new ActionSO[] { c.tank, c.mage, c.archer };
            }
        }
    }

    private void ApplyRollType(RollType newType)
    {
        CurrentRollType = newType;
        Reset();
    }

    /// <summary>
    /// AI-driven roll-type entry point — same set-and-reshuffle as the player's type buttons, minus
    /// the button-debounce lock (not relevant to a non-UI caller). Only ever called by RollState,
    /// never by AIController itself. See docs/AI.md.
    /// </summary>
    public void SetRollType(RollType type)
    {
        if (_machineState != MachineState.FirstRoll) return;
        if (type == CurrentRollType) return;
        ApplyRollType(type);
    }

    private void SwitchRollType(RollType newType)
    {
        if (_typeButtonsLocked) return;
        if (_machineState != MachineState.FirstRoll) return;
        if (newType == CurrentRollType) return;

        ApplyRollType(newType);
        _typeButtonsLocked = true;
        Utils.DoAfterDelay.Execute(() => _typeButtonsLocked = false, typeSwitchSettleDelay);
    }

    private void UpdateTypeButtonsVisibility()
    {
        bool show = _machineState == MachineState.FirstRoll;
        creatureRollButton.gameObject.SetActive(show);
        nukeRollButton.gameObject.SetActive(show);
        spellRollButton.gameObject.SetActive(show);
    }

    private void HideTypeButtons()
    {
        creatureRollButton.gameObject.SetActive(false);
        nukeRollButton.gameObject.SetActive(false);
        spellRollButton.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (_machineState == MachineState.FirstRoll && !_stopping)
                StartAll();
            else if (_machineState == MachineState.Rolling && !_stopping)
                StopAll();
        }
    }

    public void StartAll()
    {
        if (_machineState == MachineState.PostRolls)
            OnPostRollsExit?.Invoke();

        _machineState = MachineState.Rolling;
        _stopping = false;
        _columnsStopped = 0;
        _spinningCount = columns.Count;
        HideTypeButtons();

        var decided = G.Rigger.DecideFullRoll(GetActionOptions(), isPlayerMachine);
        for (int i = 0; i < columns.Count; i++)
            columns[i].AssignWinningAction(decided[i]);

        OnSlotMachineStart?.Invoke();
    }

    /// <summary>Every other column's current result, excluding the given one — used by a rerolling
    /// column to check whether it's in a dirty-triple situation. See SlotMachineRigger.</summary>
    public ActionSO[] OtherWinningActions(SlotColumn excluding)
    {
        var result = new ActionSO[columns.Count - 1];
        int i = 0;
        foreach (var col in columns)
        {
            if (col == excluding) continue;
            result[i++] = col.WinningAction;
        }
        return result;
    }

    public void StopAll()
    {
        if (_stopping) return;
        _stopping = true;
        OnSlotMachineStop?.Invoke();
    }

    private void StopAllSpinning()
    {
        foreach (var col in columns)
            col.StopSpin();
    }

    private void TurnOffFinishButton()
    {
        finishRollButton.interactable = false;
    }

    private void TurnOnFinishButton()
    {
        finishRollButton.interactable = true;
    }

    private void HandleRerollStarted()
    {
        _rerollingCount++;
        TurnOffFinishButton();
    }

    private void HandleColumnStopped()
    {
        _columnsStopped++;

        if (_machineState == MachineState.Rolling)
        {
            if (_columnsStopped >= columns.Count)
            {
                _machineState = MachineState.PostRolls;
                _stopping = false;
                _columnsStopped = 0;
                spinButton.gameObject.SetActive(false);

                if (IsTriple())
                {
                    FinishRoll();
                    return;
                }

                finishRollButton.gameObject.SetActive(true);
                TurnOnFinishButton();
                OnPostRollsEnter?.Invoke();
            }
        }
        else if (_machineState == MachineState.PostRolls)
        {
            _rerollingCount--;
            if (_rerollingCount <= 0)
            {
                _rerollingCount = 0;

                if (IsTriple())
                {
                    FinishRoll();
                    return;
                }

                TurnOnFinishButton();
                OnRerollResolved?.Invoke();
            }
        }
    }

    private void OnSpinButtonClicked()
    {
        if (_machineState == MachineState.FirstRoll)
            StartAll();
        else if (_machineState == MachineState.Rolling && !_stopping)
            StopAll();
    }

    private void OnFinishRollClicked()
    {
        FinishRoll();
    }

    public void FinishRoll()
    {
        var rolledActions = new List<ActionSO>();
        foreach (var col in columns)
            rolledActions.Add(col.WinningAction);

        OnFinishRollCompleted?.Invoke(rolledActions, CurrentRollType);
    }

    private bool IsTriple()
    {
        if (columns.Count < 3) return false;
        var first = columns[0].WinningAction;
        for (int i = 1; i < columns.Count; i++)
        {
            if (columns[i].WinningAction != first) return false;
        }
        return true;
    }

    public void Reset()
    {
        _machineState = MachineState.FirstRoll;
        _stopping = false;
        _columnsStopped = 0;
        _spinningCount = 0;
        _rerollingCount = 0;

        foreach (var col in columns)
            col.ResetColumn();

        spinButton.gameObject.SetActive(true);
        finishRollButton.gameObject.SetActive(false);
        UpdateTypeButtonsVisibility();
    }

    /// <summary>
    /// Lightweight reset: resets UI and column PostRolls state so the player must spin again.
    /// Does not reshuffle cards. Safe to call while machine is inactive.
    /// </summary>
    public void ResetUI()
    {
        if (_machineState == MachineState.PostRolls)
            OnPostRollsExit?.Invoke();

        _machineState = MachineState.FirstRoll;
        _stopping = false;
        _columnsStopped = 0;
        _rerollingCount = 0;
        _spinningCount = 0;

        foreach (var col in columns)
            col.ResetState();

        spinButton.gameObject.SetActive(true);
        finishRollButton.gameObject.SetActive(false);
        UpdateTypeButtonsVisibility();
    }
}
