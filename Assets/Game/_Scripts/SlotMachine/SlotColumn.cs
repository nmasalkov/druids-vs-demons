using System;
using UnityEngine;
using UnityEngine.UI;

public partial class SlotColumn : MonoBehaviour
{
    [SerializeField] private Button rerollButton;

    private bool _postRollsEnabled;
    private bool _isReroll;

    public event Action OnColumnStopped;
    public event Action OnRerollStarted;

    void Start()
    {
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

    public void StartSpin()
    {
        if (_state != State.Idle) return;
        _state = State.Spinning;
        _currentSpeed = spinSpeed;
        _distanceSinceLastRecycle = 0f;
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
