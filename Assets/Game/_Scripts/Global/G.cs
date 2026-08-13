using Game._Scripts.Creatures;
using Game._Scripts.PlayerView;
using UnityEngine;
using UnityEngine.InputSystem;

public class G : MonoBehaviour
{
    public static G Instance { get; private set; }

    [SerializeField] private CreaturesSO defaultCreatures;
    [SerializeField] private NukesSO defaultNukes;
    [SerializeField] private SpellsSO defaultSpells;
    [SerializeField] private CreaturesSO enemyCreatures;
    [SerializeField] private NukesSO enemyNukes;
    [SerializeField] private SpellsSO enemySpells;
    [SerializeField] private HeroView playerView;
    [SerializeField] private HeroView enemyView;
    [SerializeField] private bool testMode = true;

    public static CreaturesSO DefaultCreatures => Instance.defaultCreatures;
    public static NukesSO DefaultNukes => Instance.defaultNukes;
    public static SpellsSO DefaultSpells => Instance.defaultSpells;
    public static CreaturesSO EnemyCreatures => Instance.enemyCreatures;
    public static NukesSO EnemyNukes => Instance.enemyNukes;
    public static SpellsSO EnemySpells => Instance.enemySpells;
    public static RewardListSO RewardList => CampaignStateManager.Instance.RewardList;
    public static HeroView PlayerView => Instance.playerView;
    public static HeroView EnemyView => Instance.enemyView;
    public static CreaturesManager PlayerCreaturesManager => Instance.playerView.CreaturesManager;
    public static CreaturesManager EnemyCreaturesManager => Instance.enemyView.CreaturesManager;
    public static Hero PlayerHero => Instance.playerView.Hero;
    public static Hero EnemyHero => Instance.enemyView.Hero;
    public static bool TestMode => Instance.testMode;
    public static EncounterListSO EncounterList => CampaignManager.Instance.EncounterList;

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Overrides the default creature/nuke/spell loadout with a campaign-resolved one — the
    /// player's pool only (Instance.defaultCreatures/defaultNukes/defaultSpells; SlotMachine's
    /// enemy instance reads Instance.enemyCreatures/enemyNukes/enemySpells instead, which this
    /// never touches). Called by CampaignStateManager when a run is active and BattleScene loads;
    /// if CampaignStateManager is absent from the scene, G keeps using its own Inspector-wired
    /// defaults untouched. See docs/G.md.
    /// </summary>
    public static void ApplyCampaignLoadout(CreaturesSO creatures, NukesSO nukes, SpellsSO spells)
    {
        Instance.defaultCreatures = creatures;
        Instance.defaultNukes = nukes;
        Instance.defaultSpells = spells;
    }

    void Update()
    {
        if (testMode && Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
        {
            ExperienceManager.Instance.SpawnTestGem(Vector3.zero);
        }
    }
}
