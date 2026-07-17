using System.Collections.Generic;
using Game._Scripts.Creatures;
using Game._Scripts.Global;
using UnityEngine;

namespace Game._Scripts.PlayerView
{
    public class CreaturesManager : MonoBehaviour
    {
        [SerializeField] private UnitSlot mageSlot;
        [SerializeField] private UnitSlot archerSlot;
        [SerializeField] private UnitSlot tankSlot;

        [Header("Charm slots (2 per class, filled by the Charm spell)")]
        [SerializeField] private UnitSlot[] mageCharmSlots = new UnitSlot[2];
        [SerializeField] private UnitSlot[] archerCharmSlots = new UnitSlot[2];
        [SerializeField] private UnitSlot[] tankCharmSlots = new UnitSlot[2];

        public UnitSlot MageSlot => mageSlot;
        public UnitSlot ArcherSlot => archerSlot;
        public UnitSlot TankSlot => tankSlot;

        public Creature Mage => mageSlot.Creature;
        public Creature Archer => archerSlot.Creature;
        public Creature Tank => tankSlot.Creature;

        void Start()
        {
            GameManager.OnBattleRestart += ResetAll;
        }

        void OnDestroy()
        {
            GameManager.OnBattleRestart -= ResetAll;
        }

        /// <summary>Every slot on this manager: natives first, then each class's charm slots.</summary>
        private IEnumerable<UnitSlot> AllSlots()
        {
            yield return tankSlot;
            yield return mageSlot;
            yield return archerSlot;
            foreach (var slot in tankCharmSlots) yield return slot;
            foreach (var slot in mageCharmSlots) yield return slot;
            foreach (var slot in archerCharmSlots) yield return slot;
        }

        public List<Creature> GetAllCreatures()
        {
            var list = new List<Creature>();
            foreach (var slot in AllSlots())
                if (slot.Creature != null) list.Add(slot.Creature);
            return list;
        }

        /// <summary>The native (non-charm) slot for a creature class.</summary>
        public UnitSlot GetNativeSlot(CreatureSO creature)
        {
            return creature switch
            {
                MageSO => mageSlot,
                ArcherSO => archerSlot,
                TankSO => tankSlot,
                _ => mageSlot
            };
        }

        private UnitSlot[] CharmSlotsFor(CreatureSO creature)
        {
            return creature switch
            {
                MageSO => mageCharmSlots,
                ArcherSO => archerCharmSlots,
                TankSO => tankCharmSlots,
                _ => mageCharmSlots
            };
        }

        /// <summary>First free charm slot for a creature class, or null if both are occupied.</summary>
        public UnitSlot GetFreeCharmSlot(CreatureSO creature)
        {
            foreach (var slot in CharmSlotsFor(creature))
                if (slot.Creature == null) return slot;
            return null;
        }

        /// <summary>Creatures currently occupying the charm slots of a given class.</summary>
        public List<Creature> GetCharmSlotCreatures(CreatureSO creature)
        {
            var list = new List<Creature>();
            foreach (var slot in CharmSlotsFor(creature))
                if (slot.Creature != null) list.Add(slot.Creature);
            return list;
        }

        /// <summary>
        /// Reads the BattleCry buff multiplier already active on this side's creatures (1f if
        /// none is active). Lets a creature summoned by a bonus roll, after BattleCry was cast
        /// earlier the same turn, join the buff already in effect for that battle.
        /// </summary>
        public float GetActiveBattleCryBuffMultiplier()
        {
            foreach (var creature in GetAllCreatures())
            {
                float multiplier = creature.StatusesManager.AttackDamageMultiplier;
                if (multiplier != 1f) return multiplier;
            }
            return 1f;
        }

        public void SpawnCreatures(List<CreatureSO> creatures)
        {
            foreach (var creatureSO in creatures)
            {
                var slot = GetNativeSlot(creatureSO);
                if (slot.Creature != null) continue;

                var creature = Instantiate(creatureSO.creaturePrefab, slot.transform).GetComponent<Creature>();
                slot.Creature = creature;
                creature.Slot = slot;
            }
        }

        public void CleanUpDead()
        {
            foreach (var slot in AllSlots())
                CleanSlot(slot);
        }

        /// <summary>Destroys every creature in every slot (native + charm), instantly, no death
        /// animation. Used for a full battle restart.</summary>
        public void ResetAll()
        {
            foreach (var slot in AllSlots())
            {
                if (slot.Creature == null) continue;
                Destroy(slot.Creature.gameObject);
                slot.Creature = null;
            }
        }

        private void CleanSlot(UnitSlot slot)
        {
            if (slot.Creature != null && slot.Creature.Health.IsDead())
                slot.Creature.DestroyCreature();
        }
    }
}
