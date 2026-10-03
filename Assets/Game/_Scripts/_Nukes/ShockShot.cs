using Game._Scripts.Creatures;

namespace Game._Scripts.Nukes
{
    /// <summary>
    /// Shock shot — applies the Shocked status to the target. Only <see cref="Creature"/>s can be
    /// shocked: Heroes are immune, and a <see cref="Shield"/> target takes <see cref="ShieldDamage"/>
    /// (0 unless Shock is improved — see <see cref="ShockImprovementSO"/>), so an unimproved bolt
    /// into a standing shield is fully blocked. See <see cref="ShockResolver"/>.
    /// </summary>
    public class ShockShot : NukeShot
    {
        public float ShieldDamage;

        public override void Apply()
        {
            if (Target is Hero) return;
            if (Target is Shield)
            {
                if (ShieldDamage > 0f) ApplyDamage(ShieldDamage);
                return;
            }
            ApplyShock();
        }
    }
}
