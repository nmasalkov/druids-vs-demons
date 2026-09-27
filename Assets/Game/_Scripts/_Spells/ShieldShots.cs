using Game._Scripts.Creatures;
using Game._Scripts.PlayerView;
using UnityEngine;

namespace Game._Scripts.Spells
{
    /// <summary>Spawn a new Shield at the rolled level into Slot.</summary>
    public class ShieldSpawnShot : SpellShot
    {
        public ShieldSO Data;
        public UnitSlot Slot;
        public int Level;
        public bool IsPlayer;

        public override void Apply()
        {
            var prefab = Data.GetPrefab(IsPlayer);
            var instance = Object.Instantiate(prefab, Slot.transform);
            Slot.Unit = instance;
            instance.Slot = Slot;
            instance.Init(Level);
            instance.OnSummon();
        }
    }

    /// <summary>Promote an existing shield to a higher level (fully refill HP).</summary>
    public class ShieldPromoteShot : SpellShot
    {
        public int Level;

        public override void Apply()
        {
            ((Shield)Target).Promote(Level);
        }
    }

    /// <summary>Heal an existing shield by a flat amount.</summary>
    public class ShieldHealShot : SpellShot
    {
        public float Heal;

        public override void Apply()
        {
            Target.Health.Heal(Heal);
        }
    }
}

