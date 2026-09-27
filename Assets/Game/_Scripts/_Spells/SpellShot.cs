using Game._Scripts.Creatures;

namespace Game._Scripts.Spells
{
    /// <summary>
    /// Pre-computed unit of spell effect. Produced by a <see cref="SpellResolver"/> and applied
    /// either through <see cref="SpellResolver.ApplyInstant"/> or at a moment of the animation's
    /// choosing (mirrors <c>NukeShot</c>).
    /// </summary>
    public abstract class SpellShot
    {
        /// <summary>Target this shot affects, when applicable (heal target, etc.). May be null
        /// for spells that don't have a single target.</summary>
        public Targetable Target;

        public abstract void Apply();
    }
}

