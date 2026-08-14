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
    /// After a creature roll is finished, contains distinct creature types to spawn/promote with their target levels.
    /// </summary>
    public List<SpawnEntry> SpawnEntries { get; private set; } = new();

    /// <summary>
    /// After a nuke roll is finished, contains distinct nukes with their match counts (1, 2, or 3).
    /// </summary>
    public List<NukeEntry> NukeEntries { get; private set; } = new();
    /// <summary>
    /// After a spell roll is finished, contains distinct spells with their match counts (1, 2, or 3).
    /// </summary>
    public List<SpellEntry> SpellEntries { get; private set; } = new();

    /// <summary>
    /// True if the last roll was a triple (3 same cards). Used to trigger re-roll.
    /// </summary>
    public bool TripleRolled { get; private set; }

    /// <summary>
    /// Roll type of the last finished roll.
    /// </summary>
    public SlotMachine.RollType LastRollType { get; private set; } = SlotMachine.RollType.Creature;

    public event Action OnRollFinished;

    /// <summary>Which side's machine is active right now, tracking GameManager.ActiveSide live —
    /// one source of truth, never a separately-cached field.</summary>
    public SlotMachine ActiveMachine =>
        GameManager.Instance.ActiveSide == ActiveSide.Player ? playerSlotMachine : enemySlotMachine;

    public struct SpawnEntry
    {
        public CreatureSO Creature;
        public int Level; // target level (1 = single, 2 = pair, 3 = triple)
    }

    public struct NukeEntry : IActionEntry
    {
        public NukeSO Nuke;
        public int Count; // 1, 2, or 3
        public ActionSO Source => Nuke;
        public int Level => Count;
    }

    public struct SpellEntry : IActionEntry
    {
        public SpellSO Spell;
        public int Count; // 1, 2, or 3
        public ActionSO Source => Spell;
        public int Level => Count;
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
        GameManager.OnBattleRestart += ResetForRestart;
    }

    void OnDestroy()
    {
        playerSlotMachine.OnFinishRollCompleted -= HandleFinishRoll;
        enemySlotMachine.OnFinishRollCompleted -= HandleFinishRoll;
        GameManager.OnBattleRestart -= ResetForRestart;
    }

    public void ActivateSlotMachine()
    {
        // Outside a real fight (LoadoutPickSO/RewardPickSO), the slot machine must never be
        // activated at all — SlotMachine.Update() reads Keyboard.current.spaceKey directly,
        // bypassing UI raycast blocking entirely, so an EncounterPlayer overlay alone can't stop
        // a stray Space press from spinning reels behind it. Unity never runs Update() on an
        // inactive GameObject, so this keeps the slot machine (and RollState, which waits on it)
        // genuinely inert rather than just visually hidden. See docs/Encounters.md.
        if (CampaignManager.Instance.CurrentEncounter is not FightSO) return;
        ActiveMachine.gameObject.SetActive(true);
    }

    private void HandleFinishRoll(List<ActionSO> actions, SlotMachine.RollType rollType)
    {
        LastRollType = rollType;
        AnalyzeRoll(actions, rollType);

        var activeMachine = ActiveMachine;
        activeMachine.gameObject.SetActive(false);
        activeMachine.ResetUI();
        OnRollFinished?.Invoke();
    }

    private void AnalyzeRoll(List<ActionSO> actions, SlotMachine.RollType rollType)
    {
        SpawnEntries.Clear();
        NukeEntries.Clear();
        SpellEntries.Clear();

        var groups = actions.GroupBy(a => a).ToList();
        TripleRolled = groups.Any(g => g.Count() >= 3);

        if (rollType == SlotMachine.RollType.Nuke)
        {
            foreach (var group in groups)
            {
                NukeEntries.Add(new NukeEntry
                {
                    Nuke = (NukeSO)group.Key,
                    Count = group.Count()
                });
            }
            return;
        }

        if (rollType == SlotMachine.RollType.Spell)
        {
            foreach (var group in groups)
            {
                SpellEntries.Add(new SpellEntry
                {
                    Spell = (SpellSO)group.Key,
                    Count = group.Count()
                });
            }
            return;
        }

        foreach (var group in groups)
        {
            SpawnEntries.Add(new SpawnEntry
            {
                Creature = (CreatureSO)group.Key,
                Level = group.Count()
            });
        }
    }

    /// <summary>Clears roll results and returns both slot machines to their default (inactive)
    /// state. Used by battle restart.</summary>
    public void ResetForRestart()
    {
        SpawnEntries.Clear();
        NukeEntries.Clear();
        SpellEntries.Clear();
        TripleRolled = false;
        LastRollType = SlotMachine.RollType.Creature;

        playerSlotMachine.ResetUI();
        enemySlotMachine.ResetUI();
        playerSlotMachine.gameObject.SetActive(false);
        enemySlotMachine.gameObject.SetActive(false);
    }
}
