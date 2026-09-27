using Game._Scripts.Global;
using Game._Scripts.PlayerView;
using UnityEngine;

public class SpawnStateManager : MonoBehaviour
{
    public static SpawnStateManager Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    public CreaturesManager GetActiveCreaturesManager()
    {
        return GameManager.Instance.ActiveSide == ActiveSide.Player
            ? G.PlayerCreaturesManager
            : G.EnemyCreaturesManager;
    }
}
