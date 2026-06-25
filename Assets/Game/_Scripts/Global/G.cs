using Game._Scripts.Creatures;
using Game._Scripts.PlayerView;
using UnityEngine;
using UnityEngine.InputSystem;

public class G : MonoBehaviour
{
    public static G Instance { get; private set; }

    [SerializeField] private DefaultCreaturesSO defaultCreatures;
    [SerializeField] private DefaultNukesSO defaultNukes;
    [SerializeField] private DefaultSpellsSO defaultSpells;
    [SerializeField] private HeroView playerView;
    [SerializeField] private HeroView enemyView;
    [SerializeField] private bool testMode = true;

    public static DefaultCreaturesSO DefaultCreatures => Instance.defaultCreatures;
    public static DefaultNukesSO DefaultNukes => Instance.defaultNukes;
    public static DefaultSpellsSO DefaultSpells => Instance.defaultSpells;
    public static HeroView PlayerView => Instance.playerView;
    public static HeroView EnemyView => Instance.enemyView;
    public static CreaturesManager PlayerCreaturesManager => Instance.playerView.CreaturesManager;
    public static CreaturesManager EnemyCreaturesManager => Instance.enemyView.CreaturesManager;
    public static Hero PlayerHero => Instance.playerView.Hero;
    public static Hero EnemyHero => Instance.enemyView.Hero;
    public static bool TestMode => Instance.testMode;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (testMode && Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
        {
            ExperienceManager.Instance.SpawnTestGem(Vector3.zero);
        }
    }
}
