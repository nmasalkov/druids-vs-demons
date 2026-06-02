using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SlotMachine : MonoBehaviour
{
    [SerializeField] private List<SlotColumn> columns;
    [SerializeField] private float stopDelayBetweenColumns = 0.5f;
    [SerializeField] private Button spinButton;
    [SerializeField] private Button finishRollButton;
    [SerializeField] private Button creatureRollButton;
    [SerializeField] private Button nukeRollButton;
    [SerializeField] private float typeSwitchSettleDelay = 0.5f;

    public enum MachineState { FirstRoll, Rolling, PostRolls }
    public enum RollType { Creature, Nuke }
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
    public event Action<List<ActionSO>, RollType> OnFinishRollCompleted;

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
    }

    public ActionSO[] GetActionOptions()
    {
        switch (CurrentRollType)
        {
            case RollType.Nuke:
            {
                var dn = G.DefaultNukes;
                return new ActionSO[] { dn.nukeA, dn.nukeB, dn.nukeC };
            }
            default:
            {
                var dc = G.DefaultCreatures;
                return new ActionSO[] { dc.tank, dc.mage, dc.archer };
            }
        }
    }

    private void SwitchRollType(RollType newType)
    {
        if (_typeButtonsLocked) return;
        if (_machineState != MachineState.FirstRoll) return;
        if (newType == CurrentRollType) return;

        CurrentRollType = newType;
        _typeButtonsLocked = true;
        HideTypeButtons();
        Reset();
        Utils.DoAfterDelay.Execute(() =>
        {
            _typeButtonsLocked = false;
            UpdateTypeButtonsVisibility();
        }, typeSwitchSettleDelay);
    }

    private void UpdateTypeButtonsVisibility()
    {
        bool show = !_typeButtonsLocked && _machineState == MachineState.FirstRoll;
        creatureRollButton.gameObject.SetActive(show);
        nukeRollButton.gameObject.SetActive(show);
    }

    private void HideTypeButtons()
    {
        creatureRollButton.gameObject.SetActive(false);
        nukeRollButton.gameObject.SetActive(false);
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
        OnSlotMachineStart?.Invoke();
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
