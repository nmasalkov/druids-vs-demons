using System;
using UnityEngine;
using UnityEngine.UI;

public partial class SlotColumn : MonoBehaviour
{
    [SerializeField] private Button rerollButton;

    private bool _postRollsEnabled;
    private bool _isReroll;
    private SlotMachine _slotMachine;

    public event Action OnColumnStopped;
    public event Action OnRerollStarted;

    void Start()
    {
        if (_slotMachine == null)
            _slotMachine = GetComponentInParent<SlotMachine>();

        PlaceCards();
        rerollButton.onClick.AddListener(OnRerollClicked);
        rerollButton.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        rerollButton.onClick.RemoveListener(OnRerollClicked);
    }

    public void AddStopDelay(float delay)
    {
        stopDuration += delay;
    }

    public void OnPostRollsEnter()
    {
        _postRollsEnabled = true;
        rerollButton.gameObject.SetActive(true);
    }

    public void OnPostRollsExit()
    {
        _postRollsEnabled = false;
        rerollButton.gameObject.SetActive(false);
    }

    private void OnRerollClicked()
    {
        if (!_postRollsEnabled || _state != State.Idle) return;
        _isReroll = true;
        OnRerollStarted?.Invoke();
        StartSpin();
        Utils.DoAfterDelay.Execute(StopSpin, 0.44f);
    }

    /// <summary>
    /// Resets runtime state (spinning, reroll flags, PostRolls UI) without reshuffling cards.
    /// Also snaps the reel back to its resting position, in case this is called while the column
    /// was mid-spin/mid-stop/mid-bounce (e.g. a battle restart while paused).
    /// </summary>
    public void ResetState()
    {
        _state = State.Idle;
        _isReroll = false;
        _postRollsEnabled = false;
        SnapToAligned();
        rerollButton.gameObject.SetActive(false);
    }

    public void StartSpin()
    {
        if (_state != State.Idle) return;
        PickRandomWinningAction();
        _state = State.Spinning;
        _currentSpeed = spinSpeed;
        _distanceSinceLastRecycle = 0f;
    }

    private void PickRandomWinningAction()
    {
        var options = _slotMachine.GetActionOptions();
        WinningAction = options[UnityEngine.Random.Range(0, options.Length)];
    }

    public void StopSpin()
    {
        if (_state != State.Spinning) return;
        _state = State.WaitingToStop;
    }

    void Update()
    {
        switch (_state)
        {
            case State.Spinning:
                UpdateSpinning();
                break;
            case State.WaitingToStop:
                UpdateWaitingToStop();
                break;
            case State.Stopping:
                UpdateStopping();
                break;
            case State.Bouncing:
                UpdateBouncing();
                break;
        }
    }
}
