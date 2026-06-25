using Game._Scripts.Creatures;
using Game._Scripts.PlayerView;

namespace Game._Scripts.Spells
{
    /// <summary>
    /// Plans Shield spell outcome for a given roll:
    /// - no shield on field → SPAWN at the rolled level
    /// - rolled level > existing level → PROMOTE (full HP at new level)
    /// - rolled level <= existing level → HEAL by healPerLevel[rolledLevel - 1]
    /// </summary>
    public class ShieldResolver : SpellResolver
    {
        public override void Resolve(SpellSO source, Hero caster, HeroView casterView, int level)
        {
            Shots.Clear();

            var shieldSO = (ShieldSO)source;
            var slot = casterView.ShieldSlot;
            var existing = casterView.Shield;
            bool isPlayer = casterView == G.PlayerView;

            if (existing == null)
            {
                Shots.Add(new ShieldSpawnShot
                {
                    Data = shieldSO,
                    Slot = slot,
                    Level = level,
                    IsPlayer = isPlayer,
                });
                return;
            }

            if (level > existing.Level)
            {
                Shots.Add(new ShieldPromoteShot
                {
                    Target = existing,
                    Level = level,
                });
                return;
            }

            Shots.Add(new ShieldHealShot
            {
                Target = existing,
                Heal = shieldSO.GetHealForLevel(level),
            });
        }
    }
}

