using Game._Scripts.Creatures;
using Game._Scripts.PlayerView;
using UnityEngine;

public class SpawningState : GameState
{
    public override void OnStateStart()
    {
        var creaturesManager = SpawnStateManager.Instance.GetActiveCreaturesManager();
        var entries = RollStateManager.Instance.SpawnEntries;

        foreach (var entry in entries)
        {
            var slot = GetSlotForCreature(creaturesManager, entry.Creature);
            ProcessEntry(entry, slot);
        }

        CompleteState();
    }

    private void ProcessEntry(RollStateManager.SpawnEntry entry, UnitSlot slot)
    {
        if (slot.Creature != null)
        {
            HandleExistingCreature(entry, slot.Creature);
            return;
        }

        SpawnNewCreature(entry, slot);
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

    private void SpawnNewCreature(RollStateManager.SpawnEntry entry, UnitSlot slot)
    {
        var go = Object.Instantiate(entry.Creature.creaturePrefab, slot.transform);
        var creature = go.GetComponent<Creature>();
        slot.Creature = creature;
        creature.Slot = slot;

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
