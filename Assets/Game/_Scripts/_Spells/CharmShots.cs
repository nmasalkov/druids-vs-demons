using Game._Scripts.Creatures;
using Game._Scripts.PlayerView;
using UnityEngine;

namespace Game._Scripts.Spells
{
    /// <summary>
    /// Charm's single shot. All data mutation lives here so the instant-resolve path
    /// (<see cref="SpellResolver.ApplyInstant"/> → Apply()) reaches the exact same end state as
    /// the animated path, which calls Apply() itself mid-animation once the heart beat has played.
    /// The charmed status is a toggle: a creature only ever changes sides via Charm, so charming
    /// a normal creature marks it charmed, and charming an already-charmed one (stealing it back)
    /// clears the mark. Either way the destination is a charm slot — a creature never returns to
    /// a native slot once it has left it. Also clears any BattleCry buff/debuff on every side
    /// change — that multiplier was computed relative to whichever side the creature occupied at
    /// cast time (BattleCryResolver targets purely by CreaturesManager membership), so it's stale
    /// the instant Charm moves the creature elsewhere.
    /// </summary>
    public class CharmShot : SpellShot
    {
        public UnitSlot DestinationSlot;
        public bool Success;

        public Creature Charmed => (Creature)Target;

        public override void Apply()
        {
            if (!Success) return;

            var creature = Charmed;
            creature.Slot.Unit = null;

            var root = creature.transform;
            root.SetParent(DestinationSlot.transform, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            creature.Slot = DestinationSlot;
            DestinationSlot.Unit = creature;

            var statuses = creature.StatusesManager;
            statuses.ClearBattleCry();
            if (statuses.IsCharmed) statuses.ClearCharmed();
            else statuses.ApplyCharmed();
        }
    }
}
