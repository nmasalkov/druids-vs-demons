using System;
using System.Collections.Generic;
using Game._Scripts.Global;
using UnityEngine;

public class RollStateManager : MonoBehaviour
{
    public static RollStateManager Instance { get; private set; }

    [SerializeField] private SlotMachine playerSlotMachine;
    [SerializeField] private SlotMachine enemySlotMachine;

    public List<CreatureSO> CreaturesToSpawn { get; private set; }

    public event Action OnRollFinished;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        playerSlotMachine.gameObject.SetActive(false);
        enemySlotMachine.gameObject.SetActive(false);

        playerSlotMachine.OnFinishRollCompleted += HandleFinishRoll;
        enemySlotMachine.OnFinishRollCompleted += HandleFinishRoll;
    }

    void OnDestroy()
    {
        playerSlotMachine.OnFinishRollCompleted -= HandleFinishRoll;
        enemySlotMachine.OnFinishRollCompleted -= HandleFinishRoll;
    }

    public void ActivateSlotMachine()
    {
        var isPlayer = GameManager.Instance.ActiveSide == ActiveSide.Player;
        var machine = isPlayer ? playerSlotMachine : enemySlotMachine;
        machine.gameObject.SetActive(true);

        if (!isPlayer)
            Utils.DoAfterDelay.Execute(() => AIController.Instance.TakeControl(machine), 0f);
    }

    private void HandleFinishRoll(List<CreatureSO> creatures)
    {
        CreaturesToSpawn = creatures;

        var isPlayer = GameManager.Instance.ActiveSide == ActiveSide.Player;
        var activeMachine = isPlayer ? playerSlotMachine : enemySlotMachine;

        if (!isPlayer)
            AIController.Instance.ReleaseControl();

        activeMachine.gameObject.SetActive(false);
        OnRollFinished?.Invoke();
    }
}
