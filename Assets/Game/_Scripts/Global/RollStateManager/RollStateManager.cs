using System;
using System.Collections.Generic;
using System.Linq;
using Game._Scripts.Global;
using UnityEngine;

public class RollStateManager : MonoBehaviour
{
    public static RollStateManager Instance { get; private set; }

    [SerializeField] private SlotMachine playerSlotMachine;
    [SerializeField] private SlotMachine enemySlotMachine;

    /// <summary>
    /// After roll is finished, contains distinct creature types to spawn/promote with their target levels.
    /// </summary>
    public List<SpawnEntry> SpawnEntries { get; private set; } = new();

    /// <summary>
    /// True if the last roll was a triple (3 same cards). Used to trigger re-roll.
    /// </summary>
    public bool TripleRolled { get; private set; }

    public event Action OnRollFinished;

    public struct SpawnEntry
    {
        public CreatureSO Creature;
        public int Level; // target level (1 = single, 2 = pair, 3 = triple)
    }

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
        AnalyzeRoll(creatures);

        var isPlayer = GameManager.Instance.ActiveSide == ActiveSide.Player;
        var activeMachine = isPlayer ? playerSlotMachine : enemySlotMachine;

        if (!isPlayer)
            AIController.Instance.ReleaseControl();

        activeMachine.gameObject.SetActive(false);
        activeMachine.ResetUI();
        OnRollFinished?.Invoke();
    }

    private void AnalyzeRoll(List<CreatureSO> creatures)
    {
        SpawnEntries.Clear();

        var groups = creatures.GroupBy(c => c).ToList();
        TripleRolled = groups.Any(g => g.Count() >= 3);

        foreach (var group in groups)
        {
            SpawnEntries.Add(new SpawnEntry
            {
                Creature = group.Key,
                Level = group.Count() // 1, 2, or 3
            });
        }
    }
}
