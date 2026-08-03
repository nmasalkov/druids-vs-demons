using System;
using UnityEngine;

/// <summary>
/// Base for anything EncounterPlayer instantiates/plays for a non-fight EncounterSO (LoadoutPickSO,
/// RewardPickSO, ...) — mirrors GameState's "one runner, self-contained states" shape (see
/// docs/GameLoop.md), except a MonoBehaviour since each Encounter owns its own scene UI. Each
/// subclass owns its own presentation directly, or — for anything with real RunState mutation and a
/// non-trivial completion condition — splits into this class as a headless-testable backend plus a
/// paired "<Subclass>View" MonoBehaviour that owns UI and forwards clicks as method calls (CLAUDE.md
/// rule 28; see RewardEncounter/RewardEncounterView in docs/Rewards.md). EncounterPlayer/MapManager
/// only ever call Play() and wait for OnCompleted. See docs/Encounters.md.
/// </summary>
public abstract class Encounter : MonoBehaviour
{
    public event Action OnCompleted;

    /// <summary>
    /// Plain runtime property, never [SerializeField] — a real prefab can never ship with this
    /// accidentally on. Only test code sets it true, before calling Play() on a bare, view-less
    /// instance, so a subclass's confirm-style method self-completes immediately instead of waiting
    /// for a View to call CompletePresentation(). See CLAUDE.md rule 28.
    /// </summary>
    public bool Headless { get; set; }

    public abstract void Play(EncounterSO data);

    protected void Complete() => OnCompleted?.Invoke();

    /// <summary>
    /// Public wrapper around Complete() so a paired View (a sibling component, not a subclass) can
    /// trigger completion once its presentation (e.g. a discard animation) finishes. See rule 28.
    /// </summary>
    public void CompletePresentation() => Complete();
}
