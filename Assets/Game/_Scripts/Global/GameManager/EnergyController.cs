using System;
using Game._Scripts.Global;
using UnityEngine;

/// <summary>
/// Owns the current per-reroll cost and drives the player's reroll energy pool. The pool itself
/// has exactly one source of truth — <see cref="CampaignStateManager"/>'s
/// <c>CurrentRun.currentEnergy</c> — so <see cref="CurrentEnergy"/> is a computed read-through,
/// never a locally cached field that could drift out of sync with it. Cost starts at
/// <see cref="baseRerollCost"/> and doubles on every successful reroll; it resets back to
/// <see cref="baseRerollCost"/> whenever a roll phase ends
/// (<see cref="RollStateManager.OnRollFinished"/>), so the next round's first reroll is cheap
/// again.
///
/// See docs/Energy.md. This component fully trusts CampaignStateManager to exist (CLAUDE.md
/// rule 5 — CampaignStateManager is cross-scene-persistent, already alive by the time BattleScene
/// finishes loading).
/// </summary>
public class EnergyController : MonoBehaviour
{
    public static EnergyController Instance { get; private set; }

    [SerializeField] private int baseRerollCost = 2;

    public int CurrentEnergy => CampaignStateManager.Instance.CurrentRun.currentEnergy;
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

    // Snapshot of CurrentEnergy taken at the start of the current encounter (ApplyCampaignEnergy)
    // — what ResetForRestart() reverts to on a plain restart (pause menu, defeat), undoing any
    // reroll spend from the abandoned attempt. Not persisted itself; only ever needs to survive
    // the current session. See docs/Energy.md.
    private int _encounterStartEnergy;

    void Awake()
    {
        Instance = this;
        // Only self-contained init here (rule 4) — CurrentRerollCost doesn't depend on anything
        // else. CurrentEnergy needs no init of its own: it reads RunState.currentEnergy live.
        CurrentRerollCost = baseRerollCost;
    }

    void Start()
    {
        GameManager.OnBattleRestart += ResetForRestart;
        RollStateManager.Instance.OnRollFinished += ResetRerollCost;
    }

    /// <summary>
    /// Called by CampaignStateManager whenever the scene needs to reflect the current RunState
    /// energy — the initial Start() and every encounter transition (soft or full scene reload). Captures
    /// the current value as the snapshot ResetForRestart() reverts to. Does not touch
    /// CurrentRerollCost.
    /// </summary>
    public void ApplyCampaignEnergy()
    {
        _encounterStartEnergy = CurrentEnergy;
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

        CampaignStateManager.Instance.CurrentRun.currentEnergy = CurrentEnergy - CurrentRerollCost;
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
    /// encounter (defeat, pause menu, debug tool). Reverts RunState.currentEnergy to
    /// _encounterStartEnergy, undoing any reroll spend from this attempt. A genuine encounter
    /// transition calls ApplyCampaignEnergy() again just before RestartBattle() fires this (see
    /// CampaignManager.LoadCurrentEncounter(), docs/Encounters.md), which refreshes the
    /// snapshot to the new encounter's carried-over value first — so this is a real revert only
    /// for a same-encounter restart, and a no-op (already-correct value) on a real transition.
    /// </summary>
    private void ResetForRestart()
    {
        CampaignStateManager.Instance.CurrentRun.currentEnergy = _encounterStartEnergy;
        OnEnergyChanged?.Invoke(CurrentEnergy);
        ResetRerollCost();
    }
}
