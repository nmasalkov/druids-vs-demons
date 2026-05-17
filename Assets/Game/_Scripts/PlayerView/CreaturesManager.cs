using System.Collections.Generic;
using Game._Scripts.Creatures;
using UnityEngine;

namespace Game._Scripts.PlayerView
{
    public class CreaturesManager : MonoBehaviour
    {
        [SerializeField] private UnitSlot mageSlot;
        [SerializeField] private UnitSlot archerSlot;
        [SerializeField] private UnitSlot tankSlot;

        public UnitSlot MageSlot => mageSlot;
        public UnitSlot ArcherSlot => archerSlot;
        public UnitSlot TankSlot => tankSlot;

        public Creature Mage => mageSlot.Creature;
        public Creature Archer => archerSlot.Creature;
        public Creature Tank => tankSlot.Creature;

        public List<Creature> GetAllCreatures()
        {
            var list = new List<Creature>();
            if (tankSlot.Creature != null) list.Add(tankSlot.Creature);
            if (mageSlot.Creature != null) list.Add(mageSlot.Creature);
            if (archerSlot.Creature != null) list.Add(archerSlot.Creature);
            return list;
        }

        public void SpawnCreatures(List<CreatureSO> creatures)
        {
            foreach (var creatureSO in creatures)
            {
                switch (creatureSO)
                {
                    case MageSO when mageSlot.Creature == null:
                        mageSlot.Creature = Instantiate(creatureSO.creaturePrefab, mageSlot.transform).GetComponent<Creature>();
                        mageSlot.Creature.Slot = mageSlot;
                        break;
                    case ArcherSO when archerSlot.Creature == null:
                        archerSlot.Creature = Instantiate(creatureSO.creaturePrefab, archerSlot.transform).GetComponent<Creature>();
                        archerSlot.Creature.Slot = archerSlot;
                        break;
                    case TankSO when tankSlot.Creature == null:
                        tankSlot.Creature = Instantiate(creatureSO.creaturePrefab, tankSlot.transform).GetComponent<Creature>();
                        tankSlot.Creature.Slot = tankSlot;
                        break;
                }
            }
        }
        public void CleanUpDead()
        {
            CleanSlot(mageSlot);
            CleanSlot(archerSlot);
            CleanSlot(tankSlot);
        }

        private void CleanSlot(UnitSlot slot)
        {
            if (slot.Creature != null && slot.Creature.Health.IsDead())
                slot.Creature.DestroyCreature();
        }
    }
}
