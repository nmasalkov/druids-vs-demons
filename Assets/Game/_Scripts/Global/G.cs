using Game._Scripts.PlayerView;
using UnityEngine;

public class G : MonoBehaviour
{
    public static G Instance { get; private set; }

    [SerializeField] private DefaultCreaturesSO defaultCreatures;
    [SerializeField] private CreaturesManager playerCreaturesManager;
    [SerializeField] private CreaturesManager enemyCreaturesManager;

    public static DefaultCreaturesSO DefaultCreatures => Instance.defaultCreatures;
    public static CreaturesManager PlayerCreaturesManager => Instance.playerCreaturesManager;
    public static CreaturesManager EnemyCreaturesManager => Instance.enemyCreaturesManager;

    void Awake()
    {
        Instance = this;
    }
}
