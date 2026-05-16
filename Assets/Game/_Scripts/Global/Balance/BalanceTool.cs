using System.Collections.Generic;
using Game._Scripts.Creatures;
using Game._Scripts.PlayerView;
using UnityEngine;

public class BalanceTool : MonoBehaviour
{
    public void SpawnPlayer(CreatureSO creature)
    {
        var manager = G.PlayerCreaturesManager;
        var slot = GetSlotForCreature(manager, creature);
        if (slot.Creature != null)
        {
            HandleExistingCreature(slot);
            return;
        }
        manager.SpawnCreatures(new List<CreatureSO> { creature });
    }

    public void SpawnEnemy(CreatureSO creature)
    {
        var manager = G.EnemyCreaturesManager;
        var slot = GetSlotForCreature(manager, creature);
        if (slot.Creature != null)
        {
            HandleExistingCreature(slot);
            return;
        }
        manager.SpawnCreatures(new List<CreatureSO> { creature });
    }

    private CreatureSlot GetSlotForCreature(CreaturesManager manager, CreatureSO creature)
    {
        return creature switch
        {
            MageSO => manager.MageSlot,
            ArcherSO => manager.ArcherSlot,
            TankSO => manager.TankSlot,
            _ => manager.MageSlot
        };
    }

    private void HandleExistingCreature(CreatureSlot slot)
    {
        var creature = slot.Creature;
        if (creature.Experience.Level >= 4)
        {
            slot.Creature = null;
            Object.Destroy(creature.gameObject);
        }
        else
        {
            creature.Experience.Promote();
        }
    }

    public void SpawnPlayerMage() => SpawnPlayer(G.DefaultCreatures.mage);
    public void SpawnPlayerArcher() => SpawnPlayer(G.DefaultCreatures.archer);
    public void SpawnPlayerTank() => SpawnPlayer(G.DefaultCreatures.tank);

    public void SpawnEnemyMage() => SpawnEnemy(G.DefaultCreatures.mage);
    public void SpawnEnemyArcher() => SpawnEnemy(G.DefaultCreatures.archer);
    public void SpawnEnemyTank() => SpawnEnemy(G.DefaultCreatures.tank);

    public void PlayBattle()
    {
        new BattleState().BeginBattle();
    }
}
