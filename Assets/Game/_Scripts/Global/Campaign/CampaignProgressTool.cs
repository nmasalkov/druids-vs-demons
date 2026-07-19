using UnityEngine;

/// <summary>
/// Editor-only override tool for CampaignProgressManager, mirroring CampaignDebugTool's pattern:
/// check a box, drag in an EncounterSO, press Play — CampaignProgressManager loads that encounter
/// instead of resuming from RunState.currentEncounterIndex. Never persists — session-only, same
/// as CampaignDebugTool's overrides. See docs/Encounters.md.
///
/// Requires CampaignProgressManager's Awake() to run before this one (Script Execution Order —
/// see the Editor setup checklist in docs/Encounters.md), the same class of Awake-vs-Awake
/// ordering CampaignDebugTool already relies on for CampaignManager.
/// </summary>
public class CampaignProgressTool : MonoBehaviour
{
    [Header("Encounter Override")]
    public bool overrideEncounter;
    public EncounterSO encounter;

    void Awake()
    {
        if (!overrideEncounter) return;
        if (CampaignProgressManager.Instance == null) return;

        CampaignProgressManager.Instance.SetSessionEncounterOverride(encounter);
    }
}
