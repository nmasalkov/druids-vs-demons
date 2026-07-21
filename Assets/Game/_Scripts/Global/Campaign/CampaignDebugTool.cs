using UnityEngine;

/// <summary>
/// Editor-only override tool for CampaignManager's RunState, mirroring BalanceTool's pattern
/// (see BalanceTool.cs): check a box, drag in a creature/nuke/spell asset (or set a number) in
/// the Inspector, press Play. Never calls CampaignManager.Save() — overrides are in-memory-only
/// for the current session, so testing never corrupts a real saved run. See docs/Campaign.md.
///
/// "Use Debug Profile" is a separate, coarser mechanism: instead of checking individual fields,
/// drag in one CampaignProfileSO and every RunState field is set from it wholesale (the granular
/// overrides below are ignored while it's checked) — see docs/Campaign.md for when to use which.
/// </summary>
public class CampaignDebugTool : MonoBehaviour
{
    [Header("Use Debug Profile (overrides everything below)")]
    public bool useDebugProfile;
    public CampaignProfileSO debugProfile;

    [Header("Max HP")]
    public bool overrideMaxHp;
    public int maxHp = 100;

    [Header("Energy Capacity")]
    public bool overrideEnergyCapacity;
    public int energyCapacity = 50;

    [Header("Current Energy")]
    public bool overrideCurrentEnergy;
    public int currentEnergy = 50;

    [Header("Creatures")]
    public bool overrideArcher;
    public ArcherSO archer;
    public bool overrideTank;
    public TankSO tank;
    public bool overrideMage;
    public MageSO mage;

    [Header("Nukes")]
    public bool overrideNukeA;
    public NukeSO nukeA;
    public bool overrideNukeB;
    public NukeSO nukeB;
    public bool overrideNukeC;
    public NukeSO nukeC;

    [Header("Spells")]
    public bool overrideSpellA;
    public SpellSO spellA;
    public bool overrideSpellB;
    public SpellSO spellB;
    public bool overrideSpellC;
    public SpellSO spellC;

    void Awake()
    {
        // Must run after CampaignManager.Awake() (which creates CurrentRun) and before anything
        // reads it (Hero.Start()'s InitHealth() reads maxHp immediately) — enforced via Script
        // Execution Order, see the Editor setup checklist in docs/Campaign.md.
        if (CampaignManager.Instance == null) return;

        var run = CampaignManager.Instance.CurrentRun;

        if (useDebugProfile && debugProfile != null)
        {
            ApplyDebugProfile(run, debugProfile);
            return;
        }

        if (overrideMaxHp) run.maxHp = maxHp;
        if (overrideEnergyCapacity) run.energyCapacity = energyCapacity;
        if (overrideCurrentEnergy) run.currentEnergy = currentEnergy;
        if (overrideArcher) run.archerId = archer.id;
        if (overrideTank) run.tankId = tank.id;
        if (overrideMage) run.mageId = mage.id;
        if (overrideNukeA) run.nukeAId = nukeA.id;
        if (overrideNukeB) run.nukeBId = nukeB.id;
        if (overrideNukeC) run.nukeCId = nukeC.id;
        if (overrideSpellA) run.spellAId = spellA.id;
        if (overrideSpellB) run.spellBId = spellB.id;
        if (overrideSpellC) run.spellCId = spellC.id;
    }

    /// <summary>
    /// Wholesale RunState replacement from a CampaignProfileSO — every field, no granular
    /// toggles. Profile fields are treated as mandatory once useDebugProfile is checked (rule 5):
    /// an unassigned archer/tank/mage/nuke/spell throws immediately rather than silently
    /// resolving to null.
    ///
    /// currentEncounterIndex additionally needs CampaignProgressManager.SetSessionEncounterIndexOverride
    /// — CampaignProgressManager bootstraps its own index from PlayerPrefs independently of
    /// CurrentRun (see docs/Encounters.md), so mutating run.currentEncounterIndex alone would have
    /// no effect on which encounter actually loads this session.
    /// </summary>
    private static void ApplyDebugProfile(RunState run, CampaignProfileSO profile)
    {
        run.maxHp = profile.maxHp;
        run.energyCapacity = profile.energyCapacity;
        run.currentEnergy = profile.currentEnergy;
        run.archerId = profile.archer.id;
        run.tankId = profile.tank.id;
        run.mageId = profile.mage.id;
        run.nukeAId = profile.nukeA.id;
        run.nukeBId = profile.nukeB.id;
        run.nukeCId = profile.nukeC.id;
        run.spellAId = profile.spellA.id;
        run.spellBId = profile.spellB.id;
        run.spellCId = profile.spellC.id;
        run.currentEncounterIndex = profile.currentEncounterIndex;

        if (CampaignProgressManager.Instance != null)
            CampaignProgressManager.Instance.SetSessionEncounterIndexOverride(profile.currentEncounterIndex);
    }
}
