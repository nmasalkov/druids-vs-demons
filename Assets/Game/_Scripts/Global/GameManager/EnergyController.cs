using System;
using Game._Scripts.Global;
using UnityEngine;

/// <summary>
/// Owns the player's reroll energy pool and the current per-reroll cost. Cost starts at
/// <see cref="baseRerollCost"/> and doubles on every successful reroll; it resets back to
/// <see cref="baseRerollCost"/> whenever a roll phase ends (<see cref="RollStateManager.OnRollFinished"/>),
/// so the next round's first reroll is cheap again.
/// </summary>
public class EnergyController : MonoBehaviour
{
    public static EnergyController Instance { get; private set; }

    [SerializeField] private int startingEnergy = 50;
    [SerializeField] private int baseRerollCost = 2;

    // Set once in Start() to startingEnergy, then possibly overridden by CampaignManager's
    // energy capacity via ApplyCampaignEnergyCapacity(). ResetForRestart() refills to this
    // (not the raw serialized field) so a mid-campaign restart refills to the run's capacity.
    private int _effectiveStartingEnergy;

    public int CurrentEnergy { get; private set; }
    public int CurrentRerollCost { get; private set; }

    public bool CanAffordReroll => CurrentEnergy >= CurrentRerollCost;

    public event Action<int> OnEnergyChanged;
    public event Action<int> OnRerollCostChanged;

    void Awake()
    {
        Instance = this;
        // Must happen here, not Start(): SlotColumn.Start() reads CurrentRerollCost
        // synchronously in its own Start() to seed the reroll-cost label, and Start()-vs-Start()
        // order between unrelated components is unspecified — only the Awake-before-any-Start
        // guarantee is reliable. (A prior version of this method set these in Start() instead,
        // which intermittently left the label showing 0 depending on component order — caught
        // live during testing.)
        CurrentEnergy = _effectiveStartingEnergy = startingEnergy;
        CurrentRerollCost = baseRerollCost;
    }

    void Start()
    {
        GameManager.OnBattleRestart += ResetForRestart;
        RollStateManager.Instance.OnRollFinished += ResetRerollCost;
    }

    /// <summary>
    /// Called by CampaignManager.Start() to override this run's reroll energy capacity.
    /// CampaignManager's early Script Execution Order means its Start() runs before this
    /// component's Start() and before any default-order consumer's Start() (e.g.
    /// EnergyDisplay) — but the OnEnergyChanged fire below also makes this correct even if that
    /// ordering ever changes, since anything already showing the old value self-corrects.
    /// </summary>
    public void ApplyCampaignEnergyCapacity(int amount)
    {
        CurrentEnergy = _effectiveStartingEnergy = amount;
        OnEnergyChanged?.Invoke(CurrentEnergy);
    }

    void OnDestroy()
    {
        GameManager.OnBattleRestart -= ResetForRestart;
        if (RollStateManager.Instance != null)
            RollStateManager.Instance.OnRollFinished -= ResetRerollCost;
    }

    /// <summary>
    /// Spends energy for a reroll at the current cost and doubles the cost for the next one.
    /// Returns false (and changes nothing) if the player can't afford it.
    /// </summary>
    public bool TrySpendReroll()
    {
        if (!CanAffordReroll) return false;

        CurrentEnergy -= CurrentRerollCost;
        OnEnergyChanged?.Invoke(CurrentEnergy);

        CurrentRerollCost *= 2;
        OnRerollCostChanged?.Invoke(CurrentRerollCost);

        return true;
    }

    private void ResetRerollCost()
    {
        CurrentRerollCost = baseRerollCost;
        OnRerollCostChanged?.Invoke(CurrentRerollCost);
    }

    private void ResetForRestart()
    {
        CurrentEnergy = _effectiveStartingEnergy;
        OnEnergyChanged?.Invoke(CurrentEnergy);
        ResetRerollCost();
    }
}
