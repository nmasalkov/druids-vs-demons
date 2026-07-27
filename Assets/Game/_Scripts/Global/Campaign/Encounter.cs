using System;
using UnityEngine;

/// <summary>
/// Base for anything EncounterPlayer instantiates/plays for a non-fight EncounterSO (LoadoutPickSO,
/// RewardPickSO, ...) — mirrors GameState's "one runner, self-contained states" shape (see
/// docs/GameLoop.md), except a MonoBehaviour since each Encounter owns its own scene UI. Each
/// subclass owns its own presentation entirely; EncounterPlayer only ever calls Play() and waits
/// for OnCompleted. See docs/Encounters.md.
/// </summary>
public abstract class Encounter : MonoBehaviour
{
    public event Action OnCompleted;

    public abstract void Play(EncounterSO data);

    protected void Complete() => OnCompleted?.Invoke();
}
