using System;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class SlotColumn : MonoBehaviour
{
    [SerializeField] private Button rerollButton;
    [SerializeField] private TMP_Text rerollCostText;
    [SerializeField] private MMF_Player rerollCostChangeFeedback;

    private bool _postRollsEnabled;
    private bool _isReroll;
    private SlotMachine _slotMachine;
    private Color _affordableRerollCostColor;

    public event Action OnColumnStopped;
    public event Action OnRerollStarted;

    void Start()
    {
        if (_slotMachine == null)
            _slotMachine = GetComponentInParent<SlotMachine>();

        PlaceCards();
        rerollButton.onClick.AddListener(OnRerollClicked);
        rerollButton.gameObject.SetActive(false);

        _affordableRerollCostColor = rerollCostText.color;
        UpdateRerollCostText(EnergyController.Instance.CurrentRerollCost);
        RefreshRerollInteractable();
        EnergyController.Instance.OnRerollCostChanged += HandleRerollCostChanged;
        EnergyController.Instance.OnEnergyChanged += HandleEnergyChanged;
    }

    void OnDestroy()
    {
        rerollButton.onClick.RemoveListener(OnRerollClicked);
        if (EnergyController.Instance != null)
        {
            EnergyController.Instance.OnRerollCostChanged -= HandleRerollCostChanged;
            EnergyController.Instance.OnEnergyChanged -= HandleEnergyChanged;
        }
    }

    private void HandleRerollCostChanged(int newCost)
    {
        UpdateRerollCostText(newCost);
        rerollCostChangeFeedback.PlayFeedbacks();
        RefreshRerollInteractable();
    }

    private void HandleEnergyChanged(int newEnergy)
    {
        RefreshRerollInteractable();
    }

    private void UpdateRerollCostText(int cost)
    {
        rerollCostText.text = cost.ToString();
    }

    private void RefreshRerollInteractable()
    {
        bool canAfford = EnergyController.Instance.CanAffordReroll;
        rerollButton.interactable = canAfford;
        rerollCostText.color = canAfford ? _affordableRerollCostColor : Color.red;
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
        if (!EnergyController.Instance.TrySpendReroll()) return;
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
