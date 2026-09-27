using Game._Scripts.Creatures;

namespace Game._Scripts.Nukes
{
    /// <summary>
    /// Shock shot — applies the Shocked status to the target. Only <see cref="Creature"/>s can be
    /// shocked: Heroes are immune, and a <see cref="Shield"/> target is a no-op too (the projectile
    /// still flies and impacts, it just does nothing). So a Shock cast with no creature targets is
    /// effectively wasted on the Hero, and one cast into a standing shield is fully blocked — see
    /// <see cref="ShockResolver"/>.
    /// </summary>
    public class ShockShot : NukeShot
    {
        public override void Apply()
        {
            if (Target is Hero) return;
            ApplyShock();
        }
    }
}

