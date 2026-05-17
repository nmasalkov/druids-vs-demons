using Game._Scripts.PlayerView;
using UnityEngine;
using UnityEngine.InputSystem;

public class G : MonoBehaviour
{
    public static G Instance { get; private set; }

    [SerializeField] private DefaultCreaturesSO defaultCreatures;
    [SerializeField] private CreaturesManager playerCreaturesManager;
    [SerializeField] private CreaturesManager enemyCreaturesManager;
    [SerializeField] private bool testMode = true;

    public static DefaultCreaturesSO DefaultCreatures => Instance.defaultCreatures;
    public static CreaturesManager PlayerCreaturesManager => Instance.playerCreaturesManager;
    public static CreaturesManager EnemyCreaturesManager => Instance.enemyCreaturesManager;
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
