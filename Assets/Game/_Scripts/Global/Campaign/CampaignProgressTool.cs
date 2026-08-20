using UnityEngine;

/// <summary>
/// Editor-only override tool for CampaignManager, mirroring CampaignDebugTool's pattern:
/// check a box, drag in a FightSO, press Play — CampaignManager loads that encounter
/// instead of resuming from RunState.currentEncounterIndex. Never persists — session-only, same
/// as CampaignDebugTool's overrides. See docs/Encounters.md.
///
/// Requires CampaignManager's and CampaignStateManager's Awake() to both run before this one
/// (Script Execution Order — see the Editor setup checklist in docs/Encounters.md), the same
/// class of Awake-vs-Awake ordering CampaignDebugTool already relies on for CampaignStateManager.
/// </summary>
public class CampaignProgressTool : MonoBehaviour
{
    [Header("Encounter Override")]
    public bool overrideEncounter;
    public FightSO encounter;

    void Awake()
    {
        if (!overrideEncounter) return;
        if (CampaignManager.Instance == null) return;

        CampaignManager.Instance.SetSessionEncounterOverride(encounter);
    }
}
