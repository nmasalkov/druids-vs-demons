using Game._Scripts.Global;
using UnityEngine;

/// <summary>
/// Generic instantiate/wait/cleanup dispatcher for whatever CampaignManager.CurrentEncounter
/// currently is — mirrors GameManager.Run(GameState) (docs/GameLoop.md): it doesn't know or care
/// what any individual Encounter does internally, only that it Plays and eventually fires
/// OnCompleted. Gets out of the way entirely for FightSO (EncounterSO.EncounterPrefab left unset —
/// the normal round loop plays as usual). Fully self-driving: no other script calls into this one.
/// See docs/Encounters.md.
///
/// Relies on RollStateManager.ActivateSlotMachine()'s FightSO gate to keep the round loop
/// genuinely inert while a pick screen is up — this overlay's raycast blocking alone would not be
/// enough (SlotMachine.Update() reads the keyboard directly, bypassing UI raycasts).
/// </summary>
public class EncounterPlayer : MonoBehaviour
{
    public static EncounterPlayer Instance { get; private set; }

    [SerializeField] private Transform encounterParent;

    private Encounter _activeEncounter;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Every soft encounter transition (advance, retry, debug restart) fires this — re-derive
        // the active Encounter from whatever CurrentEncounter now is, same "one broadcaster, many
        // independent subscribers" pattern as every other battle-scoped script (CLAUDE.md rule 3/16).
        GameManager.OnBattleRestart += RefreshForCurrentEncounter;
        RefreshForCurrentEncounter();
    }

    void OnDestroy()
    {
        GameManager.OnBattleRestart -= RefreshForCurrentEncounter;
    }

    public void RefreshForCurrentEncounter()
    {
        DestroyActiveEncounter();

        var encounterSO = CampaignManager.Instance.CurrentEncounter;
        // FightSO (EncounterPrefab unset) and an unassigned list slot (mid-edit in EncounterListSO,
        // encounterSO null) both mean "nothing to play here" — GameManager's loop handles a FightSO
        // itself; a null slot is normal content-authoring state, not a wiring bug (no rule-5 throw).
        if (encounterSO == null || encounterSO.EncounterPrefab == null) return;

        _activeEncounter = Instantiate(encounterSO.EncounterPrefab, encounterParent);
        _activeEncounter.OnCompleted += HandleEncounterCompleted;
        _activeEncounter.Play(encounterSO);
    }

    private void HandleEncounterCompleted()
    {
        DestroyActiveEncounter();
        CampaignManager.Instance.CompleteCurrentEncounter();
    }

    private void DestroyActiveEncounter()
    {
        if (_activeEncounter == null) return;
        _activeEncounter.OnCompleted -= HandleEncounterCompleted;
        Destroy(_activeEncounter.gameObject);
        _activeEncounter = null;
    }
}
