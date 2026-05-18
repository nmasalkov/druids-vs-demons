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

    public enum MachineState { FirstRoll, Rolling, PostRolls }
    private MachineState _machineState = MachineState.FirstRoll;

    private bool _stopping;
    private int _columnsStopped;
    private int _spinningCount;
    private int _rerollingCount;

    public event Action OnSlotMachineStart;
    public event Action OnSlotMachineStop;
    public event Action OnPostRollsEnter;
    public event Action OnPostRollsExit;
    public event Action<List<CreatureSO>> OnFinishRollCompleted;

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
        finishRollButton.gameObject.SetActive(false);
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
        var colors = finishRollButton.colors;
        var c = colors.normalColor;
        c.a = 99f / 255f;
        colors.normalColor = c;
        finishRollButton.colors = colors;
    }

    private void TurnOnFinishButton()
    {
        finishRollButton.interactable = true;
        var colors = finishRollButton.colors;
        var c = colors.normalColor;
        c.a = 1f;
        colors.normalColor = c;
        finishRollButton.colors = colors;
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
        var rolledCreatures = new List<CreatureSO>();
        foreach (var col in columns)
            rolledCreatures.Add(col.WinningCreature);

        OnFinishRollCompleted?.Invoke(rolledCreatures);
    }

    private bool IsTriple()
    {
        if (columns.Count < 3) return false;
        var first = columns[0].WinningCreature;
        for (int i = 1; i < columns.Count; i++)
        {
            if (columns[i].WinningCreature != first) return false;
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
    }

    /// <summary>
    /// Lightweight reset: only resets UI state so the player must spin again.
    /// Does not touch columns. Safe to call while machine is inactive.
    /// </summary>
    public void ResetUI()
    {
        _machineState = MachineState.FirstRoll;
        _stopping = false;
        _columnsStopped = 0;
        _rerollingCount = 0;
        spinButton.gameObject.SetActive(true);
        finishRollButton.gameObject.SetActive(false);
    }
}
