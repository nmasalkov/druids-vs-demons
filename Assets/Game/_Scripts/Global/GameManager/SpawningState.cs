using Game._Scripts.Creatures;
using Game._Scripts.PlayerView;
using UnityEngine;

public class SpawningState : ActionState
{
    protected override void OnEnter()
    {
        var creaturesManager = SpawnStateManager.Instance.GetActiveCreaturesManager();
        var entries = RollStateManager.Instance.SpawnEntries;

        foreach (var entry in entries)
        {
            var slot = GetSlotForCreature(creaturesManager, entry.Creature);
            ProcessEntry(entry, slot, creaturesManager);
        }

        CompleteState();
    }

    private void ProcessEntry(RollStateManager.SpawnEntry entry, UnitSlot slot, CreaturesManager creaturesManager)
    {
        if (slot.Creature != null)
        {
            HandleExistingCreature(entry, slot.Creature);
            return;
        }

        SpawnNewCreature(entry, slot, creaturesManager);
    }

    private void HandleExistingCreature(RollStateManager.SpawnEntry entry, Creature creature)
    {
        if (entry.Level > creature.Experience.Level)
        {
            creature.Experience.PromoteToLevel(entry.Level);
            return;
        }

        int index = Mathf.Clamp(entry.Level - 1, 0, entry.Creature.healAmounts.Length - 1);
        creature.Health.Heal(entry.Creature.healAmounts[index]);
    }

    private void SpawnNewCreature(RollStateManager.SpawnEntry entry, UnitSlot slot, CreaturesManager creaturesManager)
    {
        var go = Object.Instantiate(entry.Creature.creaturePrefab, slot.transform);
        var creature = go.GetComponent<Creature>();
        slot.Creature = creature;
        creature.Slot = slot;
        creature.OnSummon();

        // Join a BattleCry buff already active from earlier this same turn (e.g. cast, then a
        // triple's bonus roll summons this creature before the battle it should also buff).
        float activeBuff = creaturesManager.GetActiveBattleCryBuffMultiplier();
        if (activeBuff != 1f)
            creature.StatusesManager.ApplyBattleCryBuff(activeBuff);

        if (entry.Level <= 1) return;

        int lvl = entry.Level;
        Utils.DoAfterDelay.Execute(() => creature.Experience.PromoteToLevel(lvl), 0f);
    }

    private UnitSlot GetSlotForCreature(CreaturesManager manager, CreatureSO creature)
    {
        return creature switch
        {
            MageSO => manager.MageSlot,
            ArcherSO => manager.ArcherSlot,
            TankSO => manager.TankSlot,
            _ => manager.MageSlot
        };
    }
}
