using System;
using Game._Scripts.Global;
using UnityEngine;

/// <summary>
/// Owns the player's reroll energy pool and the current per-reroll cost. Cost starts at
/// <see cref="baseRerollCost"/> and doubles on every successful reroll; it resets back to
/// <see cref="baseRerollCost"/> whenever a roll phase ends (<see cref="RollStateManager.OnRollFinished"/>),
/// so the next round's first reroll is cheap again.
///
/// The energy pool itself is campaign-persistent, not a fixed per-battle baseline (see
/// docs/Energy.md) — CurrentEnergy always comes from RunState.currentEnergy via
/// ApplyCampaignEnergy()/ResetForRestart(), never a locally serialized default. This component
/// fully trusts CampaignManager to exist (CLAUDE.md rule 5 — both live on the same
/// Global/GameManager GameObject).
/// </summary>
public class EnergyController : MonoBehaviour
{
    public static EnergyController Instance { get; private set; }

    [SerializeField] private int baseRerollCost = 2;

    public int CurrentEnergy { get; private set; }
    public int CurrentRerollCost { get; private set; }

    public bool CanAffordReroll => CurrentEnergy >= CurrentRerollCost;

    public event Action<int> OnEnergyChanged;
    public event Action<int> OnRerollCostChanged;

    /// <summary>
    /// Fired only when the player actually spends energy on a reroll — separate from
    /// OnEnergyChanged so UI feedback (the HUD "shake") plays for real spending only, not for
    /// campaign-driven value updates (initial load, encounter transition, restart) that also fire
    /// OnEnergyChanged. See docs/Energy.md.
    /// </summary>
    public event Action OnEnergySpent;

    void Awake()
    {
        Instance = this;
        // Only self-contained init here (rule 4) — CurrentRerollCost doesn't depend on anything
        // else, but CurrentEnergy does (RunState, via CampaignManager) so it's left at its
        // default until ApplyCampaignEnergy() is called from CampaignManager.Start() (SEO -100,
        // guaranteed to run before this component's own Start() or any consumer's, e.g.
        // EnergyDisplay/SlotColumn — see docs/Campaign.md).
        CurrentRerollCost = baseRerollCost;
    }

    void Start()
    {
        GameManager.OnBattleRestart += ResetForRestart;
        RollStateManager.Instance.OnRollFinished += ResetRerollCost;
    }

    /// <summary>
    /// Called by CampaignManager whenever the scene needs to reflect the current RunState energy
    /// — the initial Start() and every encounter transition (soft or full scene reload). Does not
    /// touch CurrentRerollCost.
    /// </summary>
    public void ApplyCampaignEnergy(int amount)
    {
        CurrentEnergy = amount;
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
        OnEnergySpent?.Invoke();

        CurrentRerollCost *= 2;
        OnRerollCostChanged?.Invoke(CurrentRerollCost);

        return true;
    }

    private void ResetRerollCost()
    {
        CurrentRerollCost = baseRerollCost;
        OnRerollCostChanged?.Invoke(CurrentRerollCost);
    }

    /// <summary>
    /// Fired on every GameManager.OnBattleRestart — including a restart of the *current*
    /// encounter (defeat, pause menu, debug tool). Reads RunState.currentEnergy fresh rather than
    /// a cached field: that value only ever changes via a victory reward (CampaignProgressManager.
    /// ResolveVictory), never during a restart, so re-reading it here is exactly "restore the
    /// energy this encounter started with" — see docs/Encounters.md.
    /// </summary>
    private void ResetForRestart()
    {
        CurrentEnergy = CampaignManager.Instance.CurrentRun.currentEnergy;
        OnEnergyChanged?.Invoke(CurrentEnergy);
        ResetRerollCost();
    }
}
