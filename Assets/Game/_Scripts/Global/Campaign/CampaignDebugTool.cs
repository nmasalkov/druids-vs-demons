using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Editor-only override tool for CampaignStateManager's RunState, mirroring BalanceTool's pattern
/// (see BalanceTool.cs): check a box, drag in a creature/nuke/spell asset (or set a number) in
/// the Inspector, press Play. Never calls CampaignStateManager.Save() — overrides are in-memory-only
/// for the current session, so testing never corrupts a real saved run. See docs/Campaign.md.
///
/// "Use Debug Profile" is a coarser mechanism: instead of checking individual fields, drag in one
/// CampaignProfileSO and every RunState field is set from it wholesale (the granular overrides
/// below are ignored while it's checked). "Use External Save" is coarser still: paste a whole
/// RunState JSON blob and it becomes CurrentRun for the session, parsed through the exact same
/// validation a real save goes through. Precedence: External Save > Debug Profile > granular
/// overrides — see docs/Campaign.md for when to use which.
///
/// Cross-scene-persistent (DontDestroyOnLoad + duplicate-guard, same pattern as
/// CampaignStateManager — see docs/Encounters.md): placed once in each of BattleScene/MapScene so
/// overrides apply correctly no matter which scene the session actually boots from; whichever loads
/// first survives, the other's copy self-destructs in Awake().
/// </summary>
public class CampaignDebugTool : MonoBehaviour
{
    public static CampaignDebugTool Instance { get; private set; }

    [Header("Use External Save (overrides everything below)")]
    public bool useExternalSave;
    [TextArea(6, 20)]
    public string externalSaveJson;

    [Header("Use Debug Profile (overrides everything below except External Save)")]
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

    [Header("Rewards")]
    public bool overrideStatusRewards;
    public List<StatusRewardSO> statusRewards = new List<StatusRewardSO>();
    public bool overrideBoostRewards;
    public List<BoostSO> boostRewards = new List<BoostSO>();
    public bool overrideGatheredCreatures;
    public List<CreatureSO> gatheredCreatures = new List<CreatureSO>();
    public bool overrideGatheredNukes;
    public List<NukeSO> gatheredNukes = new List<NukeSO>();
    public bool overrideGatheredSpells;
    public List<SpellSO> gatheredSpells = new List<SpellSO>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Must run after CampaignStateManager.Awake() (which creates RunState) and before
        // anything reads it (Hero.Start()'s InitHealth() reads maxHp immediately) — enforced via
        // Script Execution Order, see the Editor setup checklist in docs/Campaign.md.
        if (CampaignStateManager.Instance == null) return;

        if (useExternalSave && !string.IsNullOrEmpty(externalSaveJson))
        {
            ApplyRunStateOverride(CampaignStateManager.ParseExternalRunState(externalSaveJson), "external save JSON");
            return;
        }

        if (useDebugProfile && debugProfile != null)
        {
            ApplyRunStateOverride(BuildRunStateFromProfile(debugProfile), "debug profile");
            return;
        }

