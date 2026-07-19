using UnityEngine;

/// <summary>
/// Editor-only override tool for CampaignManager's RunState, mirroring BalanceTool's pattern
/// (see BalanceTool.cs): check a box, drag in a creature/nuke/spell asset (or set a number) in
/// the Inspector, press Play. Never calls CampaignManager.Save() — overrides are in-memory-only
/// for the current session, so testing never corrupts a real saved run. See docs/Campaign.md.
/// </summary>
public class CampaignDebugTool : MonoBehaviour
{
    [Header("Max HP")]
    public bool overrideMaxHp;
    public int maxHp = 100;

    [Header("Energy Capacity")]
    public bool overrideEnergyCapacity;
    public int energyCapacity = 50;

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
        if (overrideMaxHp) run.maxHp = maxHp;
        if (overrideEnergyCapacity) run.energyCapacity = energyCapacity;
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
}
