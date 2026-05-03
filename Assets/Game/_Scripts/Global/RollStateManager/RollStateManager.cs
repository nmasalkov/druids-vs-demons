using UnityEngine;

public class RollStateManager : MonoBehaviour
{
    public static RollStateManager Instance { get; private set; }

    [SerializeField] private GameObject playerSlotMachine;
    [SerializeField] private GameObject enemySlotMachine;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        playerSlotMachine.SetActive(false);
        enemySlotMachine.SetActive(false);
    }

    public void ActivatePlayerSlotMachine()
    {
        playerSlotMachine.SetActive(true);
    }

    public void ActivateEnemySlotMachine()
    {
        enemySlotMachine.SetActive(true);
    }
}

