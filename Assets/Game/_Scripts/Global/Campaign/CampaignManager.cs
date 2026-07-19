using UnityEngine;

/// <summary>
/// Owns the current campaign RunState: loads/creates it, resolves its loadout ids against
/// GameCatalog, and applies the result to G (creature/nuke/spell pool) and EnergyController
/// (reroll energy capacity) at battle start. See docs/Campaign.md.
/// </summary>
public class CampaignManager : MonoBehaviour
{
    public static CampaignManager Instance { get; private set; }

    [SerializeField] private GameCatalog catalog;

    public RunState CurrentRun { get; private set; }
    public BattleSO CurrentBattle { get; private set; }

    public const string SaveKey = "DvD_RunState";

    void Awake()
    {
        Instance = this;
        CurrentRun = LoadOrCreateRunState();
    }

    void Start()
    {
        // CampaignManager's early Script Execution Order (see docs/Campaign.md) means this
        // Start() runs before every default-order Start() in the scene, including
        // EnergyController's and EnergyDisplay's — safe to apply directly. EnergyController
        // itself only ever sets its own baseline in Awake(), never in Start(), so there's
        // nothing here that could get clobbered by ordering either way.
        ApplyLoadoutToG();
        EnergyController.Instance.ApplyCampaignEnergyCapacity(CurrentRun.energyCapacity);
        ApplyEncounterToScene();
    }

    public void Save() => PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(CurrentRun));

    public void ResetRun()
    {
        CurrentRun = new RunState();
        Save();
    }

    /// <summary>
    /// Self-contained PlayerPrefs read shared with CampaignProgressManager's own bootstrap
    /// (which can't rely on CampaignManager.Instance existing yet — see docs/Encounters.md).
    /// </summary>
    public static RunState LoadOrCreateRunState() =>
        PlayerPrefs.HasKey(SaveKey)
            ? JsonUtility.FromJson<RunState>(PlayerPrefs.GetString(SaveKey))
            : new RunState();

    /// <summary>
    /// Swaps in the current encounter's enemy avatar/HP. Called once from Start() for the scene's
    /// initial load, and reused by CampaignProgressManager for an in-place "soft reload" when
    /// navigating to a new encounter without leaving BattleScene (see docs/Encounters.md) — public
    /// so it's callable from outside the Awake/Start pipeline.
    /// </summary>
    public void ApplyEncounterToScene()
    {
        if (CampaignProgressManager.Instance.CurrentEncounter is not BattleSO battle) return;
        CurrentBattle = battle;
        G.EnemyView.ReplaceHeroAvatar(battle.enemyData.enemyAvatarPrefab);
    }

    private void ApplyLoadoutToG()
    {
        var creatures = ScriptableObject.CreateInstance<CreaturesSO>();
        creatures.archer = catalog.FindArcher(CurrentRun.archerId) ?? FallbackWarning(G.DefaultCreatures.archer, CurrentRun.archerId, "archer");
        creatures.tank = catalog.FindTank(CurrentRun.tankId) ?? FallbackWarning(G.DefaultCreatures.tank, CurrentRun.tankId, "tank");
        creatures.mage = catalog.FindMage(CurrentRun.mageId) ?? FallbackWarning(G.DefaultCreatures.mage, CurrentRun.mageId, "mage");

        var nukes = ScriptableObject.CreateInstance<NukesSO>();
        nukes.nukeA = catalog.FindNuke(CurrentRun.nukeAId) ?? FallbackWarning(G.DefaultNukes.nukeA, CurrentRun.nukeAId, "nukeA");
        nukes.nukeB = catalog.FindNuke(CurrentRun.nukeBId) ?? FallbackWarning(G.DefaultNukes.nukeB, CurrentRun.nukeBId, "nukeB");
        nukes.nukeC = catalog.FindNuke(CurrentRun.nukeCId) ?? FallbackWarning(G.DefaultNukes.nukeC, CurrentRun.nukeCId, "nukeC");

        var spells = ScriptableObject.CreateInstance<SpellsSO>();
        spells.spellA = catalog.FindSpell(CurrentRun.spellAId) ?? FallbackWarning(G.DefaultSpells.spellA, CurrentRun.spellAId, "spellA");
        spells.spellB = catalog.FindSpell(CurrentRun.spellBId) ?? FallbackWarning(G.DefaultSpells.spellB, CurrentRun.spellBId, "spellB");
        spells.spellC = catalog.FindSpell(CurrentRun.spellCId) ?? FallbackWarning(G.DefaultSpells.spellC, CurrentRun.spellCId, "spellC");

        G.ApplyCampaignLoadout(creatures, nukes, spells);
    }

    private static T FallbackWarning<T>(T fallback, string id, string slotName) where T : ActionSO
    {
        Debug.LogWarning($"CampaignManager: no {slotName} found in GameCatalog for id \"{id}\" — falling back to G's default.");
        return fallback;
    }
}