        var run = CampaignStateManager.Instance.CurrentRun;

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
        if (overrideStatusRewards) run.statusRewardIds = statusRewards.Select(s => s.id).ToList();
        if (overrideBoostRewards) run.boostRewardIds = boostRewards.Select(b => b.id).ToList();
        if (overrideGatheredCreatures) run.gatheredCreatureIds = gatheredCreatures.Select(c => c.id).ToList();
        if (overrideGatheredNukes) run.gatheredNukeIds = gatheredNukes.Select(n => n.id).ToList();
        if (overrideGatheredSpells) run.gatheredSpellIds = gatheredSpells.Select(s => s.id).ToList();
    }

    /// <summary>
    /// Builds a whole RunState from a CampaignProfileSO — every field, no granular toggles.
    /// Profile fields are treated as mandatory (rule 5): an unassigned archer/tank/mage/nuke/spell
    /// throws immediately rather than silently resolving to null. Also used by
    /// CampaignDebugToolEditor's "Generate Save JSON from Profile" button, so the round-trip
    /// (profile → JSON → pasted into "Use External Save") exercises the exact same field mapping
    /// "Use Debug Profile" itself uses.
    /// </summary>
    public static RunState BuildRunStateFromProfile(CampaignProfileSO profile)
    {
        return new RunState
        {
            maxHp = profile.maxHp,
            energyCapacity = profile.energyCapacity,
            currentEnergy = profile.currentEnergy,
            archerId = profile.archer.id,
            tankId = profile.tank.id,
            mageId = profile.mage.id,
            nukeAId = profile.nukeA.id,
            nukeBId = profile.nukeB.id,
            nukeCId = profile.nukeC.id,
            spellAId = profile.spellA.id,
            spellBId = profile.spellB.id,
            spellCId = profile.spellC.id,
            currentEncounterIndex = profile.currentEncounterIndex,
            statusRewardIds = profile.statusRewards.Select(s => s.id).ToList(),
            boostRewardIds = profile.boostRewards.Select(b => b.id).ToList(),
            gatheredCreatureIds = profile.gatheredCreatures.Select(c => c.id).ToList(),
            gatheredNukeIds = profile.gatheredNukes.Select(n => n.id).ToList(),
            gatheredSpellIds = profile.gatheredSpells.Select(s => s.id).ToList(),
        };
    }

    /// <summary>
    /// Builds a CampaignProfileSO snapshot from a RunState — the reverse of
    /// BuildRunStateFromProfile, used by CampaignDebugToolEditor's "Export to Debug Profile"
    /// button to capture a real (saved or live) run as a reusable test scenario asset. Resolves
    /// each id back into a direct SO reference via the given GameCatalog/RewardListSO; an id that
    /// doesn't resolve (stale/removed content) is left null with a logged warning instead of
    /// throwing — this runs against real save data, not authored Editor content, so it can't
    /// assume every id is still valid the way BuildRunStateFromProfile assumes a profile's direct
    /// references are (rule 5 doesn't apply here for the same reason GameCatalog.Find* itself
    /// doesn't throw on a missing id — see docs/Campaign.md Gotchas).
    /// </summary>
    public static CampaignProfileSO BuildProfileFromRunState(RunState run, GameCatalog catalog, RewardListSO rewardList)
    {
        var profile = ScriptableObject.CreateInstance<CampaignProfileSO>();
        profile.maxHp = run.maxHp;
        profile.energyCapacity = run.energyCapacity;
        profile.currentEnergy = run.currentEnergy;
        profile.archer = ResolveOrWarn(catalog.FindArcher(run.archerId), run.archerId, "archer");
        profile.tank = ResolveOrWarn(catalog.FindTank(run.tankId), run.tankId, "tank");
        profile.mage = ResolveOrWarn(catalog.FindMage(run.mageId), run.mageId, "mage");
        profile.nukeA = ResolveOrWarn(catalog.FindNuke(run.nukeAId), run.nukeAId, "nukeA");
        profile.nukeB = ResolveOrWarn(catalog.FindNuke(run.nukeBId), run.nukeBId, "nukeB");
        profile.nukeC = ResolveOrWarn(catalog.FindNuke(run.nukeCId), run.nukeCId, "nukeC");
        profile.spellA = ResolveOrWarn(catalog.FindSpell(run.spellAId), run.spellAId, "spellA");
        profile.spellB = ResolveOrWarn(catalog.FindSpell(run.spellBId), run.spellBId, "spellB");
        profile.spellC = ResolveOrWarn(catalog.FindSpell(run.spellCId), run.spellCId, "spellC");
        profile.currentEncounterIndex = run.currentEncounterIndex;
        profile.statusRewards = run.statusRewardIds
            .Select(id => ResolveOrWarn(rewardList.Find(id) as StatusRewardSO, id, "status reward"))
            .Where(r => r != null).ToList();
        profile.boostRewards = run.boostRewardIds
            .Select(id => ResolveOrWarn(rewardList.Find(id) as BoostSO, id, "boost reward"))
            .Where(r => r != null).ToList();
        profile.gatheredCreatures = run.gatheredCreatureIds
            .Select(id => ResolveOrWarn(catalog.allCreatures.FirstOrDefault(c => c.id == id), id, "gathered creature"))
            .Where(c => c != null).ToList();
        profile.gatheredNukes = run.gatheredNukeIds
            .Select(id => ResolveOrWarn(catalog.FindNuke(id), id, "gathered nuke"))
            .Where(n => n != null).ToList();
        profile.gatheredSpells = run.gatheredSpellIds
            .Select(id => ResolveOrWarn(catalog.FindSpell(id), id, "gathered spell"))
            .Where(s => s != null).ToList();
        return profile;
    }

    private static T ResolveOrWarn<T>(T resolved, string id, string slotName) where T : class
    {
        if (resolved == null)
            Debug.LogWarning($"CampaignDebugTool: no {slotName} found for id \"{id}\" while exporting to profile — leaving unset.");
        return resolved;
    }

    /// <summary>
    /// Shared tail end of both "Use External Save" and "Use Debug Profile": replaces CurrentRun
    /// wholesale and relocates CampaignManager's independently-bootstrapped encounter index to
    /// match (see SetSessionEncounterIndexOverride's doc comment for why that's needed — mutating
    /// run.currentEncounterIndex alone has no effect on which encounter actually loads). A null
    /// run (parse failure/unsupported version) is ignored with a warning instead of clearing
    /// CurrentRun.
    /// </summary>
    private static void ApplyRunStateOverride(RunState run, string sourceLabel)
    {
        if (run == null)
        {
            Debug.LogWarning($"CampaignDebugTool: {sourceLabel} was invalid or an unsupported version — ignoring.");
            return;
        }

        CampaignStateManager.Instance.ReplaceRunState(run);

        if (CampaignManager.Instance != null)
            CampaignManager.Instance.SetSessionEncounterIndexOverride(run.currentEncounterIndex);
    }
}
