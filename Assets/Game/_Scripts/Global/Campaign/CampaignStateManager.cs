using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sole owner of RunState (the run's persisted data — loaded/created once here, in Awake, and
/// saved here) and the catalogs that resolve its ids back into assets (GameCatalog, RewardListSO).
/// Also applies that data to whatever scene needs it: resolves a run's loadout ids into G's
/// creature/nuke/spell pool and pushes reroll energy/enemy avatar into BattleScene. CampaignManager
/// (encounter switching/restart/defeat — see docs/Encounters.md) reads/mutates RunState through
/// this rather than owning any of it itself. See docs/Campaign.md.
///
/// Cross-scene-persistent (DontDestroyOnLoad + duplicate-guard, same pattern as CampaignManager —
/// see docs/Encounters.md): placed once on the CampaignProgress prefab, instanced as a root
/// GameObject in both BattleScene.unity and MapScene.unity, so either can be the session's
/// first-loaded scene. Debug-only surface (used solely by CampaignDebugTool/CampaignProgressTool)
/// lives in CampaignStateManager.Debug.cs — see CLAUDE.md rule 20.
/// </summary>
public partial class CampaignStateManager : MonoBehaviour
{
    public static CampaignStateManager Instance { get; private set; }

    [SerializeField] private GameCatalog catalog;
    [SerializeField] private RewardListSO rewardList;

    public RewardListSO RewardList => rewardList;
    public RunState CurrentRun { get; private set; }
    public FightSO CurrentFight { get; private set; }

    /// <summary>Player's max HP for this run: base RunState.maxHp plus every claimed HpBoostRewardSO's bonusHp (duplicates stack). See docs/Rewards.md.</summary>
    public int CurrentMaxHp => CurrentRun.maxHp + GetMaxHpBonus();

    public int GetMaxHpBonus()
    {
        int bonus = 0;
        foreach (var id in CurrentRun.statusRewardIds)
            if (rewardList.Find(id) is HpBoostRewardSO hpBoost) bonus += hpBoost.bonusHp;
        return bonus;
    }

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CurrentRun = LoadOrCreateRunState();
        _overrideWindowOpen = true;
    }

    void Start()
    {
        // Closes the override window (see CampaignStateManager.Debug.cs). Since this object is
        // DontDestroyOnLoad, Start() only ever runs once per session (a scene reload's fresh copy
        // of the placed GameObject destroys itself in Awake() before ever reaching Start()) —
        // everything after this point is real gameplay, never a debug override reapplying itself.
        _overrideWindowOpen = false;

        // Every later BattleScene load (soft transition from MapScene, or a manual reload) fires
        // this — sceneLoaded is guaranteed to run after every GameObject's Awake() in that scene
        // but before any of their Start()s, so this is always safe to apply from. Handles the boot
        // case (session's first-loaded scene is already BattleScene) directly below instead, since
        // sceneLoaded never fires for a scene that was already loaded before this object woke up.
        SceneManager.sceneLoaded += HandleSceneLoaded;
        if (SceneManager.GetActiveScene().name == SceneNames.BattleScene) EnterBattleScene();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneNames.BattleScene) EnterBattleScene();
    }

    /// <summary>
    /// Boot-time/every-load redirect: if BattleScene was reached while the current encounter is
    /// actually a pick screen, bounce to MapScene instead of applying anything — BattleScene is
    /// only ever meant to host a FightSO outside the debug ProcessAllEncountersInBattleScene flag.
    /// Otherwise applies the resolved loadout and current encounter's data to this scene's objects.
    /// See docs/Encounters.md.
    /// </summary>
    private void EnterBattleScene()
    {
        if (!CampaignManager.Instance.ProcessAllEncountersInBattleScene &&
            CampaignManager.Instance.CurrentEncounter is not FightSO)
        {
            SceneManager.LoadScene(SceneNames.MapScene);
            return;
        }

        ApplyLoadoutToG();
        ApplyEncounterToScene();
    }

    /// <summary>
    /// Applies the current encounter's per-encounter run state to the scene: reroll energy (which
    /// may have just changed via a victory reward) and the enemy avatar/HP. Called once from
    /// EnterBattleScene() for a scene's initial load, and reused by CampaignManager for an in-place
    /// "soft reload" when navigating to a new encounter without leaving BattleScene (see
    /// docs/Encounters.md) — public so it's callable from outside this object's own lifecycle.
    /// </summary>
    public void ApplyEncounterToScene()
    {
        EnergyController.Instance.ApplyCampaignEnergy();

        if (CampaignManager.Instance.CurrentEncounter is not FightSO fight) return;
        CurrentFight = fight;
        G.EnemyView.ReplaceHeroAvatar(fight.enemyData.enemyAvatarPrefab);
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
        Debug.LogWarning($"CampaignStateManager: no {slotName} found in GameCatalog for id \"{id}\" — falling back to G's default.");
        return fallback;
    }

    /// <summary>
    /// The sole write path for RunState persistence.
    /// </summary>
    public void Save() => SaveStorage.Backend.Write(JsonUtility.ToJson(CurrentRun));

    /// <summary>
    /// Replaces RunState wholesale — used by CampaignManager.StartNewRun() and by
    /// CampaignDebugTool's "Use External Save"/"Use Debug Profile" overrides. Never called with
    /// Save() implied — callers that need persistence call Save() themselves; debug overrides
    /// deliberately never do.
    /// </summary>
    public void ReplaceRunState(RunState run) => CurrentRun = run;

    /// <summary>
    /// Self-contained storage read — the sole loader of RunState in the codebase. Creates and
    /// immediately persists a fresh RunState the first time this runs with no save present, or
    /// whenever the existing save is unparseable/an unsupported version — this is what makes a
    /// save slot exist from the very first launch, and what the "clear it and start a fresh run"
    /// requirement resolves to. See docs/Campaign.md's Save system section.
    /// </summary>
    public static RunState LoadOrCreateRunState()
    {
        if (!SaveStorage.Backend.Exists()) return CreateAndPersistFreshRun();

        var run = ParseRunState(SaveStorage.Backend.Read());
        return run ?? CreateAndPersistFreshRun();
    }

    private static RunState CreateAndPersistFreshRun()
    {
        var run = new RunState();
        SaveStorage.Backend.Write(JsonUtility.ToJson(run));
        return run;
    }

    /// <summary>
    /// Parses a saved RunState JSON blob, rejecting anything unparseable or whose saveVersion
    /// doesn't match RunState.CurrentSaveVersion — returns null (with a logged warning) rather
    /// than throwing, so a corrupt or future-versioned save can never crash the game. Full save
    /// migration is out of scope; an unsupported version is simply treated the same as no save.
    /// </summary>
    private static RunState ParseRunState(string json)
    {
        RunState run;
        try
        {
            run = JsonUtility.FromJson<RunState>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"CampaignStateManager: saved run JSON failed to parse ({e.Message}); starting a new run.");
            return null;
        }

        if (run == null || run.saveVersion != RunState.CurrentSaveVersion)
        {
            Debug.LogWarning($"CampaignStateManager: saved run has unsupported version " +
                $"({(run != null ? run.saveVersion.ToString() : "none")}, expected {RunState.CurrentSaveVersion}); starting a new run.");
            return null;
        }

        return run;
    }
}
