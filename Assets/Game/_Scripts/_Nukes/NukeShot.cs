using Game._Scripts.Creatures;

namespace Game._Scripts.Nukes
{
    /// <summary>
    /// Pre-computed unit of nuke effect. Produced by a <see cref="NukeResolver"/> and consumed
    /// by a <see cref="NukeActionAnimation"/> (which decides WHEN to call <see cref="Apply"/> —
    /// e.g. on projectile impact) or applied immediately via <see cref="NukeResolver.ApplyInstant"/>.
    ///
    /// Subclasses describe WHAT happens by overriding <see cref="Apply"/> and calling the
    /// provided helpers (<see cref="ApplyDamage"/>, <see cref="ApplyShock"/>, ...). Add more
    /// helpers here as new effect types are introduced.
    /// </summary>
    public abstract class NukeShot
    {
        public Targetable Target;

        public abstract void Apply();

        protected void ApplyDamage(float damage)
        {
            if (Target == null) return;
            Target.Health.TakeDamage(damage);
        }

        protected void ApplyShock()
        {
            if (Target == null) return;
            // Shock only applies to Units (creatures/heroes); shields and other Targetables ignore it.
            if (Target is Unit unit)
                unit.StatusesManager.ApplyShock();
        }
    }
}
