using UnityEngine;

/// <summary>
/// Debug-only surface, called solely by CampaignProgressTool/CampaignDebugTool — kept out of
/// CampaignManager.cs so that file stays pure game-navigation logic. See CLAUDE.md rule 20.
/// </summary>
public partial class CampaignManager
{
    [Tooltip("Debug/automation escape hatch: keeps the entire campaign in BattleScene, exactly like " +
        "before MapScene existed — MapScene never loads. Needed so headless balance-testing " +
        "(CLAUDE.md rule 7) doesn't depend on MapScene's visual reveal sequence.")]
    [SerializeField] private bool processAllEncountersInBattleScene;
    public bool ProcessAllEncountersInBattleScene => processAllEncountersInBattleScene;

    /// <summary>
    /// Session-only override used by CampaignProgressTool — never persisted, mirrors
    /// CampaignDebugTool's overrides never calling Save(). Resolves the EncounterSO against this
    /// object's own EncounterList (CampaignStateManager has no EncounterList reference), then
    /// delegates the actual RunState write to CampaignStateManager.
    /// </summary>
    public void SetSessionEncounterOverride(EncounterSO encounter)
    {
        var idx = encounterList.encounters.IndexOf(encounter);
        if (idx < 0)
        {
            Debug.LogWarning($"CampaignManager: {(encounter != null ? encounter.name : "null")} not found in encounter list.");
            return;
        }

        SetSessionEncounterIndexOverride(idx);
    }

    /// <summary>
    /// Same session-only override as SetSessionEncounterOverride, but by raw index — used by
    /// CampaignDebugTool's "Use Debug Profile" (CampaignProfileSO.currentEncounterIndex), which has
    /// no EncounterListSO reference to resolve an EncounterSO through. Clamps against this object's
    /// own EncounterList, then hands the already-valid index to CampaignStateManager (the RunState
    /// owner) to actually apply — see CampaignStateManager.ApplySessionEncounterIndexOverride().
    /// </summary>
    public void SetSessionEncounterIndexOverride(int index)
    {
        int clamped = Mathf.Clamp(index, 0, encounterList.encounters.Count - 1);
        CampaignStateManager.Instance.ApplySessionEncounterIndexOverride(clamped);
    }
}
